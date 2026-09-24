# Tests

```sh
dotnet test StarfieldGenerator.slnx
```

Five projects: one shared helper library and four suites, each covering the project it is named after.

| Project | Covers |
| --- | --- |
| `Imaging.TestSupport` | Not a suite. Holds `DecodedPng`, a minimal PNG reader used by the other projects. |
| `Imaging.Core.Tests` | The PNG encoder, noise, colour maths, the band loop and the encoder pipeline. |
| `Starfield.Core.Tests` | Star placement, the end-to-end renderer, layer export and options validation. |
| `Starfield.Cli.Tests` | Argument parsing, option merging, the preset file format, and the composer that is the parser's inverse. |
| `Starfield.App.Tests` | The front end's form and window state, and in-process rendering. |

## Why there is a decoder in the test tree

`DecodedPng` parses the chunk structure, verifies every chunk CRC, inflates the pixel data and reverses
the row filters. Encoder tests therefore check the output against the file format rather than against
the encoder's own idea of it: if a test passes, a real decoder would have read the file too. It lives in
its own project because both rendering suites need it, and it is deliberately absent from the shipping
library, which only ever writes PNGs.

## The load-bearing tests

Most of the suite is ordinary coverage. These few are the ones that would catch a real regression in the
design:

- **Band independence.** `BandedImageRendererTests` and `StarfieldRendererTests` render the same image
  at band heights of 1, 7, 64 and larger than the image, and at several thread counts, then assert the
  decoded pixels are identical. This is the guarantee the whole streaming design rests on, and it is
  what caught the half-pixel error in star culling that produced faint seams on band boundaries.
- **Star set equivalence.** `StarCellFieldTests` asserts that the stars collected for the whole image
  equal the union of the stars collected for slices of it. A star straddling a slice boundary is
  reported by both slices, so the comparison is by set rather than by sequence. `ClusteringTests`
  repeats the check with clustering on, since rejection sampling adds a second chance to get it wrong.
- **The clustering ceiling.** `StarDensityFieldTests` sweeps the field and asserts no sample exceeds the
  ceiling it advertises. Star placement samples against that ceiling, so an underestimate would silently
  clip the densest regions rather than failing outright.
- **Seamlessness.** `NoiseTests` asserts a seamless field samples identically at `x = 0` and `x = 1`,
  and that a non-seamless one does not. `StarfieldRendererTests` makes the same check on a rendered
  nebula by comparing the two edge columns.
- **Alpha round trip.** `BandEncoderTests` asserts that a transparent layer composited back over black
  reproduces the opaque encode, which is what makes exported parallax planes match the composite.
- **Command-line round trip.** `CliCommandLineTests` composes a fully populated `CliArguments` into
  tokens and parses them back, asserting equality, so the two spellings of the option vocabulary
  cannot drift apart.

## Conventions

Test methods use the `Method_Scenario_Expected` naming convention, so `.editorconfig` turns off CA1707
for `*Tests.cs` files only. Float comparisons use an explicit tolerance rather than a decimal-place
count: values that agree to within rounding can still land either side of a rounded decimal boundary.
