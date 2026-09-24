using System.Text.Json;
using System.Text.Json.Serialization;
using Starfield.Core.Options;

namespace Starfield.Cli.Presets;

/// <summary>
/// Reads and writes the JSON preset that carries a complete <see cref="StarfieldOptions"/>.
/// </summary>
/// <remarks>
/// <para>
/// The format is the options record itself, so anything the library can express a preset can express.
/// Enums are written by name, and comments and trailing commas are accepted on read, because presets
/// are meant to be edited by hand.
/// </para>
/// <para>
/// A preset may name only the settings it cares about. Everything it leaves out keeps the default from
/// the options record, which is what makes a three-line preset a usable thing to write.
/// </para>
/// <para>
/// Serialisation is reflection-based on purpose. The System.Text.Json source generator drops the
/// initialisers of <c>init</c>-only properties, so a partial preset would come back with zeros instead
/// of those defaults. Publishing this tool trimmed or ahead-of-time compiled would mean revisiting that
/// trade-off, most likely by giving the format its own mutable data model.
/// </para>
/// </remarks>
public static class PresetFile
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Loads options from a JSON preset.</summary>
    /// <param name="path">The file to read. Must exist.</param>
    /// <returns>The options it describes, with anything the file omits left at its default.</returns>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty or whitespace.</exception>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="DirectoryNotFoundException">The path names a directory that does not exist.</exception>
    /// <exception cref="JsonException">The file is not valid JSON for this format.</exception>
    /// <exception cref="InvalidDataException">The file parsed to nothing, which means it contained only <c>null</c>.</exception>
    public static StarfieldOptions Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<StarfieldOptions>(stream, SerializerOptions)
            ?? throw new InvalidDataException($"'{path}' does not contain a star field preset.");
    }

    /// <summary>Writes options to a JSON preset.</summary>
    /// <param name="path">
    /// Where to write. Any existing file is replaced, and the parent directory is created if needed.
    /// </param>
    /// <param name="options">The options to write. Every setting is written, not just the non-default ones.</param>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="IOException">The file could not be written.</exception>
    public static void Save(string path, StarfieldOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(options);

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = File.Create(path);
        JsonSerializer.Serialize(stream, options, SerializerOptions);
    }
}
