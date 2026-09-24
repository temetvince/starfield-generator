using Imaging.Core.Rendering;
using Starfield.Core.Options;
using Starfield.Core.Rendering;

namespace Starfield.Core.Tests;

/// <summary>
/// Checks that clustering actually gathers stars, and that the haze that stands in for the unresolved
/// ones agrees with where they went.
/// </summary>
public sealed class ClusteringTests
{
    private static readonly ImageSize Size = new(1200, 800);

    private static StarClusteringOptions Banded => new()
    {
        Floor = 0.1f,
        BandStrength = 2.0f,
        BandCentre = 0.5f,
        BandWidth = 0.12f,
        BandWobble = 0.0f,
        ClumpStrength = 0.0f,
        DustStrength = 0.0f,
    };

    private static StarLayerOptions Layer => new()
    {
        Name = "test",
        DensityPerMegapixel = 4000.0f,
        GlowRadiusScale = 3.0f,
        MaxRadius = 0.6f,
    };

    [Fact]
    public void Collect_GathersStarsOntoTheBand()
    {
        var density = new StarDensityField(Banded, Size, 1UL, seamlessX: true);
        var field = new StarCellField(Layer, Size, 1UL, 0, seamlessX: true, seamlessY: false, density);

        var onBand = CountBetween(field, 360, 440);
        var offBand = CountBetween(field, 0, 80);

        Assert.True(onBand > offBand * 3, $"The band held {onBand} stars against {offBand} at the edge.");
    }

    [Fact]
    public void Collect_WithNoResponse_IgnoresClustering()
    {
        var density = new StarDensityField(Banded, Size, 1UL, seamlessX: true);
        var layer = Layer with { ClusteringResponse = 0.0f };
        var field = new StarCellField(layer, Size, 1UL, 0, seamlessX: true, seamlessY: false, density);

        var onBand = CountBetween(field, 360, 440);
        var offBand = CountBetween(field, 0, 80);

        Assert.InRange(onBand, offBand * 0.6, offBand * 1.6);
    }

    [Fact]
    public void Collect_WithClustering_StillMatchesAcrossSliceBoundaries()
    {
        // Rejection happens on a candidate's own position, so slicing the image must not change which
        // candidates survive. This is the band-independence guarantee applied to clustering.
        var density = new StarDensityField(new StarClusteringOptions(), Size, 3UL, seamlessX: true);
        var field = new StarCellField(Layer, Size, 3UL, 0, seamlessX: true, seamlessY: false, density);

        var whole = new List<Star>();
        field.Collect(0, Size.Height - 1, whole);

        var sliced = new HashSet<Star>();
        for (var top = 0; top < Size.Height; top += 23)
        {
            var slice = new List<Star>();
            field.Collect(top, Math.Min(top + 22, Size.Height - 1), slice);
            sliced.UnionWith(slice);
        }

        Assert.NotEmpty(whole);
        Assert.Equal([.. whole], sliced);
    }

    [Fact]
    public void Collect_WithClustering_IsRepeatable()
    {
        var density = new StarDensityField(new StarClusteringOptions(), Size, 5UL, seamlessX: true);

        var first = new List<Star>();
        new StarCellField(Layer, Size, 5UL, 0, seamlessX: true, seamlessY: false, density).Collect(0, Size.Height - 1, first);

        var second = new List<Star>();
        new StarCellField(Layer, Size, 5UL, 0, seamlessX: true, seamlessY: false, density).Collect(0, Size.Height - 1, second);

        Assert.NotEmpty(first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Haze_IsBrightestOnTheBandAndAbsentFromTheEdges()
    {
        var options = Banded with { HazeIntensity = 1.0f, HazeContrast = 1.0f };
        var density = new StarDensityField(options, Size, 1UL, seamlessX: true);
        var haze = new StarHazeLayerRenderer(options, density);

        var buffer = new RgbBandBuffer(Size.Width, Size.Height);
        buffer.Clear();
        haze.RenderBand(new BandRegion(Size, 0, Size.Height), buffer, CancellationToken.None);

        var onBand = buffer.Get(600, 400).Luminance;
        var offBand = buffer.Get(600, 5).Luminance;

        Assert.True(onBand > 0.0f, "The haze should light the band.");
        Assert.True(onBand > offBand * 10.0f, $"Band haze {onBand} was not clearly brighter than {offBand}.");
    }

    [Fact]
    public void Haze_RendersNothingWhenTurnedOff()
    {
        foreach (var options in new[]
        {
            Banded with { HazeIntensity = 0.0f },
            Banded with { Enabled = false, HazeIntensity = 1.0f },
        })
        {
            var density = new StarDensityField(options, Size, 1UL, seamlessX: true);
            var buffer = new RgbBandBuffer(Size.Width, 4);
            buffer.Clear();

            new StarHazeLayerRenderer(options, density)
                .RenderBand(new BandRegion(Size, 400, 4), buffer, CancellationToken.None);

            Assert.Equal(0.0f, buffer.Get(600, 2).Luminance);
        }
    }

    [Fact]
    public void Haze_RejectsMissingArguments()
    {
        var density = new StarDensityField(Banded, Size, 1UL, seamlessX: true);

        Assert.Throws<ArgumentNullException>(() => new StarHazeLayerRenderer(null!, density));
        Assert.Throws<ArgumentNullException>(() => new StarHazeLayerRenderer(Banded, null!));
    }

    private static int CountBetween(StarCellField field, int firstRow, int lastRow)
    {
        var stars = new List<Star>();
        field.Collect(firstRow, lastRow, stars);
        return stars.Count(star => star.Y >= firstRow && star.Y <= lastRow);
    }
}
