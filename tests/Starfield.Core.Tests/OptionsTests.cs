using System.IO.Compression;
using Imaging.Core.Colors;
using Imaging.Core.Rendering;
using Starfield.Core.Options;

namespace Starfield.Core.Tests;

/// <summary>Checks that bad configuration is reported clearly instead of rendering something odd.</summary>
public sealed class OptionsTests
{
    [Fact]
    public void DefaultPreset_IsValid()
    {
        var options = StarfieldPresets.Default();

        Assert.Empty(options.Validate());
        Assert.Equal(3, options.StarLayers.Count);
        Assert.True(options.Nebula.Enabled);
    }

    [Fact]
    public void DefaultPreset_IsBuiltFreshEachTime()
    {
        var first = StarfieldPresets.Default();
        var second = StarfieldPresets.Default();

        Assert.NotSame(first, second);
        Assert.Equal(first.Seed, second.Seed);
        Assert.Equal(first.StarLayers.Count, second.StarLayers.Count);
        Assert.Equal(first.StarLayers[0], second.StarLayers[0]);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(100, 0)]
    [InlineData(-5, 100)]
    public void Validate_RejectsImpossibleDimensions(int width, int height)
    {
        var options = StarfieldPresets.Default() with { Width = width, Height = height };

        Assert.NotEmpty(options.Validate());
    }

