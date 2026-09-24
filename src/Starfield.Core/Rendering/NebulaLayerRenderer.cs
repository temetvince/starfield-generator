using Imaging.Core.Colors;
using Imaging.Core.Noise;
using Imaging.Core.Numerics;
using Imaging.Core.Rendering;
using Starfield.Core.Options;

namespace Starfield.Core.Rendering;

/// <summary>
/// Draws the gas clouds: a coverage mask decides where, a warped structure field decides what shape,
/// and a colour ramp decides what it glows.
/// </summary>
/// <remarks>
/// <para>
/// Cost is a fixed number of noise samples per pixel, so a nebula over a very wide image is expensive
/// in time but flat in memory. Nothing is cached between bands, which is what lets bands render in any
/// order on any thread.
/// </para>
/// <para>
/// The renderer is immutable and safe to use from several band workers at once.
/// </para>
/// </remarks>
public sealed class NebulaLayerRenderer : ILayerRenderer
{
    private readonly NebulaOptions _options;
    private readonly FractalNoise _coverage;
    private readonly FractalNoise _structure;
    private readonly FractalNoise _warpX;
    private readonly FractalNoise _warpY;
    private readonly ColorGradient _gradient;
    private readonly StarDensityField? _density;

    /// <summary>Creates a nebula renderer.</summary>
    /// <param name="options">
    /// The cloud settings. Must already have passed validation; the noise fields reject bad shapes
    /// themselves.
    /// </param>
    /// <param name="image">The image the clouds are drawn into, whose aspect ratio sets the vertical repeat.</param>
    /// <param name="seed">The field's seed.</param>
    /// <param name="seamlessX">
    /// <see langword="true"/> to wrap every noise field to a whole number of cells across the image
    /// width, which makes the clouds tile horizontally.
    /// </param>
    /// <param name="seamlessY"><see langword="true"/> to wrap every noise field to a whole number of cells down the image height as well.</param>
    /// <param name="density">
    /// The clustering field, so the clouds can gather onto the galactic band, or
    /// <see langword="null"/> to let them fall wherever the coverage field puts them.
    /// </param>
    /// <param name="name">The layer's identifier, used in logs and exported file names.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> or <paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">One of the noise fields is not valid.</exception>
    public NebulaLayerRenderer(
        NebulaOptions options,
        ImageSize image,
        ulong seed,
        bool seamlessX,
        bool seamlessY = false,
        StarDensityField? density = null,
        string name = "nebula")
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(name);

        _options = options;
        _density = density;
        Name = name;

        // Noise is sampled in turns of the image width, so the vertical period is the aspect ratio.
        var verticalPeriod = seamlessY ? (float)image.Height / image.Width : 0.0f;
        _coverage = new FractalNoise(Hash64.Combine(seed, 101UL), options.Coverage with { SeamlessX = seamlessX, VerticalPeriod = verticalPeriod });
        _structure = new FractalNoise(Hash64.Combine(seed, 102UL), options.Structure with { SeamlessX = seamlessX, VerticalPeriod = verticalPeriod });
        _warpX = new FractalNoise(Hash64.Combine(seed, 103UL), options.Warp with { SeamlessX = seamlessX, VerticalPeriod = verticalPeriod });
        _warpY = new FractalNoise(Hash64.Combine(seed, 104UL), options.Warp with { SeamlessX = seamlessX, VerticalPeriod = verticalPeriod });

        // Each seed picks one authored palette, then nudges its hues a little. Choosing from a list is
        // what keeps every seed on a ramp a person judged to look good, while the nudge keeps two seeds
        // that drew the same palette from being identical in colour.
        var palette = options.Palettes[(int)(Hash64.Combine(seed, 105UL) % (ulong)options.Palettes.Count)];
        PaletteName = palette.Name;
        HueShift = (Hash64.UnitInterval(Hash64.Combine(seed, 106UL), 0UL) - 0.5f) * options.HueVariation;
        _gradient = new ColorGradient(palette.ColorStops.Select(stop =>
        {
            var authored = stop.ToGradientStop();
            return authored with { Colour = authored.Colour.RotateHue(HueShift) };
        }));
    }

    /// <inheritdoc/>
    public string Name { get; }

    /// <summary>Gets the name of the palette this renderer's seed chose.</summary>
    /// <value>The <see cref="NebulaPaletteOptions.Name"/> of one of the options' palettes.</value>
    public string PaletteName { get; }

    /// <summary>Gets how far this renderer's seed nudged the chosen palette's hues.</summary>
    /// <value>
    /// In turns of the colour wheel, within plus or minus half of
    /// <see cref="NebulaOptions.HueVariation"/>. Zero when variation is disabled.
    /// </value>
    public float HueShift { get; }

    /// <inheritdoc/>
    public void RenderBand(BandRegion band, RgbBandBuffer target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (!_options.Enabled || _options.Intensity <= 0.0f)
        {
            return;
        }

        // Both axes are measured in turns of the image width, so cloud features stay round instead of
        // stretching when the image is far wider than it is tall.
        var scale = 1.0f / band.Image.Width;
        var coverageEdge = _options.CoverageThreshold;
        var coverageTop = coverageEdge + _options.CoverageSoftness;
        var warp = _options.WarpStrength;
        var affinity = _density is null ? 0.0f : _options.BandAffinity;

        for (var localRow = 0; localRow < band.Height; localRow++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var imageRow = band.ToImageRow(localRow) + 0.5f;
            var v = imageRow * scale;

            for (var x = 0; x < band.Image.Width; x++)
            {
                var u = (x + 0.5f) * scale;

                var mask = Interpolation.Smoothstep(coverageEdge, coverageTop, _coverage.Sample(u, v));

                if (affinity > 0.0f)
                {
                    // Clouds sit in the galactic plane. Weighting coverage by the band is what stops the
                    // nebulae and the star band from reading as two unrelated pictures in one frame.
                    var plane = _density!.SampleBand(x + 0.5f, imageRow);
                    mask *= 1.0f - affinity + (affinity * plane);
                }

                if (mask <= 0.0f)
                {
                    continue;
                }

                var offsetX = ((_warpX.Sample(u, v) * 2.0f) - 1.0f) * warp;
                var offsetY = ((_warpY.Sample(u, v) * 2.0f) - 1.0f) * warp;

                var structure = _structure.Sample(u + offsetX, v + offsetY);
                var density = Interpolation.Smoothstep(_options.DensityThreshold, 1.0f, structure);
                if (density <= 0.0f)
                {
                    continue;
                }

                density = MathF.Pow(density, _options.DensityContrast);
                target.Add(x, localRow, _gradient.Sample(density) * (density * mask * _options.Intensity));
            }
        }
    }
}
