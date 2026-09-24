namespace Imaging.Core.Rendering;

/// <summary>
/// One emissive layer of an image, able to render any horizontal slice of itself on demand.
/// </summary>
/// <remarks>
/// <para>
/// This is the seam the whole renderer is built on. A layer must be a pure function of the band it is
/// given: rendering rows 0–63 and then 64–127 must produce exactly the pixels that rendering rows 0–127
/// in one go would have produced. Implementations therefore derive any randomness from coordinates, and
/// keep no state that carries between calls.
/// </para>
/// <para>
/// Implementations must tolerate being called concurrently for different bands, on different threads,
/// with different buffers. Read-only shared state is fine; mutable per-render scratch is not, unless it
/// is thread-local.
/// </para>
/// </remarks>
public interface ILayerRenderer
{
    /// <summary>Gets a short identifier used in logs and in exported per-layer file names.</summary>
    /// <value>A non-empty name, unique within one render.</value>
    string Name { get; }

    /// <summary>Adds this layer's light into the buffer for one band.</summary>
    /// <param name="band">The slice to render, including the size of the image it belongs to.</param>
    /// <param name="target">
    /// The buffer to add into, whose <see cref="RgbBandBuffer.Height"/> matches the band. The layer adds
    /// to what is already there and never clears it.
    /// </param>
    /// <param name="cancellationToken">Cancels a long band. Implementations should observe it between rows.</param>
    void RenderBand(BandRegion band, RgbBandBuffer target, CancellationToken cancellationToken);
}
