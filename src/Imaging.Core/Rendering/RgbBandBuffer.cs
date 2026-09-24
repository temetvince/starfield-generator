using Imaging.Core.Colors;

namespace Imaging.Core.Rendering;

/// <summary>
/// A scratch buffer of linear-light pixels covering one band, which layers add their light into.
/// </summary>
/// <remarks>
/// <para>
/// Storage is three floats per pixel with no alpha. Layers here are emissive: they add light rather
/// than covering what is underneath, so an opacity channel would carry no information. Alpha, where an
/// exported layer needs it, is derived at encode time from how much light landed.
/// </para>
/// <para>
/// A buffer is sized once for the tallest band and then reused, so a long render allocates a fixed
/// number of these rather than one per band. It is not thread-safe: give each worker its own.
/// </para>
/// </remarks>
public sealed class RgbBandBuffer
{
    private readonly float[] _pixels;
    private int _height;

    /// <summary>Creates a buffer.</summary>
    /// <param name="width">Row length in pixels. Must be positive.</param>
    /// <param name="capacityRows">
    /// The largest number of rows the buffer will ever hold. Must be positive. <see cref="Height"/> may
    /// later be reduced for a short final band, but never raised past this.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">Either argument is zero or negative.</exception>
    public RgbBandBuffer(int width, int capacityRows)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacityRows);

        var length = (long)width * capacityRows * 3;
        if (length > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(capacityRows), "The band is too large to allocate.");
        }

        Width = width;
        CapacityRows = capacityRows;
        _height = capacityRows;
        _pixels = new float[(int)length];
    }

    /// <summary>Gets the row length in pixels.</summary>
    /// <value>The width passed to the constructor.</value>
    public int Width { get; }

    /// <summary>Gets the largest number of rows this buffer can hold.</summary>
    /// <value>The capacity passed to the constructor.</value>
    public int CapacityRows { get; }

    /// <summary>Gets or sets how many rows are currently in use.</summary>
    /// <value>In <c>[1, CapacityRows]</c>. Lower it for the short final band of an image.</value>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside the allowed range.</exception>
    public int Height
    {
        get => _height;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, CapacityRows);
            _height = value;
        }
    }

    /// <summary>Resets every pixel in the active region to black.</summary>
    /// <remarks>Call this before reusing a buffer for the next band; layers add, they do not overwrite.</remarks>
    public void Clear() => Array.Clear(_pixels, 0, Width * Height * 3);

    /// <summary>Adds light to one pixel, ignoring positions outside the buffer.</summary>
    /// <param name="x">Column in <c>[0, Width)</c>. Out-of-range values are dropped.</param>
    /// <param name="localRow">Row in <c>[0, Height)</c>. Out-of-range values are dropped.</param>
    /// <param name="light">The light to add. Components may be any magnitude.</param>
    /// <remarks>
    /// Silently dropping out-of-range writes is deliberate: it lets a layer splat a shape that overhangs
    /// the band or the image edge without every caller writing the same clipping loop.
    /// </remarks>
    public void Add(int x, int localRow, LinearRgb light)
    {
        if ((uint)x >= (uint)Width || (uint)localRow >= (uint)Height)
        {
            return;
        }

        var index = ((localRow * Width) + x) * 3;
        _pixels[index] += light.R;
        _pixels[index + 1] += light.G;
        _pixels[index + 2] += light.B;
    }

    /// <summary>Reads one pixel.</summary>
    /// <param name="x">Column in <c>[0, Width)</c>.</param>
    /// <param name="localRow">Row in <c>[0, Height)</c>.</param>
    /// <returns>The accumulated light at that pixel.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Either coordinate is outside the active region.</exception>
    public LinearRgb Get(int x, int localRow)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(x, Width);
        ArgumentOutOfRangeException.ThrowIfNegative(localRow);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(localRow, Height);

        var index = ((localRow * Width) + x) * 3;
        return new LinearRgb(_pixels[index], _pixels[index + 1], _pixels[index + 2]);
    }

    /// <summary>Gets the raw storage for one row, as interleaved red, green and blue.</summary>
    /// <param name="localRow">Row in <c>[0, Height)</c>.</param>
    /// <returns>A span of <c>Width * 3</c> floats that writes straight through to the buffer.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="localRow"/> is outside the active region.</exception>
    public Span<float> GetRow(int localRow)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(localRow);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(localRow, Height);

        return _pixels.AsSpan(localRow * Width * 3, Width * 3);
    }
}
