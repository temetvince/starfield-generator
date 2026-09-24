namespace Starfield.Cli.CommandLine;

/// <summary>
/// Every option the command line understands, spelled once.
/// </summary>
/// <remarks>
/// <see cref="CliParser"/> reads these and <see cref="CliCommandLine"/> writes them, so a front end that
/// composes a command line cannot drift from what the parser accepts.
/// </remarks>
public static class CliOptionNames
{
    /// <summary>Show usage and exit.</summary>
    public const string Help = "--help";

    /// <summary>Short form of <see cref="Help"/>.</summary>
    public const string HelpShort = "-?";

    /// <summary>Windows-style form of <see cref="Help"/>.</summary>
    public const string HelpSlash = "/?";

    /// <summary>Log every step.</summary>
    public const string Verbose = "--verbose";

    /// <summary>Short form of <see cref="Verbose"/>.</summary>
    public const string VerboseShort = "-v";

    /// <summary>Log warnings and errors only.</summary>
    public const string Quiet = "--quiet";

    /// <summary>Short form of <see cref="Quiet"/>.</summary>
    public const string QuietShort = "-q";

    /// <summary>Render the nebula.</summary>
    public const string Nebula = "--nebula";

    /// <summary>Render stars only.</summary>
    public const string NoNebula = "--no-nebula";

    /// <summary>Make the left and right edges join.</summary>
    public const string Seamless = "--seamless";

    /// <summary>Do not wrap the edges.</summary>
    public const string NoSeamless = "--no-seamless";

    /// <summary>The composite PNG to write.</summary>
    public const string Out = "--out";

    /// <summary>Short form of <see cref="Out"/>.</summary>
    public const string OutShort = "-o";

    /// <summary>The directory that receives one transparent PNG per layer.</summary>
    public const string Layers = "--layers";

    /// <summary>The preset file to load as the baseline.</summary>
    public const string Preset = "--preset";

    /// <summary>The file to write the effective options to instead of rendering.</summary>
    public const string DumpPreset = "--dump-preset";

    /// <summary>Image width in pixels.</summary>
    public const string Width = "--width";

    /// <summary>Short form of <see cref="Width"/>.</summary>
    public const string WidthShort = "-w";

    /// <summary>Image height in pixels.</summary>
    public const string Height = "--height";

    /// <summary>Short form of <see cref="Height"/>.</summary>
    public const string HeightShort = "-h";

    /// <summary>The seed.</summary>
    public const string Seed = "--seed";

    /// <summary>Short form of <see cref="Seed"/>.</summary>
    public const string SeedShort = "-s";

    /// <summary>Bands rendered at once.</summary>
    public const string Threads = "--threads";

    /// <summary>Short form of <see cref="Threads"/>.</summary>
    public const string ThreadsShort = "-t";

    /// <summary>Ceiling on band buffers, in mebibytes.</summary>
    public const string Memory = "--memory";

    /// <summary>Short form of <see cref="Memory"/>.</summary>
    public const string MemoryShort = "-m";

    /// <summary>Rows per band.</summary>
    public const string BandHeight = "--band-height";

    /// <summary>Brightness multiplier applied before tone mapping.</summary>
    public const string Exposure = "--exposure";

    /// <summary>Short form of <see cref="Exposure"/>.</summary>
    public const string ExposureShort = "-e";

    /// <summary>Background colour as hex text.</summary>
    public const string Background = "--background";

    /// <summary>Short form of <see cref="Background"/>.</summary>
    public const string BackgroundShort = "-b";

    /// <summary>PNG compression level.</summary>
    public const string Compression = "--compression";

    /// <summary>Short form of <see cref="Compression"/>.</summary>
    public const string CompressionShort = "-c";
}
