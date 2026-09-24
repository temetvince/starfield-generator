using Starfield.Core.Options;

namespace Starfield.App.Rendering;

/// <summary>
/// Renders star fields, so a view model can be tested without rendering anything.
/// </summary>
public interface IRenderService
{
    /// <summary>Renders one composite PNG to a file.</summary>
    /// <param name="options">The field to render. Must already be valid.</param>
    /// <param name="outputPath">Where the PNG goes. Any existing file is replaced.</param>
    /// <param name="progress">Receives the fraction complete, in <c>[0, 1]</c>, from an arbitrary thread.</param>
    /// <param name="cancellationToken">Stops the render early, leaving a partial file behind.</param>
    /// <returns>A task that completes when the file is written.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
    /// <exception cref="IOException">The file could not be written.</exception>
    /// <exception cref="UnauthorizedAccessException">The file could not be written.</exception>
    Task RenderAsync(
        StarfieldOptions options,
        string outputPath,
        IProgress<double> progress,
        CancellationToken cancellationToken);

    /// <summary>Renders one composite PNG into memory.</summary>
    /// <param name="options">The field to render. Must already be valid.</param>
    /// <param name="cancellationToken">Stops the render early.</param>
    /// <returns>The PNG file's bytes.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
    Task<byte[]> RenderPngAsync(StarfieldOptions options, CancellationToken cancellationToken);
}
