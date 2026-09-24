using Imaging.Core.Colors;
using Imaging.TestSupport;
using Starfield.Core.Options;
using Starfield.Core.Rendering;

namespace Starfield.Core.Tests;

/// <summary>Checks the parts of the nebula renderer that depend on the seed beyond the noise fields.</summary>
public sealed class NebulaLayerRendererTests
{
    private static readonly NebulaPaletteOptions Blue = Solid("blue", "#0000FF");
    private static readonly NebulaPaletteOptions Red = Solid("red", "#FF0000");

    /// <summary>A field of clouds alone, rendered so that any light in a channel is unambiguous.</summary>
    private static StarfieldOptions CloudsOnly => StarfieldPresets.Default() with
    {
        Width = 160,
        Height = 96,
        Background = "#000000",
        ToneMapping = ToneMappingCurve.Clamp,
        DitherStrength = 0.0f,
        StarLayers = [],
        Clustering = StarfieldPresets.Default().Clustering with { Enabled = false },
        Nebula = StarfieldPresets.Default().Nebula with
        {
            CoverageThreshold = 0.0f,
            HueVariation = 0.0f,
            Palettes = [Blue],
        },
    };

    [Fact]
    public void PaletteName_DependsOnTheSeed()
    {
        var options = new NebulaOptions();

        var distinct = Enumerable.Range(1, 32)
            .Select(seed => new NebulaLayerRenderer(options, (ulong)seed, seamlessX: true).PaletteName)
            .Distinct()
            .Count();

        Assert.True(distinct > 1, "Every seed chose the same palette.");
    }

    [Fact]
    public void PaletteAndHueShift_AreReproducibleForOneSeed()
    {
        var options = new NebulaOptions();

        var first = new NebulaLayerRenderer(options, 99UL, seamlessX: true);
        var second = new NebulaLayerRenderer(options, 99UL, seamlessX: false);

        Assert.Equal(first.PaletteName, second.PaletteName);
        Assert.Equal(first.HueShift, second.HueShift);
    }

    [Theory]
    [InlineData(1.0f)]
    [InlineData(0.25f)]
    public void HueShift_StaysWithinHalfTheVariationEitherWay(float variation)
    {
        var options = new NebulaOptions { HueVariation = variation };

        for (var seed = 1UL; seed <= 64UL; seed++)
        {
            Assert.InRange(new NebulaLayerRenderer(options, seed, seamlessX: true).HueShift, -variation / 2.0f, variation / 2.0f);
        }
    }

    [Fact]
    public void HueShift_IsZeroWhenVariationIsDisabled()
    {
        var options = new NebulaOptions { HueVariation = 0.0f };

        for (var seed = 1UL; seed <= 16UL; seed++)
        {
            Assert.Equal(0.0f, new NebulaLayerRenderer(options, seed, seamlessX: true).HueShift);
        }
    }

    [Fact]
    public void Render_WithOnePaletteAndNoVariation_UsesThatRampForEverySeed()
    {
        foreach (var seed in new[] { 1UL, 2UL, 3UL })
        {
            var image = Render(CloudsOnly with { Seed = seed });

            Assert.True(HasAnyLight(image, channel: 2), $"Seed {seed} drew no cloud.");
            Assert.False(HasAnyLight(image, channel: 0), $"Seed {seed} leaked red into a blue ramp.");
            Assert.False(HasAnyLight(image, channel: 1), $"Seed {seed} leaked green into a blue ramp.");
        }
    }

    [Fact]
    public void Render_WithSeveralPalettes_PaintsEachSeedWithTheOneItChose()
    {
        var options = CloudsOnly with { Nebula = CloudsOnly.Nebula with { Palettes = [Blue, Red] } };

        var redImage = Render(options with { Seed = FirstSeedChoosing(options.Nebula, "red") });
        var blueImage = Render(options with { Seed = FirstSeedChoosing(options.Nebula, "blue") });

        Assert.True(HasAnyLight(redImage, channel: 0) && !HasAnyLight(redImage, channel: 2), "The red seed did not paint red.");
        Assert.True(HasAnyLight(blueImage, channel: 2) && !HasAnyLight(blueImage, channel: 0), "The blue seed did not paint blue.");
    }

    [Fact]
    public void Render_WithVariation_NudgesTheChosenPalette()
    {
        var options = CloudsOnly with { Nebula = CloudsOnly.Nebula with { HueVariation = 0.5f } };

        // Pick a seed the hash sends well away from the authored hue, so the test depends on the hash
        // staying deterministic rather than on luck.
        var seed = Enumerable.Range(1, 64)
            .Select(candidate => (ulong)candidate)
            .First(candidate => MathF.Abs(new NebulaLayerRenderer(options.Nebula, candidate, seamlessX: true).HueShift) > 0.15f);

        var image = Render(options with { Seed = seed });

        Assert.True(HasAnyLight(image, channel: 0) || HasAnyLight(image, channel: 1), "The ramp stayed pure blue.");
    }

    private static NebulaPaletteOptions Solid(string name, string colour) => new()
    {
        Name = name,
        ColorStops = [new ColorStopOptions { Position = 0.0f, Color = colour }],
    };

    private static ulong FirstSeedChoosing(NebulaOptions options, string palette)
        => Enumerable.Range(1, 64)
            .Select(candidate => (ulong)candidate)
            .First(candidate => new NebulaLayerRenderer(options, candidate, seamlessX: true).PaletteName == palette);

    private static bool HasAnyLight(DecodedPng image, int channel)
    {
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                if (image.GetChannel(x, y, channel) > 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static DecodedPng Render(StarfieldOptions options)
    {
        using var stream = new MemoryStream();
        new StarfieldRenderer(options).RenderTo(stream);
        return DecodedPng.Decode(stream.ToArray());
    }
}
