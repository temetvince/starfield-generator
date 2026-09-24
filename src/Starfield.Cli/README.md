# Starfield.Cli

The executable, named `starfield`. It parses arguments, loads and saves presets, sets up console
logging, and hands the work to [`Starfield.Core`](../Starfield.Core/README.md). It contains no rendering
logic.

## File map

| File | Type | Purpose |
| --- | --- | --- |
| `Program.cs` | top-level statements | Wires parsing, preset loading, validation and rendering together, and maps failures to exit codes. |
| `CommandLine/CliParser.cs` | `CliParser` | Turns tokens into `CliArguments`. Touches no files and never exits the process. |
| `CommandLine/CliArguments.cs` | `CliArguments` | The parsed request, and `ApplyTo` which lays it over a baseline. |
| `CommandLine/HelpText.cs` | `HelpText` | The usage text. |
| `CommandLine/CliOptionNames.cs` | `CliOptionNames` | Every option spelled once, shared by the parser and the composer. |
| `CommandLine/CliCommandLine.cs` | `CliCommandLine` | The parser's inverse: `CliArguments` back to tokens, plus shell-style formatting for display. |
| `CommandLine/CompressionLevelNames.cs` | `CompressionLevelNames` | The words for compression levels, in both directions. |
| `ExitCodes.cs` | `ExitCodes` | The process exit codes, named. |
| `Presets/PresetFile.cs` | `PresetFile` | Reads and writes the JSON preset format. |
| `Log.cs` | `Log` | Source-generated log messages. |

## Options

| Option | Short | Meaning |
| --- | --- | --- |
| `--out <file>` | `-o` | Composite PNG to write. Default `starfield.png`. |
| `--layers <dir>` | | Also write each layer on its own as a transparent PNG. |
| `--width <px>` | `-w` | Image width. |
| `--height <px>` | `-h` | Image height. |
| `--seed <n>` | `-s` | Seed. The same seed always produces the same image. |
| `--exposure <n>` | `-e` | Brightness multiplier applied before tone mapping. |
| `--background <hex>` | `-b` | Background colour, such as `#02030A`. |
| `--seamless`, `--no-seamless` | | Whether the left and right edges join. Seamless is the default. |
| `--seamless-y`, `--no-seamless-y` | | Whether the top and bottom edges join as well, for a tile that repeats both ways. On by default. |
| `--nebula`, `--no-nebula` | | Whether the nebula is rendered. On by default. |
| `--preset <file>` | | Load all settings from JSON, then apply any other options on top. |
| `--dump-preset <file>` | | Write the settings that would be used, then exit without rendering. |
| `--threads <n>` | `-t` | Bands rendered at once. `0` uses one per processor. |
| `--band-height <rows>` | | Rows per band. Larger uses more memory. |
| `--memory <MiB>` | `-m` | Ceiling on band buffers. `0` removes it. Default 512. |
| `--compression <level>` | `-c` | `none`, `fastest`, `optimal` or `smallest`. |
| `--verbose`, `--quiet` | `-v`, `-q` | Raise or lower the log level. |
| `--help` | `-?` | Show usage. An empty command line does the same. |

Every parse problem is collected and reported together, so a command line with three mistakes prints
three messages rather than one per attempt.

## Order of settings

Later wins:

1. The built-in defaults from `StarfieldPresets.Default()`.
2. A preset file, if `--preset` was given.
3. Individual command-line options.

The merged result is validated as a whole, which means a preset file gets the same checks a command line
does.

## Exit codes

| Code | Meaning |
| --- | --- |
| 0 | Success. |
| 1 | The request was not usable: an unknown option, an unreadable value, invalid settings, or a preset that could not be read. |
| 2 | The work failed: the output could not be written. |
| 3 | Cancelled with Ctrl+C. Any partly written file should be discarded. |

## The preset format

A preset is `StarfieldOptions` serialised as JSON. Enums are written by name, comments and trailing
commas are accepted on read, and a preset only needs the settings it wants to change — everything else
keeps its default. Start from `--dump-preset` and delete what you do not care about:

```json
{
  "Width": 10000,
  "Height": 1080,
  "Seed": 42,
  "Nebula": { "Intensity": 0.8 }
}
```

To pin every seed to one colour, or to add ramps of your own, replace the nebula's `Palettes` list.
Each palette is a name plus colour stops from the faint rim (position 0) to the dense core (position 1).
`HueVariation` is how far, in turns of the colour wheel, a seed may nudge the palette it chose:

```json
{
  "Nebula": {
    "HueVariation": 0,
    "Palettes": [
      {
        "Name": "crimson",
        "ColorStops": [
          { "Position": 0.0, "Color": "#260A1C" },
          { "Position": 0.65, "Color": "#B8324A" },
          { "Position": 1.0, "Color": "#FFDDB4" }
        ]
      }
    ]
  }
}
```

Serialisation is reflection-based on purpose. The System.Text.Json source generator drops the
initialisers of `init`-only properties, which would make every omitted setting come back as zero instead
of its default. Publishing this tool trimmed or ahead-of-time compiled would mean revisiting that,
most likely by giving the preset format its own mutable data model.
