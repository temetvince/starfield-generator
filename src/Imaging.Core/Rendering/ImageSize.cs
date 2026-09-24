namespace Imaging.Core.Rendering;

/// <summary>Pixel dimensions of an image, validated on construction.</summary>
/// <remarks>
/// The type exists so that a width and a height cannot be swapped silently at a call site, and so that
/// "positive dimensions" is checked once rather than by every consumer.
/// </remarks>
public readonly record struct ImageSize
{
    /// <summary>Creates a size.</summary>
    /// <param name="width">Width in pixels. Must be positive.</param>
    /// <param name="height">Height in pixels. Must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException">Either dimension is zero or negative.</exception>
    public ImageSize(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        Width = width;
        Height = height;
    }

    /// <summary>Gets the width in pixels.</summary>
    /// <value>Always positive.</value>
    public int Width { get; }

    /// <summary>Gets the height in pixels.</summary>
    /// <value>Always positive.</value>
    public int Height { get; }

    /// <summary>Gets the total pixel count.</summary>
    /// <value>
    /// Computed as a 64-bit value, because a wide panorama can exceed what a 32-bit count would hold.
    /// </value>
    public long PixelCount => (long)Width * Height;
}
