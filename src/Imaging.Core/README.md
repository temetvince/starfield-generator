# Imaging.Core

Application-neutral imaging machinery. It knows how to stream a PNG, sample tiling noise, do colour
maths and drive a banded render loop — and nothing at all about stars, nebulae or any other subject.
Anything in here would be equally useful to a terrain generator or a texture baker.

The only runtime dependency is `Microsoft.Extensions.Logging.Abstractions`. PNG encoding uses
`System.IO.Compression` from the base library, so there is no third-party imaging dependency and no
licence to consider.

## The central idea

Image size is a cost in time and disk, never in memory. Rendering walks the image in horizontal bands.
Each band is rendered, tone-mapped, encoded and streamed into the PNG before the next one starts, so
peak memory depends on the row length and the band height, not on the image area.

That only works if a layer produces identical pixels regardless of how the image was divided. That
requirement is `ILayerRenderer`, and every other design choice here follows from it: randomness is
derived from coordinates rather than drawn from a running generator, noise is a pure function of
position, and dithering is keyed to absolute image coordinates.

## File map

| Area | Types | Purpose |
| --- | --- | --- |
| `Png/` | `PngStreamWriter`, `PngColorType`, `Crc32` | Row-at-a-time PNG encoder with adaptive filtering and chunked IDAT output. |
| `Noise/` | `TilingPerlinNoise`, `FractalNoise`, `FractalNoiseOptions`, `FractalNoiseShape` | Gradient noise that can repeat exactly across a chosen period. |
| `Colors/` | `LinearRgb`, `Oklab`, `Oklch`, `Srgb`, `Blackbody`, `ColorGradient`, `HexColor`, `ToneMapper` | Linear-light colour type and the conversions around it. |
| `Numerics/` | `Hash64`, `DeterministicRandom`, `Interpolation` | Reproducible hashing, a small seeded generator, and blending helpers. |
| `Rendering/` | `BandedImageRenderer`, `ILayerRenderer`, `RgbBandBuffer`, `BandRegion`, `ImageSize`, `BandEncoder`, `BandedRenderOptions`, `ImageEncodingOptions` | The band loop and the pipeline from accumulated light to stored bytes. |
| `Log.cs` | `Log` | Source-generated log messages. |

## Rendering pipeline

1. `BandedImageRenderer.Render` divides the image into bands and allocates one `RgbBandBuffer` and one
   encode buffer per worker.
2. Workers render bands in parallel. For each band, every `ILayerRenderer` adds its light into the
   buffer, in composite order.
3. `BandEncoder` applies exposure, the tone curve, the background and dithering, then writes the band's
   bytes. This runs on the same worker that rendered the band.
4. The main thread writes the finished bands to `PngStreamWriter` in strict top-to-bottom order.

Only step 4 is serialised. Steps 2 and 3 scale with the worker count.

## Working in linear light

Layers add light; they do not paint over each other. Accumulated values routinely exceed one — a star
core can reach the hundreds — and that is deliberate. Clamping early would flatten every bright core to
the same white disc. The single conversion to displayable range happens in `BandEncoder`, in this order:

1. Add `ImageEncodingOptions.Background`, unless the output is a transparent layer.
2. Multiply by `Exposure`, then apply the `ToneMappingCurve`.
3. Encode to sRGB, add the dither offset, quantise to a byte.

`AlphaMode.FromLight` changes the last step: alpha becomes the largest encoded channel and colour is
divided by it. Compositing such a layer over black reproduces the opaque render exactly, which is what
makes exported parallax planes line up with the composite.

## Seamless noise

`TilingPerlinNoise.Sample` takes a period in lattice cells. When one is given, lattice columns wrap
modulo that period, so the value at `x` and at `x + period` are identical by construction rather than by
blending two renders together.

`FractalNoise` builds on that. It samples in *turns of the image width*: `x` runs from zero at the left
edge to one at the right, and `y` uses the same scale, so features stay round rather than stretching on
a wide image. Each octave's period is a whole number of cells, and each octave gets its own lattice so
that octaves do not line up into visible grid artefacts.

## Memory

Peak memory is roughly `width × BandHeight × 16 bytes × workers`: twelve bytes of float accumulation
plus three or four bytes of encoded output per pixel. `BandedRenderOptions.MaxWorkingSetBytes` caps the
total, 512 MiB by default, by reducing the worker count rather than the band height. Parallelism never
drops below one worker, so the cap slows a render rather than failing it.

## Reproducibility rules for new layers

Anything implementing `ILayerRenderer` must hold to these, or banded rendering will produce seams:

- Derive randomness from absolute image coordinates, through `Hash64`. Never carry a generator between
  calls.
- Measure distances from pixel centres, at `x + 0.5` and `y + 0.5`. A shape whose reach is tested
  against row indices instead will lose its outermost row at band boundaries.
- Treat any position outside the band as clipped, not as an error. `RgbBandBuffer.Add` drops
  out-of-range writes so that a shape overhanging a band needs no special case.
- Keep all shared state read-only. Bands render concurrently on different threads.
