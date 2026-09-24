namespace Imaging.Core.Rendering;

/// <summary>
/// A horizontal slice of an image, plus the size of the image it belongs to.
/// </summary>
/// <param name="Image">The full image size, which layers need in order to place themselves.</param>
/// <param name="Top">The image row this band starts at. Zero is the top of the image.</param>
/// <param name="Height">How many rows this band covers. The last band of an image may be shorter than the rest.</param>
/// <remarks>
/// A layer renderer is handed one of these and must produce exactly the same pixels it would have
/// produced had it rendered the whole image at once. Keeping the full image size in the band is what
/// makes that possible without any shared state between bands.
/// </remarks>
public readonly record struct BandRegion(ImageSize Image, int Top, int Height)
{
    /// <summary>Gets the image row just past the bottom of the band.</summary>
    /// <value><see cref="Top"/> plus <see cref="Height"/>, never more than the image height.</value>
    public int Bottom => Top + Height;

    /// <summary>Converts a row within the band to a row within the image.</summary>
    /// <param name="localRow">A row index in <c>[0, Height)</c>.</param>
    /// <returns>The corresponding image row.</returns>
    public int ToImageRow(int localRow) => Top + localRow;
}
