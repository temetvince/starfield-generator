using Imaging.Core.Colors;
using Imaging.Core.Png;
using Imaging.Core.Rendering;

namespace Imaging.Core.Tests;

/// <summary>Checks the final stage: tone mapping, background, dithering and alpha extraction.</summary>
public sealed class BandEncoderTests
{
    private static readonly ImageSize Size = new(4, 4);

    [Fact]
    public void Encode_WithAlphaFromLight_CompositesBackOverBlackToTheOpaqueResult()
    {
        var buffer = FilledBuffer();
        var band = new BandRegion(Size, 0, buffer.Height);

        var opaqueOptions = new ImageEncodingOptions { DitherStrength = 0.0f };
        var layerOptions = new ImageEncodingOptions { DitherStrength = 0.0f, AlphaMode = AlphaMode.FromLight };

        var opaque = new byte[BandEncoder.RequiredLength(Size.Width, band.Height, PngColorType.Rgb)];
        var layered = new byte[BandEncoder.RequiredLength(Size.Width, band.Height, PngColorType.Rgba)];

        BandEncoder.Encode(buffer, band, opaqueOptions, opaque);
        BandEncoder.Encode(buffer, band, layerOptions, layered);

        for (var pixel = 0; pixel < Size.Width * band.Height; pixel++)
        {
            var alpha = layered[(pixel * 4) + 3] / 255.0f;

            for (var channel = 0; channel < 3; channel++)
            {
                var composited = layered[(pixel * 4) + channel] * alpha;
                Assert.InRange(composited, opaque[(pixel * 3) + channel] - 2.0f, opaque[(pixel * 3) + channel] + 2.0f);
            }
        }
    }

    [Fact]
    public void Encode_AddsTheBackgroundOnlyForOpaqueOutput()
    {
        var buffer = new RgbBandBuffer(Size.Width, Size.Height);
        buffer.Clear();
        var band = new BandRegion(Size, 0, Size.Height);

        var background = new LinearRgb(0.25f, 0.5f, 0.75f);
        var opaque = new byte[BandEncoder.RequiredLength(Size.Width, Size.Height, PngColorType.Rgb)];
        var layered = new byte[BandEncoder.RequiredLength(Size.Width, Size.Height, PngColorType.Rgba)];

        BandEncoder.Encode(
            buffer,
            band,
            new ImageEncodingOptions
            {
                Background = background,
                DitherStrength = 0.0f,
                ToneMapping = ToneMappingCurve.Clamp,
            },
            opaque);

        BandEncoder.Encode(
            buffer,
            band,
            new ImageEncodingOptions
            {
                Background = background,
                DitherStrength = 0.0f,
                AlphaMode = AlphaMode.FromLight,
            },
            layered);

        Assert.Equal(Srgb.Quantise(Srgb.FromLinear(0.25f), 0.0f), opaque[0]);
        Assert.Equal(0, layered[3]);
    }

    [Fact]
    public void Encode_WithoutDither_IsRepeatable()
    {
        var buffer = FilledBuffer();
        var band = new BandRegion(Size, 0, buffer.Height);
        var options = new ImageEncodingOptions { DitherStrength = 0.0f };

        var first = new byte[BandEncoder.RequiredLength(Size.Width, band.Height, PngColorType.Rgb)];
        var second = new byte[first.Length];

        BandEncoder.Encode(buffer, band, options, first);
        BandEncoder.Encode(buffer, band, options, second);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Encode_DitherDependsOnImagePositionRatherThanBandPosition()
    {
        // The same pixel encoded as part of two differently placed bands must get the same dither, or
        // banding seams would appear wherever a band boundary falls.
        var buffer = FilledBuffer();
        var options = new ImageEncodingOptions { DitherStrength = 1.0f };

        var atTop = new byte[BandEncoder.RequiredLength(Size.Width, buffer.Height, PngColorType.Rgb)];
        var lowerDown = new byte[atTop.Length];

        BandEncoder.Encode(buffer, new BandRegion(Size, 0, buffer.Height), options, atTop);
        BandEncoder.Encode(buffer, new BandRegion(Size, 0, buffer.Height), options, lowerDown);

        Assert.Equal(atTop, lowerDown);

        var shifted = new byte[atTop.Length];
        BandEncoder.Encode(buffer, new BandRegion(Size, 2, buffer.Height), options, shifted);

        Assert.NotEqual(atTop, shifted);
    }

    [Fact]
    public void Encode_WithATooShortDestination_Throws()
    {
        var buffer = FilledBuffer();
        var band = new BandRegion(Size, 0, buffer.Height);

        Assert.Throws<ArgumentException>(() =>
        {
            var destination = new byte[3];
            BandEncoder.Encode(buffer, band, new ImageEncodingOptions(), destination);
        });
    }

    [Fact]
    public void Encode_WithAMismatchedBandHeight_Throws()
    {
        var buffer = FilledBuffer();

        Assert.Throws<ArgumentException>(() =>
        {
            var destination = new byte[BandEncoder.RequiredLength(Size.Width, 1, PngColorType.Rgb)];
            BandEncoder.Encode(buffer, new BandRegion(Size, 0, 1), new ImageEncodingOptions(), destination);
        });
    }

    [Fact]
    public void RgbBandBuffer_DropsWritesOutsideItsBounds()
    {
        var buffer = new RgbBandBuffer(4, 2);
        buffer.Clear();

        buffer.Add(-1, 0, LinearRgb.White);
        buffer.Add(4, 0, LinearRgb.White);
        buffer.Add(0, -1, LinearRgb.White);
        buffer.Add(0, 2, LinearRgb.White);

        Assert.Equal(LinearRgb.Black, buffer.Get(0, 0));
        Assert.Equal(LinearRgb.Black, buffer.Get(3, 1));
    }

    [Fact]
    public void RgbBandBuffer_AccumulatesRatherThanReplacing()
    {
        var buffer = new RgbBandBuffer(2, 1);
        buffer.Clear();

        buffer.Add(0, 0, new LinearRgb(0.25f, 0.0f, 0.0f));
        buffer.Add(0, 0, new LinearRgb(0.5f, 0.0f, 0.0f));

        Assert.Equal(0.75f, buffer.Get(0, 0).R, 5);
    }

    [Fact]
    public void RgbBandBuffer_RejectsAHeightAboveItsCapacity()
    {
        var buffer = new RgbBandBuffer(2, 3);

        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.Height = 4);
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.Height = 0);
    }

    private static RgbBandBuffer FilledBuffer()
    {
        var buffer = new RgbBandBuffer(Size.Width, Size.Height);
        buffer.Clear();

        for (var y = 0; y < Size.Height; y++)
        {
            for (var x = 0; x < Size.Width; x++)
            {
                buffer.Add(x, y, new LinearRgb(0.1f * (x + 1), 0.05f * (y + 1), 1.5f));
            }
        }

        return buffer;
    }
}
