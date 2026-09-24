using Imaging.Core.Png;
using Imaging.TestSupport;
using Starfield.Core.Options;

namespace Starfield.Core.Tests;

/// <summary>Checks the end-to-end renderer: reproducibility, seams and layer export.</summary>
public sealed class StarfieldRendererTests
{
    private static StarfieldOptions SmallField => StarfieldPresets.Default() with
    {
        Width = 240,
        Height = 120,
        Seed = 4242UL,
    };

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(64)]
    [InlineData(4096)]
    public void Render_ProducesTheSameImageAtEveryBandHeight(int bandHeight)
    {
        var reference = Render(SmallField with { BandHeight = 120, MaxDegreeOfParallelism = 1 });
        var banded = Render(SmallField with { BandHeight = bandHeight });

        Assert.True(
            DecodedPng.Decode(reference).Pixels.SequenceEqual(DecodedPng.Decode(banded).Pixels),
            $"Band height {bandHeight} changed the image.");
    }

    [Fact]
    public void Render_ProducesTheSameImageAtEveryThreadCount()
    {
        var single = Render(SmallField with { BandHeight = 11, MaxDegreeOfParallelism = 1 });
        var many = Render(SmallField with { BandHeight = 11, MaxDegreeOfParallelism = 8 });

        Assert.True(DecodedPng.Decode(single).Pixels.SequenceEqual(DecodedPng.Decode(many).Pixels));
    }

    [Fact]
    public void Render_DependsOnTheSeed()
    {
        var first = Render(SmallField with { Seed = 1UL });
        var second = Render(SmallField with { Seed = 2UL });

        Assert.False(DecodedPng.Decode(first).Pixels.SequenceEqual(DecodedPng.Decode(second).Pixels));
    }

    [Fact]
    public void Render_WhenSeamless_JoinsAsSmoothlyAsAnyNeighbouringColumns()
    {
        // Clouds only, so the comparison measures the noise fields rather than whether a star happened
        // to land on an edge column.
        var nebulaOnly = SmallField with { StarLayers = [], DitherStrength = 0.0f };

        var seamless = DecodedPng.Decode(Render(nebulaOnly with { SeamlessX = true }));
        var plain = DecodedPng.Decode(Render(nebulaOnly with { SeamlessX = false }));

        // Under wrapping, the last column and the first are neighbours, so they should differ about as
        // much as any other neighbouring pair. Comparing against a real neighbouring pair rather than
        // against a fixed number keeps the test honest whatever the preset's contrast happens to be.
        var seam = ColumnDifference(seamless, seamless.Width - 1, 0);
        var neighbours = ColumnDifference(seamless, 0, 1);
        var unwrapped = ColumnDifference(plain, plain.Width - 1, 0);

        Assert.True(
            seam <= (neighbours * 2.0) + 1.0,
            $"The seam differed by {seam:F2} where neighbouring columns differ by {neighbours:F2}.");

        Assert.True(
            unwrapped > seam * 1.5,
            $"Without wrapping the seam differed by only {unwrapped:F2} against {seam:F2} with it.");
    }

    [Fact]
    public void RenderLayerFiles_WritesOneTransparentFilePerLayer()
    {
        var options = SmallField;
        var directory = Path.Combine(Path.GetTempPath(), "starfield-tests", Guid.NewGuid().ToString("N"));

        try
        {
            var renderer = new StarfieldRenderer(options);
            IReadOnlyList<string> written = renderer.RenderLayerFiles(directory, "field");

            Assert.Equal(renderer.CreateLayers().Length, written.Count);

            foreach (var path in written)
            {
                var decoded = DecodedPng.Decode(File.ReadAllBytes(path));

                Assert.Equal(PngColorType.Rgba, decoded.ColorType);
                Assert.Equal(options.Width, decoded.Width);
                Assert.Equal(options.Height, decoded.Height);
            }

            Assert.Contains(written, path => Path.GetFileName(path).StartsWith("field-00-nebula", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void RenderLayerFiles_ProducesLayersThatAreMostlyTransparent()
    {
        var options = SmallField with
        {
            Nebula = SmallField.Nebula with { Enabled = false },
            Clustering = SmallField.Clustering with { HazeIntensity = 0.0f },
        };
        var directory = Path.Combine(Path.GetTempPath(), "starfield-tests", Guid.NewGuid().ToString("N"));

        try
        {
            IReadOnlyList<string> written = new StarfieldRenderer(options).RenderLayerFiles(directory, "layer");
            var decoded = DecodedPng.Decode(File.ReadAllBytes(written[0]));

            var opaque = 0;
            for (var y = 0; y < decoded.Height; y++)
            {
                for (var x = 0; x < decoded.Width; x++)
                {
                    if (decoded.GetChannel(x, y, 3) > 8)
                    {
                        opaque++;
                    }
                }
            }

            Assert.InRange(opaque, 1, decoded.Width * decoded.Height / 2);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void CreateLayers_BuildsOneLayerPerEnabledFeature()
    {
        var stars = SmallField.StarLayers.Count;

        // The nebula and the unresolved-star haze each add a layer of their own.
        Assert.Equal(stars + 2, new StarfieldRenderer(SmallField).CreateLayers().Length);

        var withoutNebula = SmallField with { Nebula = SmallField.Nebula with { Enabled = false } };
        Assert.Equal(stars + 1, new StarfieldRenderer(withoutNebula).CreateLayers().Length);

        var withoutHaze = SmallField with { Clustering = SmallField.Clustering with { HazeIntensity = 0.0f } };
        Assert.Equal(stars + 1, new StarfieldRenderer(withoutHaze).CreateLayers().Length);

        var bare = withoutNebula with { Clustering = SmallField.Clustering with { Enabled = false } };
        Assert.Equal(stars, new StarfieldRenderer(bare).CreateLayers().Length);
    }

    [Fact]
    public void Constructor_WithInvalidOptions_Throws()
    {
        Assert.Throws<ArgumentException>(() => new StarfieldRenderer(SmallField with { Width = 0 }));
        Assert.Throws<ArgumentNullException>(() => new StarfieldRenderer(null!));
    }

    [Fact]
    public void RenderToFile_WritesAReadablePng()
    {
        var path = Path.Combine(Path.GetTempPath(), $"starfield-{Guid.NewGuid():N}.png");

        try
        {
            new StarfieldRenderer(SmallField).RenderToFile(path);
            var decoded = DecodedPng.Decode(File.ReadAllBytes(path));

            Assert.Equal(240, decoded.Width);
            Assert.Equal(120, decoded.Height);
            Assert.Equal(PngColorType.Rgb, decoded.ColorType);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Measures how far apart two columns are, per channel.</summary>
    /// <param name="image">The decoded image.</param>
    /// <param name="left">One column index.</param>
    /// <param name="right">The other column index.</param>
    /// <returns>The mean absolute channel difference between the two columns.</returns>
    private static double ColumnDifference(DecodedPng image, int left, int right)
    {
        var total = 0.0;

        for (var y = 0; y < image.Height; y++)
        {
            for (var channel = 0; channel < 3; channel++)
            {
                total += Math.Abs(image.GetChannel(left, y, channel) - image.GetChannel(right, y, channel));
            }
        }

        return total / (image.Height * 3);
    }

    private static byte[] Render(StarfieldOptions options)
    {
        using var stream = new MemoryStream();
        new StarfieldRenderer(options).RenderTo(stream);
        return stream.ToArray();
    }
}
