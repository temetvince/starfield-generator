using Imaging.Core.Colors;
using Imaging.Core.Png;
using Imaging.Core.Rendering;
using Imaging.TestSupport;
using NSubstitute;

namespace Imaging.Core.Tests;

/// <summary>
/// Checks the guarantee the whole design rests on: how an image is divided into bands must not change
/// a single pixel of the result.
/// </summary>
public sealed class BandedImageRendererTests
{
    private static readonly ImageSize Size = new(53, 41);

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(40)]
    [InlineData(41)]
    [InlineData(500)]
    public void Render_ProducesTheSamePixelsAtEveryBandHeight(int bandHeight)
    {
        var reference = Render(new BandedRenderOptions { BandHeight = 41, MaxDegreeOfParallelism = 1 });
        var banded = Render(new BandedRenderOptions { BandHeight = bandHeight });

        Assert.True(
            DecodedPng.Decode(reference).Pixels.SequenceEqual(DecodedPng.Decode(banded).Pixels),
            $"Band height {bandHeight} changed the image.");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(8)]
    public void Render_ProducesTheSamePixelsAtEveryThreadCount(int threads)
    {
        var reference = Render(new BandedRenderOptions { BandHeight = 5, MaxDegreeOfParallelism = 1 });
        var parallel = Render(new BandedRenderOptions { BandHeight = 5, MaxDegreeOfParallelism = threads });

        Assert.True(DecodedPng.Decode(reference).Pixels.SequenceEqual(DecodedPng.Decode(parallel).Pixels));
    }

    [Fact]
    public void Render_CoversEveryRowExactlyOnce()
    {
        var layer = Substitute.For<ILayerRenderer>();
        layer.Name.Returns("mock");

        var bands = new List<BandRegion>();
        layer
            .When(mock => mock.RenderBand(Arg.Any<BandRegion>(), Arg.Any<RgbBandBuffer>(), Arg.Any<CancellationToken>()))
            .Do(call =>
            {
                var band = call.Arg<BandRegion>();
                lock (bands)
                {
                    bands.Add(band);
                }
            });

        using var stream = new MemoryStream();
        new BandedImageRenderer().Render(stream, Size, [layer], new BandedRenderOptions { BandHeight = 7 });

        List<BandRegion> ordered = [.. bands.OrderBy(band => band.Top)];
        var expectedTop = 0;

        foreach (var band in ordered)
        {
            Assert.Equal(expectedTop, band.Top);
            Assert.InRange(band.Height, 1, 7);
            expectedTop = band.Bottom;
        }

        Assert.Equal(Size.Height, expectedTop);
    }

    [Fact]
    public void Render_WithNoLayers_WritesTheBackground()
    {
        var options = new BandedRenderOptions
        {
            BandHeight = 9,
            Encoding = new ImageEncodingOptions
            {
                Background = new LinearRgb(1.0f, 0.0f, 0.0f),
                ToneMapping = ToneMappingCurve.Clamp,
                DitherStrength = 0.0f,
            },
        };

        using var stream = new MemoryStream();
        new BandedImageRenderer().Render(stream, Size, [], options);

        var decoded = DecodedPng.Decode(stream.ToArray());

        Assert.Equal(PngColorType.Rgb, decoded.ColorType);
        Assert.Equal(255, decoded.GetChannel(10, 10, 0));
        Assert.Equal(0, decoded.GetChannel(10, 10, 1));
    }

    [Fact]
    public void Render_WithAlphaFromLight_WritesAnRgbaFileThatIgnoresTheBackground()
    {
        var options = new BandedRenderOptions
        {
            BandHeight = 9,
            Encoding = new ImageEncodingOptions
            {
                Background = new LinearRgb(1.0f, 1.0f, 1.0f),
                AlphaMode = AlphaMode.FromLight,
                DitherStrength = 0.0f,
            },
        };

        using var stream = new MemoryStream();
        new BandedImageRenderer().Render(stream, Size, [], options);

        var decoded = DecodedPng.Decode(stream.ToArray());

        Assert.Equal(PngColorType.Rgba, decoded.ColorType);
        Assert.Equal(0, decoded.GetChannel(5, 5, 3));
    }

    [Fact]
    public void Render_WhenCancelled_Throws()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        using var stream = new MemoryStream();

        Assert.ThrowsAny<OperationCanceledException>(() => new BandedImageRenderer().Render(
            stream,
            Size,
            [new GradientLayer()],
            new BandedRenderOptions { BandHeight = 4 },
            cancellation.Token));
    }

    [Fact]
    public void Render_WithInvalidOptions_Throws()
    {
        using var stream = new MemoryStream();

        Assert.Throws<ArgumentException>(() => new BandedImageRenderer().Render(
            stream,
            Size,
            [],
            new BandedRenderOptions { BandHeight = 0 }));
    }

    [Fact]
    public void Render_RespectsTheWorkingSetBudgetWithoutChangingThePixels()
    {
        var unbounded = Render(new BandedRenderOptions { BandHeight = 5, MaxWorkingSetBytes = 0 });
        var squeezed = Render(new BandedRenderOptions { BandHeight = 5, MaxWorkingSetBytes = 1 });

        Assert.True(DecodedPng.Decode(unbounded).Pixels.SequenceEqual(DecodedPng.Decode(squeezed).Pixels));
    }

    [Fact]
    public void Render_ReportsProgressUpToOne()
    {
        var reported = new List<double>();
        var progress = new SynchronousProgress(reported.Add);

        Render(new BandedRenderOptions { BandHeight = 5, MaxDegreeOfParallelism = 1, Progress = progress });

        Assert.NotEmpty(reported);
        Assert.Equal(1.0, reported[^1]);
        Assert.Equal([.. reported.Order()], reported);
        Assert.All(reported, fraction => Assert.InRange(fraction, 0.0, 1.0));
    }

    private sealed class SynchronousProgress(Action<double> handler) : IProgress<double>
    {
        public void Report(double value) => handler(value);
    }

    private static byte[] Render(BandedRenderOptions options)
    {
        using var stream = new MemoryStream();
        new BandedImageRenderer().Render(stream, Size, [new GradientLayer()], options);
        return stream.ToArray();
    }

    /// <summary>
    /// A layer whose output depends only on absolute image coordinates, so any band that covers a pixel
    /// must produce the same value for it.
    /// </summary>
    private sealed class GradientLayer : ILayerRenderer
    {
        public string Name => "gradient";

        public void RenderBand(BandRegion band, RgbBandBuffer target, CancellationToken cancellationToken)
        {
            for (var localRow = 0; localRow < band.Height; localRow++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var imageRow = band.ToImageRow(localRow);

                for (var x = 0; x < band.Image.Width; x++)
                {
                    var red = (float)x / band.Image.Width;
                    var green = (float)imageRow / band.Image.Height;
                    var blue = ((x * 7919) ^ (imageRow * 104729)) % 997 / 997.0f;
                    target.Add(x, localRow, new LinearRgb(red, green, blue));
                }
            }
        }
    }
}
