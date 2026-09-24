using System.Collections.Immutable;
using System.Globalization;
using System.IO.Compression;

namespace Starfield.Cli.CommandLine;

/// <summary>
/// Turns raw command-line tokens into <see cref="CliArguments"/>.
/// </summary>
/// <remarks>
/// The parser is deliberately small and pure: it reads no files, writes no output and never exits the
/// process. Every problem it finds is collected into <see cref="CliArguments.Errors"/> so the caller can
/// report all of them at once instead of one per run.
/// </remarks>
public static class CliParser
{
    /// <summary>Parses a command line.</summary>
    /// <param name="args">The tokens, without the executable name. An empty array requests help.</param>
    /// <returns>
    /// The parsed arguments. Check <see cref="CliArguments.Errors"/> and
    /// <see cref="CliArguments.HelpRequested"/> before using anything else.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="args"/> is <see langword="null"/>.</exception>
    public static CliArguments Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length == 0)
        {
            return new CliArguments { HelpRequested = true };
        }

        var errors = ImmutableArray.CreateBuilder<string>();
        var result = new CliArguments();

        for (var index = 0; index < args.Length; index++)
        {
            var token = args[index];

            switch (token)
            {
                case CliOptionNames.Help or CliOptionNames.HelpShort or CliOptionNames.HelpSlash:
                    result = result with { HelpRequested = true };
                    break;

                case CliOptionNames.Verbose or CliOptionNames.VerboseShort:
                    result = result with { Verbose = true };
                    break;

                case CliOptionNames.Quiet or CliOptionNames.QuietShort:
                    result = result with { Quiet = true };
                    break;

                case CliOptionNames.NoNebula:
                    result = result with { NebulaEnabled = false };
                    break;

                case CliOptionNames.Nebula:
                    result = result with { NebulaEnabled = true };
                    break;

                case CliOptionNames.NoSeamless:
                    result = result with { SeamlessX = false };
                    break;

                case CliOptionNames.Seamless:
                    result = result with { SeamlessX = true };
                    break;

                case CliOptionNames.NoSeamlessY:
                    result = result with { SeamlessY = false };
                    break;

                case CliOptionNames.SeamlessY:
                    result = result with { SeamlessY = true };
                    break;

                case CliOptionNames.Out or CliOptionNames.OutShort:
                    result = result with { OutputPath = TakeValue(args, ref index, token, errors) ?? result.OutputPath };
                    break;

                case CliOptionNames.Layers:
                    result = result with { LayersDirectory = TakeValue(args, ref index, token, errors) };
                    break;

                case CliOptionNames.Preset:
                    result = result with { PresetPath = TakeValue(args, ref index, token, errors) };
                    break;

                case CliOptionNames.DumpPreset:
                    result = result with { DumpPresetPath = TakeValue(args, ref index, token, errors) };
                    break;

                case CliOptionNames.Width or CliOptionNames.WidthShort:
                    result = result with { Width = TakeInt(args, ref index, token, errors) ?? result.Width };
                    break;

                case CliOptionNames.Height or CliOptionNames.HeightShort:
                    result = result with { Height = TakeInt(args, ref index, token, errors) ?? result.Height };
                    break;

                case CliOptionNames.Seed or CliOptionNames.SeedShort:
                    result = result with { Seed = TakeSeed(args, ref index, token, errors) ?? result.Seed };
                    break;

                case CliOptionNames.Threads or CliOptionNames.ThreadsShort:
                    result = result with { Threads = TakeInt(args, ref index, token, errors) ?? result.Threads };
                    break;

                case CliOptionNames.Memory or CliOptionNames.MemoryShort:
                    result = result with
                    {
                        MemoryBudgetMebibytes = TakeInt(args, ref index, token, errors) ?? result.MemoryBudgetMebibytes,
                    };
                    break;

                case CliOptionNames.BandHeight:
                    result = result with { BandHeight = TakeInt(args, ref index, token, errors) ?? result.BandHeight };
                    break;

                case CliOptionNames.Exposure or CliOptionNames.ExposureShort:
                    result = result with { Exposure = TakeFloat(args, ref index, token, errors) ?? result.Exposure };
                    break;

                case CliOptionNames.Background or CliOptionNames.BackgroundShort:
                    result = result with { Background = TakeValue(args, ref index, token, errors) ?? result.Background };
                    break;

                case CliOptionNames.Compression or CliOptionNames.CompressionShort:
                    result = result with { Compression = TakeCompression(args, ref index, token, errors) ?? result.Compression };
                    break;

                default:
                    errors.Add($"Unknown option '{token}'. Run with {CliOptionNames.Help} to see the available options.");
                    break;
            }
        }

        return result with { Errors = errors.ToImmutable() };
    }

    private static string? TakeValue(string[] args, ref int index, string option, ImmutableArray<string>.Builder errors)
    {
        if (index + 1 >= args.Length)
        {
            errors.Add($"{option} needs a value.");
            return null;
        }

        return args[++index];
    }

    private static int? TakeInt(string[] args, ref int index, string option, ImmutableArray<string>.Builder errors)
    {
        var text = TakeValue(args, ref index, option, errors);
        if (text is null)
        {
            return null;
        }

        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        errors.Add($"{option} expects a whole number but got '{text}'.");
        return null;
    }

    private static ulong? TakeSeed(string[] args, ref int index, string option, ImmutableArray<string>.Builder errors)
    {
        var text = TakeValue(args, ref index, option, errors);
        if (text is null)
        {
            return null;
        }

        if (ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        errors.Add($"{option} expects a whole number from 0 upwards but got '{text}'.");
        return null;
    }

    private static float? TakeFloat(string[] args, ref int index, string option, ImmutableArray<string>.Builder errors)
    {
        var text = TakeValue(args, ref index, option, errors);
        if (text is null)
        {
            return null;
        }

        if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        errors.Add($"{option} expects a number but got '{text}'.");
        return null;
    }

    private static CompressionLevel? TakeCompression(
        string[] args,
        ref int index,
        string option,
        ImmutableArray<string>.Builder errors)
    {
        var text = TakeValue(args, ref index, option, errors);
        if (text is null)
        {
            return null;
        }

        if (CompressionLevelNames.TryParse(text, out var level))
        {
            return level;
        }

        errors.Add($"{option} expects {string.Join(", ", CompressionLevelNames.Canonical)} but got '{text}'.");
        return null;
    }
}
