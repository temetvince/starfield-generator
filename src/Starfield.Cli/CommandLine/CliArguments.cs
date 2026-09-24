using System.Collections.Immutable;
using System.IO.Compression;
using Imaging.Core.Colors;
using Starfield.Core.Options;

namespace Starfield.Cli.CommandLine;

/// <summary>
/// What the command line asked for: the overrides it supplied and the paths it named.
/// </summary>
/// <remarks>
/// Every setting is nullable because the command line only ever supplies a subset. The baseline comes
/// from a preset file or from <see cref="StarfieldPresets.Default"/>, and <see cref="ApplyTo"/> lays
/// these on top. Parsing itself touches no files, which keeps it cheap to test.
/// </remarks>
public sealed record CliArguments
{
    /// <summary>Gets the problems found while parsing.</summary>
    /// <value>Empty when the command line was understood. Any entry means nothing should be rendered.</value>
    public ImmutableArray<string> Errors { get; init; } = [];

    /// <summary>Gets whether usage text was requested.</summary>
    /// <value><see langword="true"/> for <c>--help</c>, or when the command line was empty.</value>
    public bool HelpRequested { get; init; }

    /// <summary>Gets where the composite image is written.</summary>
    /// <value>Defaults to <c>starfield.png</c> in the working directory.</value>
    public string OutputPath { get; init; } = "starfield.png";

    /// <summary>Gets the directory that receives one transparent PNG per layer.</summary>
    /// <value><see langword="null"/> unless <c>--layers</c> was given.</value>
    public string? LayersDirectory { get; init; }

    /// <summary>Gets the preset file to load as the baseline.</summary>
    /// <value><see langword="null"/> to start from the built-in default.</value>
    public string? PresetPath { get; init; }

    /// <summary>Gets the file to write the effective options to.</summary>
    /// <value>
    /// <see langword="null"/> unless <c>--dump-preset</c> was given. When set, the options are written
    /// and no image is rendered.
    /// </value>
    public string? DumpPresetPath { get; init; }

    /// <summary>Gets whether debug-level logging was requested.</summary>
    /// <value><see langword="true"/> for <c>--verbose</c>.</value>
    public bool Verbose { get; init; }

    /// <summary>Gets whether logging should be reduced to warnings and errors.</summary>
    /// <value><see langword="true"/> for <c>--quiet</c>, which wins over <see cref="Verbose"/>.</value>
    public bool Quiet { get; init; }

    /// <summary>Gets the image width override, in pixels.</summary>
    /// <value><see langword="null"/> to keep the baseline width.</value>
    public int? Width { get; init; }

    /// <summary>Gets the image height override, in pixels.</summary>
    /// <value><see langword="null"/> to keep the baseline height.</value>
    public int? Height { get; init; }

    /// <summary>Gets the seed override.</summary>
    /// <value><see langword="null"/> to keep the baseline seed.</value>
    public ulong? Seed { get; init; }

    /// <summary>Gets the horizontal-tiling override.</summary>
    /// <value><see langword="null"/> to keep the baseline setting.</value>
    public bool? SeamlessX { get; init; }

    /// <summary>Gets the vertical-tiling override.</summary>
    /// <value><see langword="null"/> to keep the baseline setting.</value>
    public bool? SeamlessY { get; init; }

    /// <summary>Gets the exposure override.</summary>
    /// <value><see langword="null"/> to keep the baseline exposure.</value>
    public float? Exposure { get; init; }

    /// <summary>Gets the background colour override, as hex text.</summary>
    /// <value><see langword="null"/> to keep the baseline background.</value>
    public string? Background { get; init; }

    /// <summary>Gets the nebula override.</summary>
    /// <value><see langword="null"/> to keep the baseline setting; <see langword="false"/> for stars alone.</value>
    public bool? NebulaEnabled { get; init; }

    /// <summary>Gets the worker-count override.</summary>
    /// <value><see langword="null"/> to keep the baseline; zero means one worker per logical processor.</value>
    public int? Threads { get; init; }

    /// <summary>Gets the band-height override, in image rows.</summary>
    /// <value><see langword="null"/> to keep the baseline band height.</value>
    public int? BandHeight { get; init; }

    /// <summary>Gets the band-buffer memory ceiling override, in mebibytes.</summary>
    /// <value><see langword="null"/> to keep the baseline ceiling; zero removes it.</value>
    public int? MemoryBudgetMebibytes { get; init; }

    /// <summary>Gets the compression-level override.</summary>
    /// <value><see langword="null"/> to keep the baseline level.</value>
    public CompressionLevel? Compression { get; init; }

    /// <summary>Lays these overrides on top of a baseline.</summary>
    /// <param name="baseline">The options loaded from a preset, or the built-in default.</param>
    /// <returns>A new set of options; the baseline is not modified.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="baseline"/> is <see langword="null"/>.</exception>
    public StarfieldOptions ApplyTo(StarfieldOptions baseline)
    {
        ArgumentNullException.ThrowIfNull(baseline);

        var result = baseline with
        {
            Width = Width ?? baseline.Width,
            Height = Height ?? baseline.Height,
            Seed = Seed ?? baseline.Seed,
            SeamlessX = SeamlessX ?? baseline.SeamlessX,
            SeamlessY = SeamlessY ?? baseline.SeamlessY,
            Exposure = Exposure ?? baseline.Exposure,
            Background = Background ?? baseline.Background,
            MaxDegreeOfParallelism = Threads ?? baseline.MaxDegreeOfParallelism,
            BandHeight = BandHeight ?? baseline.BandHeight,
            CompressionLevel = Compression ?? baseline.CompressionLevel,
            MaxWorkingSetBytes = MemoryBudgetMebibytes is null
                ? baseline.MaxWorkingSetBytes
                : (long)MemoryBudgetMebibytes.Value * 1024 * 1024,
        };

        return NebulaEnabled is null
            ? result
            : result with { Nebula = result.Nebula with { Enabled = NebulaEnabled.Value } };
    }

    /// <summary>Reports command-line problems that only a colour parse can find.</summary>
    /// <returns>A human-readable list of problems, empty when the arguments are usable.</returns>
    /// <remarks>
    /// Range checks on the numeric settings belong to <see cref="StarfieldOptions.Validate"/>, which
    /// runs on the merged result and so covers preset files too.
    /// </remarks>
    public IReadOnlyList<string> ValidateOverrides()
    {
        var problems = new List<string>();

        if (Background is not null && !HexColor.TryParse(Background, out _))
        {
            problems.Add($"--background '{Background}' is not a hex colour such as #02030A.");
        }

        return problems;
    }
}
