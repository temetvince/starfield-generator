using Imaging.Core.Colors;
using Imaging.Core.Numerics;
using Imaging.Core.Png;

namespace Imaging.Core.Rendering;

/// <summary>
/// Turns a band of accumulated linear light into the bytes a PNG row expects.
/// </summary>
/// <remarks>
/// Encoding is a pure function of the buffer contents, the band position and the options, so it can run
/// on the same worker that rendered the band. Only the actual file write has to be serialised.
/// </remarks>
public static class BandEncoder
{
    /// <summary>Reports how many bytes <see cref="Encode"/> will write for a band.</summary>
    /// <param name="width">Image width in pixels.</param>
    /// <param name="rows">Number of rows in the band.</param>
    /// <param name="colorType">The colour type being written.</param>
    /// <returns>The required destination length in bytes.</returns>
    public static int RequiredLength(int width, int rows, PngColorType colorType)
        => width * rows * PngStreamWriter.BytesPerPixel(colorType);

    /// <summary>Encodes one band.</summary>
    /// <param name="source">
    /// The accumulated light. Its <see cref="RgbBandBuffer.Height"/> must equal the band height.
    /// </param>
    /// <param name="band">Where the band sits in the image, which anchors the dither pattern.</param>
    /// <param name="options">Exposure, tone curve, background, dithering and alpha behaviour.</param>
    /// <param name="destination">
    /// Receives the rows back to back, in the channel order the PNG colour type implies. Must be at
    /// least <see cref="RequiredLength"/> bytes; anything beyond that is left untouched.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The buffer height does not match the band, or <paramref name="destination"/> is too short.
    /// </exception>
    public static void Encode(RgbBandBuffer source, BandRegion band, ImageEncodingOptions options, Span<byte> destination)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);

        if (source.Height != band.Height)
        {
            throw new ArgumentException("The buffer height does not match the band height.", nameof(source));
        }

        var required = RequiredLength(source.Width, band.Height, options.ColorType);
        if (destination.Length < required)
        {
            throw new ArgumentException("The destination is too short for this band.", nameof(destination));
        }

        var withAlpha = options.AlphaMode == AlphaMode.FromLight;
        var background = withAlpha ? LinearRgb.Black : options.Background;
        var dither = options.DitherStrength;
        var index = 0;

        for (var localRow = 0; localRow < band.Height; localRow++)
        {
            var imageRow = band.ToImageRow(localRow);
            ReadOnlySpan<float> row = source.GetRow(localRow);

            for (var x = 0; x < source.Width; x++)
            {
                var offset = x * 3;
                var light = new LinearRgb(row[offset], row[offset + 1], row[offset + 2]);
                var mapped = ToneMapper.Apply(light + background, options.ToneMapping, options.Exposure);

                var red = Srgb.FromLinear(mapped.R);
                var green = Srgb.FromLinear(mapped.G);
                var blue = Srgb.FromLinear(mapped.B);
                var alpha = 1.0f;

                if (withAlpha)
                {
                    // Alpha is how much light landed; colour is that light normalised. Compositing the
                    // result over black therefore reproduces the opaque render exactly.
                    alpha = MathF.Max(red, MathF.Max(green, blue));
                    if (alpha > 0.0f)
                    {
                        var scale = 1.0f / alpha;
                        red *= scale;
                        green *= scale;
                        blue *= scale;
                    }
                }

                destination[index++] = Srgb.Quantise(red, Dither(options, x, imageRow, 0, dither));
                destination[index++] = Srgb.Quantise(green, Dither(options, x, imageRow, 1, dither));
                destination[index++] = Srgb.Quantise(blue, Dither(options, x, imageRow, 2, dither));

                if (withAlpha)
                {
                    destination[index++] = Srgb.Quantise(alpha, Dither(options, x, imageRow, 3, dither));
                }
            }
        }
    }

    /// <summary>
    /// Produces the triangular noise offset for one channel of one pixel.
    /// </summary>
    /// <remarks>
    /// The offset is derived from the pixel's image coordinates rather than from a running generator, so
    /// a pixel dithers identically no matter which band it was rendered in.
    /// </remarks>
    private static float Dither(ImageEncodingOptions options, int x, int y, int channel, float strength)
    {
        if (strength <= 0.0f)
        {
            return 0.0f;
        }

        var seed = Hash64.Combine(options.DitherSeed, x, y, channel);

        // Two uniforms subtracted give a triangular distribution, which is quieter than a flat one.
        var first = Hash64.UnitInterval(seed, 0UL);
        var second = Hash64.UnitInterval(seed, 1UL);
        return (first - second) * strength;
    }
}
