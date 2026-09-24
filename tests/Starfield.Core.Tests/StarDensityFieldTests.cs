using Imaging.Core.Rendering;
using Starfield.Core.Options;
using Starfield.Core.Rendering;

namespace Starfield.Core.Tests;

/// <summary>
/// Checks the clustering field: the galactic band, the clouds within it and the dust lanes across it.
/// </summary>
public sealed class StarDensityFieldTests
{
    private static readonly ImageSize Size = new(2000, 1000);

    [Fact]
    public void Sample_PeaksOnTheBandAndFadesAwayFromIt()
    {
        var field = Field(new StarClusteringOptions
        {
            Floor = 0.2f,
            BandStrength = 2.0f,
            BandCentre = 0.5f,
            BandWidth = 0.15f,
            BandWobble = 0.0f,
            ClumpStrength = 0.0f,
            DustStrength = 0.0f,
        });

        var onBand = field.Sample(1000.0f, 500.0f);
        var offBand = field.Sample(1000.0f, 20.0f);

        Assert.True(onBand > offBand * 3.0f, $"Band {onBand} was not clearly denser than the edge {offBand}.");
        Assert.Equal(0.2f, offBand, 0.05f);
    }

    [Fact]
    public void Sample_NeverExceedsTheCeilingItAdvertises()
    {
        // Star placement samples against this ceiling, so an underestimate would silently clip the
        // densest regions rather than failing outright.
        var field = Field(new StarClusteringOptions());

        for (var y = -200; y < Size.Height + 200; y += 7)
        {
            for (var x = 0; x < Size.Width; x += 13)
            {
                var value = field.Sample(x, y);

                Assert.InRange(value, 0.0f, field.MaximumMultiplier);
            }
        }
    }

    [Fact]
    public void SampleBand_IsOneOnTheCentreLineAndFallsAwayMonotonically()
    {
        var field = Field(new StarClusteringOptions { BandCentre = 0.5f, BandWidth = 0.2f, BandWobble = 0.0f });

        Assert.Equal(1.0f, field.SampleBand(500.0f, 500.0f), 0.001f);

        var previous = 1.0f;
        for (var y = 500; y < Size.Height; y += 25)
        {
            var value = field.SampleBand(500.0f, y);

            Assert.True(value <= previous + 1e-4f, $"The band rose again at y={y}.");
            previous = value;
        }

        Assert.True(previous < 0.05f, "The band should have faded to nothing by the bottom edge.");
    }

    [Fact]
    public void Sample_WhenDisabled_IsUniform()
    {
        var field = Field(new StarClusteringOptions { Enabled = false });

        Assert.Equal(1.0f, field.Sample(10.0f, 10.0f));
        Assert.Equal(1.0f, field.Sample(1900.0f, 900.0f));
        Assert.Equal(1.0f, field.MaximumMultiplier);
    }

    [Fact]
    public void Sample_WithDust_DarkensSomeOfTheBand()
    {
        var clear = new StarClusteringOptions { BandWobble = 0.0f, DustStrength = 0.0f };
        var dusty = clear with { DustStrength = 0.9f };

        var clearField = Field(clear);
        var dustyField = Field(dusty);

        var dimmed = 0;
        var brightened = 0;

        for (var x = 0; x < Size.Width; x += 5)
        {
            var withoutDust = clearField.Sample(x, Size.Height * 0.45f);
            var withDust = dustyField.Sample(x, Size.Height * 0.45f);

            if (withDust < withoutDust - 0.01f)
            {
                dimmed++;
            }
            else if (withDust > withoutDust + 0.01f)
            {
                brightened++;
            }
        }

        Assert.True(dimmed > 20, $"Dust dimmed only {dimmed} samples along the band.");
        Assert.Equal(0, brightened);
    }

    [Fact]
    public void Sample_WhenSeamless_MatchesAcrossTheImageEdge()
    {
        var field = Field(new StarClusteringOptions(), seamlessX: true);

        for (var y = 0; y < Size.Height; y += 37)
        {
            Assert.Equal(field.Sample(0.0f, y), field.Sample(Size.Width, y), 1e-3f);
        }
    }

    [Fact]
    public void Sample_IsRepeatableForTheSameSeed()
    {
        var options = new StarClusteringOptions();

        Assert.Equal(Field(options).Sample(123.0f, 456.0f), Field(options).Sample(123.0f, 456.0f));
        Assert.NotEqual(Field(options).Sample(123.0f, 456.0f), Field(options, seed: 2UL).Sample(123.0f, 456.0f));
    }

    [Fact]
    public void Constructor_WithoutOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new StarDensityField(null!, Size, 1UL, true));

    private static StarDensityField Field(StarClusteringOptions options, ulong seed = 1UL, bool seamlessX = true)
        => new(options, Size, seed, seamlessX);
}
