using System.IO.Compression;
using Starfield.Cli.CommandLine;
using Starfield.Core.Options;

namespace Starfield.Cli.Tests;

/// <summary>Checks that the command line is understood, and that bad input is reported rather than guessed at.</summary>
public sealed class CliParserTests
{
    [Fact]
    public void Parse_WithNoArguments_AsksForHelp()
    {
        var parsed = CliParser.Parse([]);

        Assert.True(parsed.HelpRequested);
        Assert.Empty(parsed.Errors);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-?")]
    public void Parse_RecognisesTheHelpFlag(string flag)
        => Assert.True(CliParser.Parse([flag]).HelpRequested);

    [Fact]
    public void Parse_ReadsEveryOption()
    {
        var parsed = CliParser.Parse(
        [
            "--out", "field.png",
            "--layers", "planes",
            "--preset", "in.json",
            "--dump-preset", "out.json",
            "--width", "10000",
            "--height", "1080",
            "--seed", "12345",
            "--threads", "4",
            "--band-height", "32",
            "--memory", "256",
            "--exposure", "1.5",
            "--background", "#101820",
            "--compression", "fastest",
            "--no-seamless",
            "--no-nebula",
            "--verbose",
        ]);

        Assert.Empty(parsed.Errors);
        Assert.Equal("field.png", parsed.OutputPath);
        Assert.Equal("planes", parsed.LayersDirectory);
        Assert.Equal("in.json", parsed.PresetPath);
        Assert.Equal("out.json", parsed.DumpPresetPath);
        Assert.Equal(10000, parsed.Width);
        Assert.Equal(1080, parsed.Height);
        Assert.Equal(12345UL, parsed.Seed);
        Assert.Equal(4, parsed.Threads);
        Assert.Equal(32, parsed.BandHeight);
        Assert.Equal(256, parsed.MemoryBudgetMebibytes);
        Assert.Equal(1.5f, parsed.Exposure);
        Assert.Equal("#101820", parsed.Background);
        Assert.Equal(CompressionLevel.Fastest, parsed.Compression);
        Assert.False(parsed.SeamlessX);
        Assert.False(parsed.NebulaEnabled);
        Assert.True(parsed.Verbose);
    }

    [Fact]
    public void Parse_AcceptsShortForms()
    {
        var parsed = CliParser.Parse(["-w", "800", "-h", "600", "-s", "3", "-o", "a.png", "-q"]);

        Assert.Empty(parsed.Errors);
        Assert.Equal(800, parsed.Width);
        Assert.Equal(600, parsed.Height);
        Assert.Equal(3UL, parsed.Seed);
        Assert.Equal("a.png", parsed.OutputPath);
        Assert.True(parsed.Quiet);
    }

    [Theory]
    [InlineData("none", CompressionLevel.NoCompression)]
    [InlineData("FASTEST", CompressionLevel.Fastest)]
    [InlineData("optimal", CompressionLevel.Optimal)]
    [InlineData("smallest", CompressionLevel.SmallestSize)]
    public void Parse_UnderstandsEveryCompressionName(string text, CompressionLevel expected)
        => Assert.Equal(expected, CliParser.Parse(["--compression", text]).Compression);

    [Fact]
    public void Parse_ReportsAnUnknownOption()
    {
        var parsed = CliParser.Parse(["--sparkle"]);

        Assert.Single(parsed.Errors);
        Assert.Contains("--sparkle", parsed.Errors[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_ReportsAMissingValue()
    {
        var parsed = CliParser.Parse(["--width"]);

        Assert.Single(parsed.Errors);
        Assert.Contains("needs a value", parsed.Errors[0], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--width", "wide")]
    [InlineData("--seed", "-1")]
    [InlineData("--exposure", "bright")]
    [InlineData("--compression", "medium")]
    public void Parse_ReportsAnUnreadableValue(string option, string value)
        => Assert.Single(CliParser.Parse([option, value]).Errors);

    [Fact]
    public void Parse_ReportsEveryProblemAtOnce()
    {
        var parsed = CliParser.Parse(["--sparkle", "--width", "huge", "--glitter"]);

        Assert.Equal(3, parsed.Errors.Length);
    }

    [Fact]
    public void Parse_WithoutArguments_UsesAConventionalOutputPath()
        => Assert.Equal("starfield.png", CliParser.Parse(["-v"]).OutputPath);

    [Fact]
    public void ApplyTo_ChangesOnlyWhatWasAsked()
    {
        var baseline = StarfieldPresets.Default() with { Width = 123, Exposure = 2.0f };
        var merged = CliParser.Parse(["--width", "456"]).ApplyTo(baseline);

        Assert.Equal(456, merged.Width);
        Assert.Equal(baseline.Height, merged.Height);
        Assert.Equal(2.0f, merged.Exposure);
        Assert.Equal(baseline.Seed, merged.Seed);
        Assert.Equal(baseline.StarLayers.Count, merged.StarLayers.Count);
    }

    [Fact]
    public void ApplyTo_ConvertsTheMemoryBudgetToBytes()
    {
        var merged = CliParser.Parse(["--memory", "64"]).ApplyTo(StarfieldPresets.Default());

        Assert.Equal(64L * 1024 * 1024, merged.MaxWorkingSetBytes);
    }

    [Fact]
    public void ApplyTo_TogglesTheNebulaWithoutDisturbingItsSettings()
    {
        var baseline = StarfieldPresets.Default();
        var merged = CliParser.Parse(["--no-nebula"]).ApplyTo(baseline);

        Assert.False(merged.Nebula.Enabled);
        Assert.Equal(baseline.Nebula.Palettes.Count, merged.Nebula.Palettes.Count);
        Assert.Equal(baseline.Nebula.Intensity, merged.Nebula.Intensity);
    }

    [Fact]
    public void ApplyTo_WithoutABaseline_Throws()
        => Assert.Throws<ArgumentNullException>(() => new CliArguments().ApplyTo(null!));

    [Fact]
    public void ValidateOverrides_RejectsAMalformedBackground()
    {
        Assert.Single(CliParser.Parse(["--background", "octarine"]).ValidateOverrides());
        Assert.Empty(CliParser.Parse(["--background", "#123456"]).ValidateOverrides());
    }

    [Theory]
    [InlineData("--seamless-y", true)]
    [InlineData("--no-seamless-y", false)]
    public void Parse_ReadsTheVerticalTilingSwitch(string option, bool expected)
    {
        var parsed = CliParser.Parse([option]);

        Assert.Empty(parsed.Errors);
        Assert.Equal(expected, parsed.SeamlessY);
        Assert.Null(parsed.SeamlessX);
    }

    [Fact]
    public void Parse_WithoutArguments_Throws()
        => Assert.Throws<ArgumentNullException>(() => CliParser.Parse(null!));

    [Fact]
    public void HelpText_DocumentsEveryOptionTheParserAccepts()
    {
        var usage = HelpText.Usage;

        foreach (var option in new[]
        {
            "--out", "--layers", "--width", "--height", "--seed", "--exposure", "--background",
            "--seamless", "--no-seamless", "--seamless-y", "--no-seamless-y", "--nebula", "--no-nebula", "--preset", "--dump-preset",
            "--threads", "--band-height", "--memory", "--compression", "--verbose", "--quiet", "--help",
        })
        {
            Assert.Contains(option, usage, StringComparison.Ordinal);
        }
    }
}
