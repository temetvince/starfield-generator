namespace Starfield.Cli.CommandLine;

/// <summary>The usage text shown for <c>--help</c> and for an empty command line.</summary>
public static class HelpText
{
    /// <summary>Gets the full usage text.</summary>
    /// <value>Ends without a trailing newline, so the caller decides how to terminate it.</value>
    public static string Usage
        => """
        starfield - generate star field and nebula PNGs at any size.

        Usage:
          starfield [options]

        Output:
          -o, --out <file>          Composite PNG to write. Default: starfield.png
              --layers <dir>        Also write each layer on its own as a transparent PNG.

        Image:
          -w, --width <px>          Image width. Default: 10000
          -h, --height <px>         Image height. Default: 1080
          -s, --seed <n>            Seed. The same seed always produces the same image.
          -e, --exposure <n>        Brightness multiplier applied before tone mapping. Default: 1
          -b, --background <hex>    Background colour, such as #02030A
              --seamless            Make the left and right edges join. This is the default.
              --no-seamless         Do not wrap the edges.
              --nebula              Render the nebula. This is the default.
              --no-nebula           Render stars only.

        Presets:
              --preset <file>       Load all settings from a JSON preset, then apply any options above.
              --dump-preset <file>  Write the settings that would be used to a JSON file and exit.

        Performance:
          -t, --threads <n>         Bands rendered at once. 0 uses one per processor. Default: 0
              --band-height <rows>  Rows per band. Larger uses more memory. Default: 64
          -m, --memory <MiB>        Ceiling on band buffers. Fewer bands run at once to stay under it.
                                    0 removes the ceiling. Default: 512
          -c, --compression <level> none, fastest, optimal or smallest. Default: optimal

        Diagnostics:
          -v, --verbose             Log every step.
          -q, --quiet               Log warnings and errors only.
              --help                Show this text.

        Examples:
          starfield -w 10000 -h 1080 -s 42 -o wide.png
          starfield --preset nebula.json --layers ./planes -o composite.png
          starfield --dump-preset default.json
        """;
}
