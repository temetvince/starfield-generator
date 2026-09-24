using System.Collections.Immutable;
using Imaging.Core.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Starfield.Core.Options;
using Starfield.Core.Rendering;
using static System.FormattableString;

namespace Starfield.Core;

/// <summary>
/// Turns a set of options into finished PNG files, either as one composite or as separate layers.
/// </summary>
/// <remarks>
/// <para>
/// This is the entry point application code is expected to use. It owns the translation from
/// configuration into layer renderers and hands the actual work to the reusable banded renderer.
/// </para>
/// <para>
/// An instance is bound to one set of options, validated at construction, and may be reused for any
/// number of renders.
/// </para>
/// </remarks>
public sealed class StarfieldRenderer
{
    private const int FileBufferSize = 1 << 20;

    private readonly StarfieldOptions _options;
    private readonly ILogger<StarfieldRenderer> _logger;
    private readonly BandedImageRenderer _renderer;

    /// <summary>Creates a renderer bound to one set of options.</summary>
    /// <param name="options">
    /// The field to render. Rejected here rather than at render time if
    /// <see cref="StarfieldOptions.Validate"/> reports anything.
    /// </param>
    /// <param name="loggerFactory">
    /// Where lifecycle and progress go. Pass <see langword="null"/> for a silent renderer.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="options"/> is not valid.</exception>
    public StarfieldRenderer(StarfieldOptions options, ILoggerFactory? loggerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        var problems = options.Validate();
        if (problems.Count > 0)
        {
            throw new ArgumentException(string.Join(" ", problems), nameof(options));
        }

        _options = options;

        var factory = loggerFactory ?? NullLoggerFactory.Instance;
        _logger = factory.CreateLogger<StarfieldRenderer>();
        _renderer = new BandedImageRenderer(factory.CreateLogger<BandedImageRenderer>());
    }

    /// <summary>Builds the layer renderers described by the options, in composite order.</summary>
    /// <returns>
    /// The nebula first, if enabled, then each star layer. Every entry is safe to render concurrently.
    /// </returns>
    public ImmutableArray<ILayerRenderer> CreateLayers()
    {
        var layers = ImmutableArray.CreateBuilder<ILayerRenderer>();

        // One clustering field is shared by every layer, so the whole sky agrees on where the galactic
        // band and the star clouds are.
        var density = new StarDensityField(_options.Clustering, _options.Size, _options.Seed, _options.SeamlessX);

        if (_options.Nebula.Enabled)
        {
            var nebula = new NebulaLayerRenderer(_options.Nebula, _options.Seed, _options.SeamlessX, density);
            Log.NebulaPaletteChosen(_logger, _options.Seed, nebula.PaletteName, nebula.HueShift);
            layers.Add(nebula);
        }

        if (_options.Clustering.Enabled && _options.Clustering.HazeIntensity > 0.0f)
        {
            layers.Add(new StarHazeLayerRenderer(_options.Clustering, density));
        }

        for (var index = 0; index < _options.StarLayers.Count; index++)
        {
            layers.Add(new StarLayerRenderer(
                _options.StarLayers[index],
                _options.Size,
                _options.Seed,
                index,
                _options.SeamlessX,
                density));
        }

        return layers.ToImmutable();
    }

    /// <summary>Renders the whole field as one opaque image.</summary>
    /// <param name="destination">Where the PNG goes. Left open, and never seeked.</param>
    /// <param name="progress">
    /// Receives the fraction complete, in <c>[0, 1]</c>, as bands are written, or <see langword="null"/>
    /// for no reporting.
    /// </param>
    /// <param name="cancellationToken">Cancels the render, leaving a partial file behind.</param>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The token was signalled.</exception>
    public void RenderTo(Stream destination, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);

        var layers = CreateLayers();
        Log.CompositeStarted(_logger, _options.Width, _options.Height, _options.Seed, layers.Length);
        var renderOptions = _options.ToRenderOptions(AlphaMode.Opaque) with { Progress = progress };
        _renderer.Render(destination, _options.Size, layers, renderOptions, cancellationToken);
    }

    /// <summary>Renders the whole field as one opaque image and writes it to a file.</summary>
    /// <param name="path">
    /// Where to write. Any existing file is replaced. The parent directory must already exist.
    /// </param>
    /// <param name="progress">
    /// Receives the fraction complete, in <c>[0, 1]</c>, as bands are written, or <see langword="null"/>
    /// for no reporting.
    /// </param>
    /// <param name="cancellationToken">Cancels the render, leaving a partial file behind.</param>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="IOException">The file could not be written.</exception>
    /// <exception cref="OperationCanceledException">The token was signalled.</exception>
    public void RenderToFile(string path, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, FileBufferSize);
        RenderTo(file, progress, cancellationToken);
        Log.FileWritten(_logger, path, file.Length);
    }

    /// <summary>
    /// Renders each layer on its own into a transparent PNG, ready to be composited as parallax planes.
    /// </summary>
    /// <param name="directory">
    /// Where the files go. Created if it does not exist. Existing files with the same names are replaced.
    /// </param>
    /// <param name="fileNameStem">
    /// The leading part of each file name. Names are formed as
    /// <c>{stem}-{index}-{layer}.png</c>, so the natural sort order matches the composite order.
    /// </param>
    /// <param name="cancellationToken">Cancels the run, leaving the files written so far in place.</param>
    /// <returns>The paths written, in composite order.</returns>
    /// <exception cref="ArgumentException">Either argument is empty or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
    /// <exception cref="IOException">A file could not be written.</exception>
    /// <exception cref="OperationCanceledException">The token was signalled.</exception>
    /// <remarks>
    /// Each file carries alpha derived from the light that landed in it, and no background. Compositing
    /// them in order over the background therefore reproduces the opaque render.
    /// </remarks>
    public ImmutableArray<string> RenderLayerFiles(
        string directory,
        string fileNameStem,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileNameStem);

        Directory.CreateDirectory(directory);

        var layers = CreateLayers();
        var renderOptions = _options.ToRenderOptions(AlphaMode.FromLight);
        var written = ImmutableArray.CreateBuilder<string>(layers.Length);

        for (var index = 0; index < layers.Length; index++)
        {
            var layer = layers[index];
            var fileName = Invariant($"{fileNameStem}-{index:D2}-{Sanitise(layer.Name)}.png");
            var path = Path.Combine(directory, fileName);

            Log.LayerStarted(_logger, layer.Name, path);

            using (var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, FileBufferSize))
            {
                _renderer.Render(file, _options.Size, [layer], renderOptions, cancellationToken);
                Log.FileWritten(_logger, path, file.Length);
            }

            written.Add(path);
        }

        return written.ToImmutable();
    }

    /// <summary>Replaces anything a file name cannot contain with a hyphen.</summary>
    /// <param name="name">The layer name to clean up.</param>
    /// <returns>A name safe to place in a path.</returns>
    private static string Sanitise(string name)
    {
        Span<char> buffer = stackalloc char[name.Length];
        ReadOnlySpan<char> invalid = Path.GetInvalidFileNameChars();

        for (var index = 0; index < name.Length; index++)
        {
            var character = name[index];
            buffer[index] = invalid.Contains(character) || char.IsWhiteSpace(character) ? '-' : character;
        }

        return new string(buffer).ToLowerInvariant();
    }
}
