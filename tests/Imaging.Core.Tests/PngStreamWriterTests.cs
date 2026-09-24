using System.IO.Compression;
using Imaging.Core.Numerics;
using Imaging.Core.Png;
using Imaging.TestSupport;

namespace Imaging.Core.Tests;

/// <summary>Checks that the encoder writes files a decoder can read back byte for byte.</summary>
public sealed class PngStreamWriterTests
{
    [Theory]
    [InlineData(PngColorType.Rgb)]
    [InlineData(PngColorType.Rgba)]
    public void WriteRows_RoundTripsEveryPixel(PngColorType colorType)
    {
        const int Width = 37;
        const int Height = 23;

        var expected = RandomPixels(Width, Height, PngStreamWriter.BytesPerPixel(colorType), seed: 5);
        var file = Encode(Width, Height, colorType, expected, CompressionLevel.Optimal);

        var decoded = DecodedPng.Decode(file);

        Assert.Equal(Width, decoded.Width);
        Assert.Equal(Height, decoded.Height);
        Assert.Equal(colorType, decoded.ColorType);
        Assert.True(expected.AsSpan().SequenceEqual(decoded.Pixels));
    }

    [Theory]
    [InlineData(CompressionLevel.NoCompression)]
    [InlineData(CompressionLevel.Fastest)]
    [InlineData(CompressionLevel.SmallestSize)]
    public void WriteRows_RoundTripsAtEveryCompressionLevel(CompressionLevel level)
    {
        const int Width = 19;
        const int Height = 11;

        var expected = RandomPixels(Width, Height, 3, seed: 9);
        var file = Encode(Width, Height, PngColorType.Rgb, expected, level);

        Assert.True(expected.AsSpan().SequenceEqual(DecodedPng.Decode(file).Pixels));
    }

    [Fact]
    public void WriteRows_AcceptsRowsInAnyGrouping()
    {
        const int Width = 8;
        const int Height = 6;

        var expected = RandomPixels(Width, Height, 3, seed: 11);

        using var oneAtATime = new MemoryStream();
        using (var writer = new PngStreamWriter(oneAtATime, Width, Height, PngColorType.Rgb, leaveOpen: true))
        {
            for (var row = 0; row < Height; row++)
            {
                writer.WriteRows(expected.AsSpan(row * Width * 3, Width * 3));
            }
        }

        var allAtOnce = Encode(Width, Height, PngColorType.Rgb, expected, CompressionLevel.Optimal);

        Assert.True(oneAtATime.ToArray().AsSpan().SequenceEqual(allAtOnce));
    }

    [Fact]
    public void WriteRows_WithPartialRow_Throws()
    {
        using var stream = new MemoryStream();
        using var writer = new PngStreamWriter(stream, 4, 4, PngColorType.Rgb, leaveOpen: true);

        Assert.Throws<ArgumentException>(() => writer.WriteRows(new byte[5]));
    }

    [Fact]
    public void WriteRows_PastDeclaredHeight_Throws()
    {
        using var stream = new MemoryStream();
        using var writer = new PngStreamWriter(stream, 4, 2, PngColorType.Rgb, leaveOpen: true);

        writer.WriteRows(new byte[4 * 3 * 2]);

        Assert.Throws<InvalidOperationException>(() => writer.WriteRows(new byte[4 * 3]));
    }

    [Fact]
    public void Complete_BeforeEveryRowIsWritten_Throws()
    {
        using var stream = new MemoryStream();
        using var writer = new PngStreamWriter(stream, 4, 4, PngColorType.Rgb, leaveOpen: true);

        writer.WriteRows(new byte[4 * 3]);

        Assert.Throws<InvalidOperationException>(writer.Complete);
    }

    [Fact]
    public void Constructor_WithNonPositiveDimension_Throws()
    {
        using var stream = new MemoryStream();

        Assert.Throws<ArgumentOutOfRangeException>(() => new PngStreamWriter(stream, 0, 4, PngColorType.Rgb));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PngStreamWriter(stream, 4, -1, PngColorType.Rgb));
    }

    [Fact]
    public void Dispose_WithLeaveOpenFalse_ClosesTheDestination()
    {
        var stream = new MemoryStream();

        using (var writer = new PngStreamWriter(stream, 2, 1, PngColorType.Rgb))
        {
            writer.WriteRows(new byte[2 * 3]);
        }

        Assert.False(stream.CanWrite);
    }

    [Fact]
    public void WriteRows_AcrossManyIdatChunks_StillRoundTrips()
    {
        // Wide enough that the compressed data spills past the one-mebibyte chunk buffer, which is the
        // only way the multi-chunk path gets exercised.
        const int Width = 4000;
        const int Height = 400;

        var expected = RandomPixels(Width, Height, 3, seed: 3);
        var file = Encode(Width, Height, PngColorType.Rgb, expected, CompressionLevel.NoCompression);

        var decoded = DecodedPng.Decode(file);

        Assert.True(expected.AsSpan().SequenceEqual(decoded.Pixels));
    }

    private static byte[] Encode(int width, int height, PngColorType colorType, byte[] pixels, CompressionLevel level)
    {
        using var stream = new MemoryStream();
        using (var writer = new PngStreamWriter(stream, width, height, colorType, level, leaveOpen: true))
        {
            writer.WriteRows(pixels);
        }

        return stream.ToArray();
    }

    private static byte[] RandomPixels(int width, int height, int bytesPerPixel, ulong seed)
    {
        var random = new DeterministicRandom(seed);
        var pixels = new byte[width * height * bytesPerPixel];

        for (var index = 0; index < pixels.Length; index++)
        {
            pixels[index] = (byte)(random.NextBits() & 0xFF);
        }

        return pixels;
    }
}
