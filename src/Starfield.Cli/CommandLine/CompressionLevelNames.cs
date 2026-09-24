using System.IO.Compression;

namespace Starfield.Cli.CommandLine;

/// <summary>
/// The words the command line uses for PNG compression levels.
/// </summary>
/// <remarks>
/// Parsing accepts a few aliases for convenience; formatting always produces the canonical word, so a
/// composed command line reads the way the help text describes it.
/// </remarks>
public static class CompressionLevelNames
{
    /// <summary>The canonical word for <see cref="CompressionLevel.NoCompression"/>.</summary>
    public const string None = "none";

    /// <summary>The canonical word for <see cref="CompressionLevel.Fastest"/>.</summary>
    public const string Fastest = "fastest";

    /// <summary>The canonical word for <see cref="CompressionLevel.Optimal"/>.</summary>
    public const string Optimal = "optimal";

    /// <summary>The canonical word for <see cref="CompressionLevel.SmallestSize"/>.</summary>
    public const string Smallest = "smallest";

    /// <summary>Gets the words a user may type, in the order the help text lists them.</summary>
    /// <value>The four canonical words.</value>
    public static IReadOnlyList<string> Canonical { get; } = [None, Fastest, Optimal, Smallest];

    /// <summary>Formats a level as its canonical word.</summary>
    /// <param name="level">The level to format.</param>
    /// <returns>One of <see cref="Canonical"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="level"/> is not a defined level.</exception>
    public static string ToName(CompressionLevel level) => level switch
    {
        CompressionLevel.NoCompression => None,
        CompressionLevel.Fastest => Fastest,
        CompressionLevel.Optimal => Optimal,
        CompressionLevel.SmallestSize => Smallest,
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown compression level."),
    };

    /// <summary>Parses a word, accepting the aliases the help text does not mention.</summary>
    /// <param name="text">The word, in any letter case.</param>
    /// <param name="level">The level it names, or <see cref="CompressionLevel.Optimal"/> when parsing fails.</param>
    /// <returns><see langword="true"/> when <paramref name="text"/> was understood.</returns>
    public static bool TryParse(string? text, out CompressionLevel level)
    {
        switch (text?.ToLowerInvariant())
        {
            case None:
                level = CompressionLevel.NoCompression;
                return true;
            case Fastest or "fast":
                level = CompressionLevel.Fastest;
                return true;
            case Optimal or "default":
                level = CompressionLevel.Optimal;
                return true;
            case Smallest or "max":
                level = CompressionLevel.SmallestSize;
                return true;
            default:
                level = CompressionLevel.Optimal;
                return false;
        }
    }
}
