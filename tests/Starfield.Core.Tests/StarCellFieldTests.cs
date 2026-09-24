using Imaging.Core.Rendering;
using Starfield.Core.Options;
using Starfield.Core.Rendering;

namespace Starfield.Core.Tests;

/// <summary>
/// Checks that stars are a pure function of their cell, which is what lets a band repopulate only the
/// part of the sky it needs.
/// </summary>
public sealed class StarCellFieldTests
{
    private static readonly ImageSize Size = new(320, 180);

    [Fact]
    public void Collect_IsRepeatableForTheSameSeed()
    {
        var first = Collect(Field(), 0, Size.Height - 1);
        var second = Collect(Field(), 0, Size.Height - 1);

        Assert.NotEmpty(first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Collect_DiffersBetweenSeedsAndBetweenLayerPositions()
    {
        var baseline = Collect(Field(seed: 1), 0, Size.Height - 1);
        var otherSeed = Collect(Field(seed: 2), 0, Size.Height - 1);
        var otherIndex = Collect(Field(seed: 1, layerIndex: 1), 0, Size.Height - 1);

        Assert.NotEqual(baseline, otherSeed);
        Assert.NotEqual(baseline, otherIndex);
    }

    [Fact]
    public void Collect_OverSlicesMatchesCollectOverTheWholeImage()
    {
        // This is the invariant banded rendering depends on. A star that straddles a slice boundary is
        // reported by both slices, so the slices are compared as a set rather than as a sequence.
        HashSet<Star> whole = [.. Collect(Field(), 0, Size.Height - 1)];

        HashSet<Star> sliced = [];
        for (var top = 0; top < Size.Height; top += 17)
        {
            sliced.UnionWith(Collect(Field(), top, Math.Min(top + 16, Size.Height - 1)));
        }

        Assert.Equal(whole, sliced);
    }

    [Fact]
    public void Collect_IncludesStarsWhoseCentreSitsOutsideTheImage()
    {
        var topBand = Collect(Field(), 0, 3);

        Assert.Contains(topBand, star => star.Y < 0.0f);
    }

    [Fact]
    public void Collect_WhenSeamless_AddsWrappedCopiesAtTheEdges()
    {
        var seamless = Collect(Field(seamlessX: true), 0, Size.Height - 1);
        var plain = Collect(Field(seamlessX: false), 0, Size.Height - 1);

        Assert.True(seamless.Count > plain.Count, "Seamless collection should add wrapped copies.");
        Assert.Contains(seamless, star => star.X < 0.0f || star.X > Size.Width);
        Assert.DoesNotContain(plain, star => star.X < 0.0f || star.X > Size.Width);
    }

    [Fact]
    public void Collect_WithZeroDensity_ProducesNothing()
    {
        var field = Field(density: 0.0f);

        Assert.Empty(Collect(field, 0, Size.Height - 1));
    }

    [Fact]
    public void Collect_WithAnInvertedRowRange_ProducesNothing() => Assert.Empty(Collect(Field(), 50, 10));

    [Fact]
    public void MaxInfluenceRadius_CoversEveryStarItCanProduce()
    {
        // The margin must bound the largest star the layer can actually make, jitter included, or a band
        // would skip the cell holding that star and lose the glow reaching into it.
        var layer = Layer() with
        {
            DensityPerMegapixel = 3000.0f,
            MaxRadius = 2.0f,
            GlowRadiusScale = 5.0f,
            Spikes = new SpikeOptions { Enabled = true, LengthScale = 3.0f, BrightnessThreshold = 0.0f },
        };

        var field = new StarCellField(layer, Size, 1UL, 0, seamlessX: false);
        var stars = Collect(field, 0, Size.Height - 1);

        Assert.NotEmpty(stars);
        Assert.All(stars, star => Assert.True(
            star.InfluenceRadius <= field.MaxInfluenceRadius,
            $"A star reached {star.InfluenceRadius} beyond the {field.MaxInfluenceRadius} margin."));
    }

    [Fact]
    public void Collect_SpikesOnlyTheBrightestStars()
    {
        var layer = Layer() with
        {
            DensityPerMegapixel = 4000.0f,
            Spikes = new SpikeOptions { Enabled = true, BrightnessThreshold = 0.9f },
        };

        var stars = Collect(new StarCellField(layer, Size, 1UL, 0, seamlessX: false), 0, Size.Height - 1);
        List<Star> spiked = [.. stars.Where(star => star.HasSpikes)];

        Assert.NotEmpty(spiked);
        Assert.True(spiked.Count < stars.Count / 2, "Only the top of the brightness range should be spiked.");
    }

    [Fact]
    public void Constructor_WithoutALayer_Throws()
        => Assert.Throws<ArgumentNullException>(() => new StarCellField(null!, Size, 1UL, 0, seamlessX: true));

    private static StarLayerOptions Layer() => new()
    {
        Name = "test",
        DensityPerMegapixel = 800.0f,
        GlowRadiusScale = 8.0f,
        MaxRadius = 1.5f,
    };

    private static StarCellField Field(
        float density = 800.0f,
        ulong seed = 1UL,
        int layerIndex = 0,
        bool seamlessX = true)
        => new(Layer() with { DensityPerMegapixel = density }, Size, seed, layerIndex, seamlessX);

    private static List<Star> Collect(StarCellField field, int firstRow, int lastRow)
    {
        var stars = new List<Star>();
        field.Collect(firstRow, lastRow, stars);
        return stars;
    }
}
