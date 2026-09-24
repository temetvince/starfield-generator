using Imaging.Core.Noise;

namespace Imaging.Core.Tests;

/// <summary>Checks that the noise fields are reproducible and that seamless really means seamless.</summary>
public sealed class NoiseTests
{
    [Fact]
    public void PerlinNoise_RepeatsExactlyAcrossItsPeriod()
    {
        var noise = new TilingPerlinNoise(17UL);
        const int Period = 8;

        for (var y = -2.0f; y < 2.0f; y += 0.37f)
        {
            for (var x = 0.0f; x < Period; x += 0.31f)
            {
                // A tolerance rather than a decimal count: the two samples take different float paths to
                // the same lattice position, so they agree to within rounding, not to the last bit.
                Assert.Equal(noise.Sample(x, y, Period), noise.Sample(x + Period, y, Period), 1e-5f);
            }
        }
    }

    [Fact]
    public void PerlinNoise_WithoutAPeriod_DoesNotRepeat()
    {
        var noise = new TilingPerlinNoise(17UL);

        Assert.NotEqual(noise.Sample(1.5f, 0.5f, 0), noise.Sample(9.5f, 0.5f, 0));
    }

    [Fact]
    public void PerlinNoise_StaysWithinItsNominalRange()
    {
        var noise = new TilingPerlinNoise(3UL);

        for (var y = 0.0f; y < 20.0f; y += 0.13f)
        {
            for (var x = 0.0f; x < 20.0f; x += 0.11f)
            {
                Assert.InRange(noise.Sample(x, y, 0), -1.05f, 1.05f);
            }
        }
    }

    [Fact]
    public void PerlinNoise_IsZeroOnLatticePoints()
    {
        // Gradient noise crosses zero at every lattice point; a non-zero value there means the
        // interpolation weights are wrong.
        var noise = new TilingPerlinNoise(23UL);

        Assert.Equal(0.0f, noise.Sample(3.0f, 5.0f, 0), 5);
        Assert.Equal(0.0f, noise.Sample(-2.0f, 7.0f, 0), 5);
    }

    [Theory]
    [InlineData(FractalNoiseShape.Brownian)]
    [InlineData(FractalNoiseShape.Ridged)]
    [InlineData(FractalNoiseShape.Billow)]
    public void FractalNoise_StaysInTheUnitInterval(FractalNoiseShape shape)
    {
        var noise = new FractalNoise(5UL, new FractalNoiseOptions { Shape = shape, Octaves = 6 });

        for (var y = 0.0f; y < 0.5f; y += 0.011f)
        {
            for (var x = 0.0f; x < 1.0f; x += 0.013f)
            {
                Assert.InRange(noise.Sample(x, y), 0.0f, 1.0f);
            }
        }
    }

    [Fact]
    public void FractalNoise_WhenSeamless_MatchesAcrossTheImageEdge()
    {
        var noise = new FractalNoise(9UL, new FractalNoiseOptions { SeamlessX = true, Octaves = 6 });

        for (var y = 0.0f; y < 0.3f; y += 0.017f)
        {
            Assert.Equal(noise.Sample(0.0f, y), noise.Sample(1.0f, y), 1e-4f);
        }
    }

    [Fact]
    public void FractalNoise_WhenNotSeamless_DiffersAcrossTheImageEdge()
    {
        var noise = new FractalNoise(9UL, new FractalNoiseOptions { SeamlessX = false, Octaves = 6 });

        var difference = 0.0f;
        for (var y = 0.0f; y < 0.3f; y += 0.017f)
        {
            difference += MathF.Abs(noise.Sample(0.0f, y) - noise.Sample(1.0f, y));
        }

        Assert.True(difference > 0.1f, "A non-seamless field should not line up across the edge.");
    }

    [Fact]
    public void FractalNoise_IsReproducibleForTheSameSeed()
    {
        var options = new FractalNoiseOptions { Octaves = 4 };
        var first = new FractalNoise(31UL, options);
        var second = new FractalNoise(31UL, options);
        var other = new FractalNoise(32UL, options);

        Assert.Equal(first.Sample(0.3f, 0.2f), second.Sample(0.3f, 0.2f));
        Assert.NotEqual(first.Sample(0.3f, 0.2f), other.Sample(0.3f, 0.2f));
    }

    [Fact]
    public void FractalNoise_ReportsTheOctaveCountItWasBuiltWith()
    {
        var noise = new FractalNoise(1UL, new FractalNoiseOptions { Octaves = 7 });

        Assert.Equal(7, noise.OctaveCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    public void FractalNoise_WithAnImpossibleOctaveCount_Throws(int octaves)
    {
        var options = new FractalNoiseOptions { Octaves = octaves };

        Assert.Throws<ArgumentException>(() => new FractalNoise(1UL, options));
    }

    [Fact]
    public void FractalNoiseOptions_ReportEveryProblemAtOnce()
    {
        var options = new FractalNoiseOptions
        {
            Octaves = 0,
            BaseFrequency = 0.0f,
            Lacunarity = 0.5f,
            Gain = 2.0f,
        };

        Assert.Equal(4, options.Validate().Count);
    }

    [Fact]
    public void FractalNoiseOptions_DefaultsAreValid() => Assert.Empty(new FractalNoiseOptions().Validate());
}
