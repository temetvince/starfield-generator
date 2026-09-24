using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace Starfield.Cli.CommandLine;

/// <summary>
/// Turns <see cref="CliArguments"/> back into the tokens <see cref="CliParser"/> would parse them from.
/// </summary>
/// <remarks>
/// This is the inverse of the parser, and the two are held together by a round-trip test. A front end
/// that builds a command line through here therefore produces exactly what the executable accepts,
/// with numbers written in the invariant culture the parser reads.
/// </remarks>
public static class CliCommandLine
{
    /// <summary>Composes the tokens for a set of arguments.</summary>
    /// <param name="arguments">The arguments to spell out. <see cref="CliArguments.Errors"/> is ignored.</param>
    /// <returns>
    /// The tokens, without the executable name. <see cref="CliArguments.OutputPath"/> is always written,
    /// so the result is never empty and never reads as a request for help by accident.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="arguments"/> is <see langword="null"/>.</exception>
    public static ImmutableArray<string> Compose(CliArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var tokens = ImmutableArray.CreateBuilder<string>();

        if (arguments.HelpRequested)
        {
            tokens.Add(CliOptionNames.Help);
        }

        tokens.Add(CliOptionNames.Out);
        tokens.Add(arguments.OutputPath);
        AddValue(tokens, CliOptionNames.Layers, arguments.LayersDirectory);
        AddValue(tokens, CliOptionNames.Preset, arguments.PresetPath);
        AddValue(tokens, CliOptionNames.DumpPreset, arguments.DumpPresetPath);

        AddNumber(tokens, CliOptionNames.Width, arguments.Width);
        AddNumber(tokens, CliOptionNames.Height, arguments.Height);
        AddNumber(tokens, CliOptionNames.Seed, arguments.Seed);
        AddNumber(tokens, CliOptionNames.Exposure, arguments.Exposure);
        AddValue(tokens, CliOptionNames.Background, arguments.Background);
        AddSwitch(tokens, CliOptionNames.Seamless, CliOptionNames.NoSeamless, arguments.SeamlessX);
        AddSwitch(tokens, CliOptionNames.SeamlessY, CliOptionNames.NoSeamlessY, arguments.SeamlessY);
        AddSwitch(tokens, CliOptionNames.Nebula, CliOptionNames.NoNebula, arguments.NebulaEnabled);

        AddNumber(tokens, CliOptionNames.Threads, arguments.Threads);
        AddNumber(tokens, CliOptionNames.BandHeight, arguments.BandHeight);
        AddNumber(tokens, CliOptionNames.Memory, arguments.MemoryBudgetMebibytes);

        if (arguments.Compression is { } compression)
        {
            tokens.Add(CliOptionNames.Compression);
            tokens.Add(CompressionLevelNames.ToName(compression));
        }

        if (arguments.Verbose)
        {
            tokens.Add(CliOptionNames.Verbose);
        }

        if (arguments.Quiet)
        {
            tokens.Add(CliOptionNames.Quiet);
        }

        return tokens.ToImmutable();
    }

    /// <summary>Joins tokens into one line a person can read or paste into a shell.</summary>
    /// <param name="tokens">The tokens, typically the executable followed by <see cref="Compose"/>.</param>
    /// <returns>
    /// The tokens separated by spaces. A token that is empty or contains whitespace or a quote is
    /// wrapped in double quotes, with inner quotes escaped by a backslash, which both Windows and
    /// POSIX shells read back as one argument.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="tokens"/> is <see langword="null"/>.</exception>
    public static string Format(IEnumerable<string> tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);

        var line = new StringBuilder();
        foreach (var token in tokens)
        {
            if (line.Length > 0)
            {
                line.Append(' ');
            }

            line.Append(NeedsQuoting(token) ? Quote(token) : token);
        }

        return line.ToString();
    }

    private static bool NeedsQuoting(string token)
        => token.Length == 0 || token.Any(character => char.IsWhiteSpace(character) || character == '"');

    private static string Quote(string token)
        => "\"" + token.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private static void AddValue(ImmutableArray<string>.Builder tokens, string option, string? value)
    {
        if (value is not null)
        {
            tokens.Add(option);
            tokens.Add(value);
        }
    }

    private static void AddNumber<T>(ImmutableArray<string>.Builder tokens, string option, T? value)
        where T : struct, IFormattable
    {
        if (value is { } number)
        {
            tokens.Add(option);
            tokens.Add(number.ToString(null, CultureInfo.InvariantCulture));
        }
    }

    private static void AddSwitch(ImmutableArray<string>.Builder tokens, string on, string off, bool? value)
    {
        if (value is { } enabled)
        {
            tokens.Add(enabled ? on : off);
        }
    }
}
