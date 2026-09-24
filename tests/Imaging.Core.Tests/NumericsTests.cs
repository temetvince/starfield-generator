using Imaging.Core.Numerics;

namespace Imaging.Core.Tests;

/// <summary>Checks the reproducibility guarantees the whole renderer is built on.</summary>
public sealed class NumericsTests
{
    [Fact]
    public void Hash64_IsStableForTheSameInputs()
    {
        Assert.Equal(Hash64.Mix(12345UL), Hash64.Mix(12345UL));
        Assert.Equal(Hash64.Combine(7UL, 3, 9), Hash64.Combine(7UL, 3, 9));
        Assert.Equal(Hash64.Combine(7UL, 1, 2, 3), Hash64.Combine(7UL, 1, 2, 3));
    }

    [Fact]
    public void Hash64_SeparatesNeighbouringCoordinates()
    {
        Assert.NotEqual(Hash64.Combine(1UL, 0, 0), Hash64.Combine(1UL, 0, 1));
        Assert.NotEqual(Hash64.Combine(1UL, 0, 0), Hash64.Combine(1UL, 1, 0));
        Assert.NotEqual(Hash64.Combine(1UL, 0, 0), Hash64.Combine(2UL, 0, 0));
        Assert.NotEqual(Hash64.Combine(1UL, 3, 4), Hash64.Combine(1UL, 4, 3));
    }

    [Fact]
    public void Hash64_HandlesNegativeCoordinates()
    {
        Assert.NotEqual(Hash64.Combine(1UL, -1, 0), Hash64.Combine(1UL, 1, 0));
        Assert.Equal(Hash64.Combine(1UL, -5, -7), Hash64.Combine(1UL, -5, -7));
    }

    [Fact]
    public void DeterministicRandom_RepeatsForTheSameSeed()
    {
        var first = new DeterministicRandom(99UL);
        var second = new DeterministicRandom(99UL);

        for (var draw = 0; draw < 64; draw++)
        {
            Assert.Equal(first.NextBits(), second.NextBits());
        }
    }

    [Fact]
    public void DeterministicRandom_DivergesForDifferentSeeds()
    {
        var first = new DeterministicRandom(1UL);
        var second = new DeterministicRandom(2UL);

        Assert.NotEqual(first.NextBits(), second.NextBits());
    }

    [Fact]
    public void NextUnit_StaysInTheHalfOpenUnitInterval()
    {
        var random = new DeterministicRandom(4242UL);

        for (var draw = 0; draw < 10000; draw++)
        {
            var value = random.NextUnit();
            Assert.InRange(value, 0.0f, 0.9999999f);
        }
    }

    [Fact]
    public void NextRange_StaysWithinItsBounds()
    {
        var random = new DeterministicRandom(11UL);

        for (var draw = 0; draw < 1000; draw++)
        {
            Assert.InRange(random.NextRange(-3.0f, 5.0f), -3.0f, 5.0f);
        }
    }

    [Fact]
    public void NextBiased_CrowdsTowardTheMinimumForLargeExponents()
    {
        var random = new DeterministicRandom(77UL);
        var belowMidpoint = 0;

        for (var draw = 0; draw < 2000; draw++)
        {
            var value = random.NextBiased(0.0f, 1.0f, 3.0f);
            Assert.InRange(value, 0.0f, 1.0f);

            if (value < 0.5f)
            {
                belowMidpoint++;
            }
        }

        // A cubic bias puts everything below the cube root of a half, so about 79% of draws.
        Assert.InRange(belowMidpoint, 1500, 1700);
    }

    [Fact]
    public void NextBiased_WithNonPositiveExponent_Throws()
    {
        var random = new DeterministicRandom(1UL);

        Assert.Throws<ArgumentOutOfRangeException>(() => random.NextBiased(0.0f, 1.0f, 0.0f));
    }

    [Fact]
    public void DeterministicRandom_ComparesByStreamPosition()
    {
        var first = new DeterministicRandom(5UL);
        var second = new DeterministicRandom(5UL);

        Assert.True(first == second);

        first.NextBits();

        Assert.True(first != second);
        Assert.False(first.Equals(second));
    }

    [Fact]
    public void Smoothstep_IsFlatOutsideItsEdgesAndSmoothBetween()
    {
        Assert.Equal(0.0f, Interpolation.Smoothstep(0.2f, 0.8f, 0.1f));
        Assert.Equal(1.0f, Interpolation.Smoothstep(0.2f, 0.8f, 0.9f));
        Assert.Equal(0.5f, Interpolation.Smoothstep(0.2f, 0.8f, 0.5f), 5);
        Assert.InRange(Interpolation.Smoothstep(0.0f, 1.0f, 0.25f), 0.0f, 0.25f);
    }

    [Fact]
    public void Smoothstep_WithCollapsedEdges_BecomesAHardStep()
    {
        Assert.Equal(0.0f, Interpolation.Smoothstep(0.5f, 0.5f, 0.4f));
        Assert.Equal(1.0f, Interpolation.Smoothstep(0.5f, 0.5f, 0.6f));
    }

    [Fact]
    public void Lerp_HitsBothEnds()
    {
        Assert.Equal(2.0f, Interpolation.Lerp(2.0f, 6.0f, 0.0f));
        Assert.Equal(6.0f, Interpolation.Lerp(2.0f, 6.0f, 1.0f));
        Assert.Equal(4.0f, Interpolation.Lerp(2.0f, 6.0f, 0.5f));
    }
}
