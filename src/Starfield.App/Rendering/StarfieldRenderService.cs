using Starfield.Core;
using Starfield.Core.Options;

namespace Starfield.App.Rendering;

/// <summary>
/// Renders with the star field library in this process, on a worker thread.
/// </summary>
/// <remarks>
/// Nothing is launched: the library is compiled into the application, and progress comes from the band
/// loop's own hook rather than from parsing anything.
/// </remarks>
public sealed class StarfieldRenderService : IRenderService
{
    /// <inheritdoc/>
    public Task RenderAsync(
        StarfieldOptions options,
        string outputPath,
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(progress);

        return Task.Run(
            () => new StarfieldRenderer(options).RenderToFile(outputPath, progress, cancellationToken),
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task<byte[]> RenderPngAsync(StarfieldOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        return Task.Run(
            () =>
            {
                using var stream = new MemoryStream();
                new StarfieldRenderer(options).RenderTo(stream, cancellationToken: cancellationToken);
                return stream.ToArray();
            },
            cancellationToken);
    }
}
