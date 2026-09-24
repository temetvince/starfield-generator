# Starfield Generator

A .NET 10 command-line tool that renders star fields and nebulae as PNG files, at any size you can
store. It was built for wide parallax backdrops — 10000×1080 is the size it was tuned against — and it
holds a fixed amount of memory no matter how large the image gets.

## Quick start

```sh
dotnet build StarfieldGenerator.slnx
dotnet run --project src/Starfield.Cli -- --width 10000 --height 1080 --seed 42 -o wide.png
```

Or build once and use the executable directly:

```sh
dotnet build src/Starfield.Cli -c Release
./src/Starfield.Cli/bin/Release/net10.0/starfield --width 10000 --height 1080 -o wide.png
```

Run `starfield --help` for the full option list. Every option is also documented in the
[CLI reference](src/Starfield.Cli/README.md).

There is also a desktop front end: pick a size and a seed, press Render, and watch the sky appear. It
renders in-process with the same library, so there is nothing else to install:

```sh
dotnet run --project src/Starfield.Ui
```

## What it renders

Each image is built from a nebula layer, a haze layer and several star layers, composited in order:

- **A galactic band.** Stars are gathered onto a soft band that meanders across the image, broken into
  star clouds by a fractal field and cut by dark dust lanes. The nebulae follow the same band, so the
  whole frame reads as one structure rather than as unrelated pieces.
- **Unresolved-star haze.** The diffuse glow of stars too faint and too crowded to draw individually.
  This is what makes the band look like a band instead of a patch of denser dots.
- **Nebula.** Fractal clouds shaped by three noise fields: one decides where clouds appear along the
  band, one carves the filaments inside them, and one warps the filaments so they curl. A colour ramp
  runs from the faint rim to the dense core, and each seed picks that ramp from a set of authored
  palettes, so one seed's clouds are violet and another's teal or crimson.
- **Star layers.** Each layer is a population with its own density, size, brightness and temperature
  range. Star colour comes from a black-body curve, so a 3000 K star is orange and a 12000 K star is
  blue-white. Brightness follows a power law, giving many faint stars and a few blazing ones.
- **Point-like stars.** Stars are drawn with the Moffat profile astronomers model real point sources
  with: a tight peak with power-law wings. Brightness therefore shows as intensity and halo rather than
  as a growing disc. Diffraction spikes are available per layer but off by default.

Two behaviours are worth knowing about:

- **Seamless by default.** The left and right edges join exactly, so the image loops as a scrolling
  backdrop. Stars that straddle the seam are drawn on both sides, and every noise field wraps to a whole
  number of cells. Turn it off with `--no-seamless`.
- **Reproducible always.** The same seed and options produce the same image, byte for byte, on any
  machine, at any band size and any thread count.

## Parallax layers

`--layers <dir>` writes each layer again on its own as a transparent PNG, ready to be scrolled at
different speeds in a game engine:

```sh
starfield -w 4096 -h 1024 --layers ./planes -o composite.png
```

That produces `composite.png` plus `composite-00-nebula.png`, `composite-01-far.png` and so on. The
numbering matches the composite order, so sorting the file names gives you back-to-front.

## Presets

Every setting lives in one JSON file. Write out the current settings, edit them, and render from them:

```sh
starfield --dump-preset my-field.json
starfield --preset my-field.json -o field.png
```

A preset only needs the settings it wants to change; everything it leaves out keeps its default.
Command-line options are applied on top of the preset, so `--preset x.json --seed 7` works as expected.
The [starfield library reference](src/Starfield.Core/README.md) documents what each setting does.

## Large images

Image size costs time and disk, not memory. Rendering walks the image in horizontal bands, and each band
is encoded and streamed into the PNG as soon as it is done, so nothing ever holds the whole image.
Measured on a 16-core desktop, in a Release build:

| Image | Time | File |
| --- | --- | --- |
| 1600×400 | 0.4 s | 0.6 MB |
| 10000×1080 | 2.5 s | 8.4 MB |
| 40000×2160 | 20 s | 66 MB |
| 100000×1080 (`--compression fastest`) | 30 s | 151 MB |

Three options tune the trade-off:

- `--band-height <rows>` sets how many rows a band covers. Larger bands are slightly faster and use
  proportionally more memory.
- `--memory <MiB>` caps the memory held by in-flight bands, 512 MiB by default. When a very wide image
  would push past the cap, fewer bands run at once instead. The pixels never change; only the wall clock
  does.
- `--compression <level>` trades file size for encode time. `fastest` is worth using once images pass
  about 50 megapixels.

## How it is put together

Five projects, each one layer more specific than the last. Dependencies only ever point from specific
to general.

| Project | Role |
| --- | --- |
| [`src/Imaging.Core`](src/Imaging.Core/README.md) | Application-neutral imaging machinery: streaming PNG encoder, tiling noise, colour maths, banded render loop. Knows nothing about stars. |
| [`src/Starfield.Core`](src/Starfield.Core/README.md) | The star and nebula layers, and the options model that describes them. Plugs into the imaging core's layer interface. |
| [`src/Starfield.Cli`](src/Starfield.Cli/README.md) | Argument parsing, preset files, console logging. The executable, named `starfield`. |
| [`src/Starfield.App`](src/Starfield.App/README.md) | The front end's view models and in-process rendering. No UI toolkit dependency, so it is unit-tested as plain objects. |
| [`src/Starfield.Ui`](src/Starfield.Ui/README.md) | The Avalonia desktop window over `Starfield.App`. |

The seam between the first two is `ILayerRenderer`: a layer must be able to render any horizontal slice
of itself and produce exactly the pixels it would have produced rendering the whole image at once. That
one rule is what makes banding, parallelism and reproducibility possible at the same time.

## Releases

Pushing a tag that starts with `v` builds a release. The workflow in `.github/workflows/release.yml`
builds and tests the solution, then publishes self-contained single-file executables of both the
command line and the desktop app for Windows (x64), Linux (x64) and macOS (Apple Silicon and Intel),
and attaches one archive per platform to a GitHub release named after the tag:

```sh
git tag v1.0.0
git push origin v1.0.0
```

Each archive holds `starfield` and `starfield-ui` with the .NET runtime bundled, so nothing needs
installing. That makes them large, about 70 MB and 100 MB, which is the price of not asking users to
install .NET. Trimming is left off on purpose: the preset format and the window's bindings use
reflection.

## Tests

```sh
dotnet test StarfieldGenerator.slnx
```

The suites are described in [tests/README.md](tests/README.md). The load-bearing ones assert that band
height and thread count cannot change a single pixel of the output.

## Repository layout

| Path | Contents |
| --- | --- |
| `src/` | The five projects above. |
| `tests/` | Four test projects plus the shared test-support library. |
| `Directory.Build.props` | Repo-wide build settings, including the warnings-as-errors gate. |
| `.github/workflows/` | The release workflow, triggered by version tags. |
| `.editorconfig` | Analyzer rules, code style and naming. The source of truth for both. |
| `AGENTS.md` | The working agreement for changes made here. |
