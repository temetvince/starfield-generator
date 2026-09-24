using Starfield.App.ViewModels;
using Starfield.Core.Options;

namespace Starfield.App.Tests;

/// <summary>Checks that the form turns into the options the renderer expects.</summary>
public sealed class RenderRequestViewModelTests
{
    [Fact]
    public void Build_WithABlankForm_RendersFourKWithTheBuiltInPreset()
    {
        var form = new RenderRequestViewModel();

        var built = form.Build();

        var preset = StarfieldPresets.Default();
        Assert.True(built.IsValid);
        Assert.Equal("starfield.png", built.OutputPath);
        Assert.Equal(3840, built.Options!.Width);
        Assert.Equal(2160, built.Options.Height);
        Assert.Equal(preset.Seed, built.Options.Seed);
        Assert.Equal(preset.StarLayers.Count, built.Options.StarLayers.Count);
    }

    [Fact]
    public void Build_WithEveryFieldSet_OverridesSizeAndSeed()
    {
        var form = new RenderRequestViewModel
        {
            OutputPath = " sky.png ",
            Width = "4096",
            Height = "1024",
            Seed = "42",
        };

        var built = form.Build();

        Assert.True(built.IsValid);
        Assert.Equal("sky.png", built.OutputPath);
        Assert.Equal(4096, built.Options!.Width);
        Assert.Equal(1024, built.Options.Height);
        Assert.Equal(42UL, built.Options.Seed);
        Assert.Equal(StarfieldPresets.Default().Nebula, built.Options.Nebula);
    }

    [Fact]
    public void Build_ReportsEveryProblem()
    {
        var form = new RenderRequestViewModel
        {
            OutputPath = "  ",
            Width = "wide",
            Height = "0",
            Seed = "-1",
        };

        var built = form.Build();

        Assert.False(built.IsValid);
        Assert.Equal(4, built.Problems.Length);
        Assert.Contains("Save as must not be empty.", built.Problems);
        Assert.Contains("Width must be a whole number, but was 'wide'.", built.Problems);
        Assert.Contains("Height must be at least 1, but was '0'.", built.Problems);
        Assert.Contains("Seed must be a whole number from 0 upwards, but was '-1'.", built.Problems);
    }

    [Fact]
    public void Build_WithCustomColours_PinsOneRimToCorePaletteAndNoHueNudge()
    {
        var form = new RenderRequestViewModel
        {
            UseCustomColours = true,
            RimColour = "#102040",
            CoreColour = "#FFF0E0",
        };

        var built = form.Build();

        Assert.True(built.IsValid);
        var nebula = built.Options!.Nebula;
        Assert.Equal(0.0f, nebula.HueVariation);
        var palette = Assert.Single(nebula.Palettes);
        Assert.Equal("#102040", palette.ColorStops[0].Color);
        Assert.Equal("#FFF0E0", palette.ColorStops[^1].Color);
        Assert.Empty(nebula.Validate());
    }

    [Fact]
    public void Build_WithCustomColoursOff_IgnoresTheColoursEvenWhenInvalid()
    {
        var form = new RenderRequestViewModel { RimColour = "not a colour" };

        var built = form.Build();

        Assert.True(built.IsValid);
        Assert.Equal(StarfieldPresets.Default().Nebula.Palettes.Count, built.Options!.Nebula.Palettes.Count);
        Assert.Empty(form.RampStops);
    }

    [Fact]
    public void Build_WithCustomColoursOn_ReportsInvalidHex()
    {
        var form = new RenderRequestViewModel { UseCustomColours = true, CoreColour = "orange" };

        var built = form.Build();

        Assert.False(built.IsValid);
        Assert.Contains("Core colour must be a hex colour such as #7A2A18, but was 'orange'.", built.Problems);
    }

    [Fact]
    public void RampStops_FollowTheColoursForPreviewing()
    {
        var form = new RenderRequestViewModel();
        Assert.Equal(5, form.RampStops.Length);
        Assert.Equal(RenderRequestViewModel.DefaultRimColour, form.RampStops[0]);

        form.CoreColour = "#00FF00";

        Assert.Equal("#00FF00", form.RampStops[^1]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Build_PassesTheVerticalTilingChoiceThrough(bool tile)
    {
        var form = new RenderRequestViewModel { TileVertically = tile };

        var built = form.Build();

        Assert.True(built.IsValid);
        Assert.Equal(tile, built.Options!.SeamlessY);
        Assert.True(built.Options.SeamlessX);
    }

    [Fact]
    public void RandomiseSeed_ProducesAParsableSeedThatChanges()
    {
        var form = new RenderRequestViewModel();

        form.RandomiseSeed();
        var first = form.Seed;
        form.RandomiseSeed();

        Assert.True(form.Build().IsValid);
        Assert.NotEqual(first, form.Seed);
    }

    [Fact]
    public void Defaults_ComeFromTheBuiltInPreset()
    {
        var defaults = RequestDefaults.FromBuiltIn();

        Assert.Equal("starfield.png", defaults.OutputPath);
        Assert.Equal("3840", defaults.Width);
        Assert.Equal("2160", defaults.Height);
    }
}