    [Fact]
    public void Validate_RejectsAMalformedBackground()
    {
        var problems = (StarfieldPresets.Default() with { Background = "not-a-colour" }).Validate();

        Assert.Contains(problems, problem => problem.Contains("Background", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RejectsDuplicateLayerNames()
    {
        var options = StarfieldPresets.Default() with
        {
            StarLayers =
            [
                new StarLayerOptions { Name = "twin" },
                new StarLayerOptions { Name = "TWIN" },
            ],
        };

        Assert.Contains(options.Validate(), problem => problem.Contains("more than once", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ReportsProblemsFromNestedOptions()
    {
        var options = StarfieldPresets.Default() with
        {
            Nebula = new NebulaOptions { DensityContrast = -1.0f, Palettes = [] },
            StarLayers = [new StarLayerOptions { MinRadius = 0.0f, MaxIntensity = -3.0f }],
        };

        var problems = options.Validate();

        Assert.Contains(problems, problem => problem.Contains("DensityContrast", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.Contains("Palettes", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.Contains("MinRadius", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.Contains("MaxIntensity", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RejectsAnInvertedTemperatureRange()
    {
        var layer = new StarLayerOptions { MinTemperatureKelvin = 9000.0f, MaxTemperatureKelvin = 4000.0f };

        Assert.Contains(layer.Validate(), problem => problem.Contains("MaxTemperatureKelvin", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RejectsSpikesWithTooManyArms()
    {
        var spikes = new SpikeOptions { Enabled = true, Arms = 9 };

        Assert.Contains(spikes.Validate(), problem => problem.Contains("Arms", StringComparison.Ordinal));
    }

    [Fact]
    public void ToRenderOptions_CarriesEverySharedSetting()
    {
        var options = StarfieldPresets.Default() with
        {
            BandHeight = 32,
            MaxDegreeOfParallelism = 3,
            MaxWorkingSetBytes = 1234,
            CompressionLevel = CompressionLevel.Fastest,
            Exposure = 1.5f,
            DitherStrength = 0.25f,
            Seed = 77UL,
        };

        var rendering = options.ToRenderOptions(AlphaMode.Opaque);

        Assert.Equal(32, rendering.BandHeight);
        Assert.Equal(3, rendering.MaxDegreeOfParallelism);
        Assert.Equal(1234, rendering.MaxWorkingSetBytes);
        Assert.Equal(CompressionLevel.Fastest, rendering.CompressionLevel);
        Assert.Equal(1.5f, rendering.Encoding.Exposure);
        Assert.Equal(0.25f, rendering.Encoding.DitherStrength);
        Assert.Equal(77UL, rendering.Encoding.DitherSeed);
        Assert.Empty(rendering.Validate());
    }

    [Fact]
    public void ToRenderOptions_DropsTheBackgroundForTransparentLayers()
    {
        var options = StarfieldPresets.Default() with { Background = "#FF0000" };

        Assert.Equal(AlphaMode.FromLight, options.ToRenderOptions(AlphaMode.FromLight).Encoding.AlphaMode);
        Assert.Equal(AlphaMode.Opaque, options.ToRenderOptions(AlphaMode.Opaque).Encoding.AlphaMode);
    }

    [Theory]
    [InlineData(-0.1f)]
    [InlineData(1.5f)]
    [InlineData(float.NaN)]
    public void NebulaOptions_RejectHueVariationOutsideTheUnitInterval(float variation)
    {
        var options = new NebulaOptions { HueVariation = variation };

        Assert.Contains(options.Validate(), problem => problem.Contains("HueVariation", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0.0f)]
    [InlineData(1.0f)]
    public void NebulaOptions_AcceptHueVariationAtEitherEndOfTheRange(float variation)
    {
        var options = new NebulaOptions { HueVariation = variation };

        Assert.Empty(options.Validate());
    }

    [Fact]
    public void NebulaPaletteOptions_ReportAnEmptyNameAndAnEmptyRamp()
    {
        var palette = new NebulaPaletteOptions { Name = " ", ColorStops = [] };

        var problems = palette.Validate();

        Assert.Contains(problems, problem => problem.Contains("Name", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.Contains("colour stop", StringComparison.Ordinal));
    }

    [Fact]
    public void NebulaPalettes_ShipOnlyValidRampsWithDistinctNames()
    {
        var shipped = NebulaPalettes.Default();

        Assert.NotEmpty(shipped);
        Assert.All(shipped, palette => Assert.Empty(palette.Validate()));
        Assert.Equal(shipped.Count, shipped.Select(palette => palette.Name).Distinct().Count());
    }

    [Fact]
    public void NebulaPalettes_Between_RunsFromRimToCoreThroughValidStops()
    {
        var rim = HexColor.Parse("#7A2A18");
        var core = HexColor.Parse("#FFE8C0");

        var palette = NebulaPalettes.Between("sunset", rim, core);

        Assert.Equal("sunset", palette.Name);
        Assert.Empty(palette.Validate());
        Assert.Equal(5, palette.ColorStops.Count);
        Assert.Equal(0.0f, palette.ColorStops[0].Position);
        Assert.Equal(1.0f, palette.ColorStops[^1].Position);
        Assert.Equal("#7A2A18", palette.ColorStops[0].Color);
        Assert.Equal("#FFE8C0", palette.ColorStops[^1].Color);

        var middle = HexColor.Parse(palette.ColorStops[2].Color);
        Assert.True(middle.Luminance > rim.Luminance && middle.Luminance < core.Luminance);
    }

    [Fact]
    public void NebulaPalettes_Between_RejectsABlankNameOrTooFewStops()
    {
        Assert.Throws<ArgumentException>(() => NebulaPalettes.Between(" ", LinearRgb.Black, LinearRgb.White));
        Assert.Throws<ArgumentOutOfRangeException>(() => NebulaPalettes.Between("x", LinearRgb.Black, LinearRgb.White, 1));
    }

    [Fact]
    public void ColorStopOptions_ConvertHexTextToLinearLight()
    {
        var stop = new ColorStopOptions { Position = 0.5f, Color = "#FFFFFF" };

        Assert.Empty(stop.Validate());
        Assert.Equal(1.0f, stop.ToGradientStop().Colour.MaxComponent, 4);
    }

    [Fact]
    public void ColorStopOptions_RejectAnOutOfRangePosition()
    {
        var stop = new ColorStopOptions { Position = 1.5f, Color = "#FFF" };

        Assert.Contains(stop.Validate(), problem => problem.Contains("Position", StringComparison.Ordinal));
    }
}
