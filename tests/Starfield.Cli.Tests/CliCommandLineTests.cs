using System.IO.Compression;
using Starfield.Cli.CommandLine;

namespace Starfield.Cli.Tests;

/// <summary>Checks that composing a command line is the exact inverse of parsing one.</summary>
public sealed class CliCommandLineTests
{
    private static CliArguments Everything => new()
    {
        HelpRequested = true,
        OutputPath = "out dir/composite.png",
        LayersDirectory = "./planes",
        PresetPath = "presets/nebula.json",
        DumpPresetPath = "dump.json",
        Verbose = true,
        Quiet = true,
        Width = 4096,
        Height = 1024,
        Seed = 18446744073709551615UL,
        SeamlessX = false,
        Exposure = 1.25f,
        Background = "#02030A",
        NebulaEnabled = false,
        Threads = 4,
        BandHeight = 32,
        MemoryBudgetMebibytes = 256,
        Compression = CompressionLevel.SmallestSize,
    };

    [Fact]
    public void Compose_ThenParse_ReturnsTheSameArguments()
    {
        var tokens = CliCommandLine.Compose(Everything);

        var parsed = CliParser.Parse([.. tokens]);

        Assert.Empty(parsed.Errors);
        Assert.Equal(Everything, parsed);
    }

    [Fact]
    public void Compose_WithOnlyDefaults_WritesJustTheOutputPath()
    {
        var tokens = CliCommandLine.Compose(new CliArguments());

        Assert.Equal<string>([CliOptionNames.Out, "starfield.png"], tokens);
    }

    [Theory]
    [InlineData(true, CliOptionNames.Seamless)]
    [InlineData(false, CliOptionNames.NoSeamless)]
    public void Compose_WritesEachSideOfASwitch(bool value, string expected)
    {
        var tokens = CliCommandLine.Compose(new CliArguments { SeamlessX = value });

        Assert.Contains(expected, tokens);
    }

    [Fact]
    public void Compose_WritesNumbersInTheInvariantCulture()
    {
        var tokens = CliCommandLine.Compose(new CliArguments { Exposure = 0.5f, Width = 10000 });

        Assert.Contains("0.5", tokens);
        Assert.Contains("10000", tokens);
    }

    [Theory]
    [InlineData(CompressionLevel.NoCompression, "none")]
    [InlineData(CompressionLevel.Fastest, "fastest")]
    [InlineData(CompressionLevel.Optimal, "optimal")]
    [InlineData(CompressionLevel.SmallestSize, "smallest")]
    public void CompressionLevelNames_RoundTripEveryLevel(CompressionLevel level, string name)
    {
        Assert.Equal(name, CompressionLevelNames.ToName(level));
        Assert.True(CompressionLevelNames.TryParse(name, out var parsed));
        Assert.Equal(level, parsed);
    }

    [Fact]
    public void Format_QuotesOnlyTheTokensThatNeedIt()
    {
        var line = CliCommandLine.Format(["starfield", "-o", "my field.png", "--seed", "7", "", "say \"hi\""]);

        Assert.Equal("starfield -o \"my field.png\" --seed 7 \"\" \"say \\\"hi\\\"\"", line);
    }
}
