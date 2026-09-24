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
