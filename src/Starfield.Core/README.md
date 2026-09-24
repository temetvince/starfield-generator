# Starfield.Core

The subject matter: what a star looks like, what a nebula looks like, and the options model that
describes both. It plugs into [`Imaging.Core`](../Imaging.Core/README.md) through `ILayerRenderer` and
adds no machinery of its own — no file formats, no threading, no encoding.

## File map

| Area | Types | Purpose |
| --- | --- | --- |
| `StarfieldRenderer.cs` | `StarfieldRenderer` | The entry point. Builds layers from options and renders composites or per-layer files. |
| `Options/` | `StarfieldOptions`, `NebulaOptions`, `NebulaPaletteOptions`, `NebulaPalettes`, `StarClusteringOptions`, `StarLayerOptions`, `SpikeOptions`, `ColorStopOptions`, `StarfieldPresets` | Immutable configuration, each part able to report its own problems. |
| `Rendering/` | `StarCellField`, `StarDensityField`, `Star`, `StarLayerRenderer`, `StarHazeLayerRenderer`, `NebulaLayerRenderer` | The three layer kinds, the star placement grid and the clustering field they share. |
| `Log.cs` | `Log` | Source-generated log messages. |

## Using it

```csharp
var options = StarfieldPresets.Default() with { Width = 10000, Height = 1080, Seed = 42 };
var renderer = new StarfieldRenderer(options, loggerFactory);

renderer.RenderToFile("wide.png");
renderer.RenderLayerFiles("./planes", "wide");
```

`StarfieldOptions` is an immutable record, so customising means a `with` expression rather than
mutation. The constructor validates and throws; nothing is left to fail halfway through a render.

## How stars are placed

A star field cannot be a list of stars if the image might be 100000 pixels wide, and a band cannot ask
"which stars are near me?" of a list it never built. `StarCellField` solves this with a fixed grid of
roughly 64-pixel cells:

1. Each cell derives a random stream from the seed, the layer index and its own coordinates.
2. The stream decides how many stars the cell holds, then draws each one's position, brightness,
   radius, temperature and spike angle — always in that order, always for every star, so culling can
   never knock the stream out of step.
3. A band asks for the cells overlapping its rows, widened by `MaxInfluenceRadius`, the furthest any
   star in the layer can reach from its own centre.

Two consequences of that design are worth stating plainly:

- The margin must bound the *largest star the layer can actually produce*, jitter included. If it does
  not, a band skips the cell holding an over-sized star and loses the part of its glow that reached
  inside, which shows up as a faint seam exactly on the band boundary.
- Overlap is tested against pixel centres, at `row + 0.5`, because that is where shading measures
  distance from. A star centred just past a band's last row can still light that row.

Cells one ring outside the image are generated too, so stars whose centres sit just off the top or
bottom edge still cast their glow inward and the edges do not thin out.

## How stars cluster

Stars are not spread evenly. `StarDensityField` computes a density multiplier from four parts, and every
star layer is placed against it:

| Part | Effect |
| --- | --- |
| Band | A soft ridge across the image, the galactic plane. Its centre line meanders under a noise field. |
| Clumps | A fractal field that breaks the band into star clouds and voids. Concentrated on the band, but not confined to it. |
| Dust | A ridged field that darkens lanes through the band, dimming haze and thinning stars together. |
| Floor | The multiplier where none of the above reaches, so the empty sky is thin rather than bare. |

Two design points are worth stating:

- **The centre line meanders rather than tilts.** A tilted line would arrive at the right-hand edge at a
  different height than it left the left-hand edge, breaking a seamless image at exactly the join it is
  meant to hide. Noise with the image's period cannot do that.
- **Clustering is applied by rejection, not by scaling counts.** Each cell offers candidates at the
  highest density the field can reach, and each candidate survives in proportion to the density where it
  landed. Scaling the count per cell instead would make density change in visible 64-pixel steps. It also
  means `StarClusteringOptions.MaximumMultiplier` must be a true upper bound: an underestimate would
  silently clip the densest regions.

Because rejection depends only on a candidate's own position, clustering does not disturb band
independence — a sliced render still produces exactly the stars a whole-image render would.

Each layer chooses how much of this it follows through `ClusteringResponse`. Distant populations trace
the band closely; a sparse foreground layer looks better nearly uniform.

## How a star is drawn

Each star is a continuous falloff evaluated per pixel, never a drawn disc, so sub-pixel stars land as
partly lit pixels and nothing aliases into squares. Three terms add together:

- **Core.** A Moffat profile, `(1 + r²/CoreRadius²)^-FalloffExponent`. This is the standard model for a
  point source, and the choice matters: a Gaussian of the same width saturates into a flat disc with a
  hard rim, which is what makes rendered stars read as shiny balls rather than points of light. The
  Moffat's sharp peak and power-law wings keep a bright star small and let it fade smoothly instead.
- **Aureole.** The same profile widened and flattened, scaled by `GlowStrength`: the faint scattered
  light only the brightest stars show. Keep it low; much above `0.1` it reads as lens flare.
- **Spikes.** Off by default. For stars above `SpikeOptions.BrightnessThreshold`, arms spread evenly
  around the star, falling off exponentially along their length and as a Gaussian across their width.

Radii sit near a pixel, so brightness shows as intensity and halo rather than as size — which is how a
real star behaves.

## Unresolved stars

Below about a pixel of separation the eye reads a crowd of dots as a smooth glow, and rendering each of
them costs everything and adds nothing. `StarHazeLayerRenderer` supplies that glow directly from the
same density field the individual stars are placed against, so the two always agree about where the
crowd is. This is what makes a galactic band look like one rather than like a patch of slightly denser
dots.

Colour comes from `Blackbody.FromTemperature`, pulled toward neutral by `Saturation`, then multiplied
by the star's brightness. Brightness itself is `u^IntensityExponent` over the layer's range, which is
what produces a believable population of many faint stars and a few blazing ones. Radius tracks
brightness, with a jitter of ±20% so a layer does not look stamped from one template.

When `SeamlessX` is set, a star whose reach crosses an image edge is emitted twice, once on each side.

## How the nebula is drawn

Three noise fields, each sampled per pixel in turns of the image width:

| Field | Job | Typical shape |
| --- | --- | --- |
| `Coverage` | Decides which regions of the image have any cloud at all. | Low frequency, Brownian |
| `Structure` | Carves filaments inside those regions. | Higher frequency, ridged |
| `Warp` | Displaces the structure field so filaments curl instead of running straight. | Low frequency, Brownian |

Coverage matters most on a wide image: without it a panorama is uniform haze rather than nebulae with
clear sky between them. The pipeline per pixel is coverage → mask, warp → displaced position, structure
→ density, density → colour from the chosen palette's `ColorStops` and brightness. Position zero on
the ramp is the faint outer rim and position one the dense core.

Colour is chosen per seed from `Palettes`, a list of named, hand-authored ramps. The seed picks one
palette, then nudges its hues by up to half of `HueVariation` either way, so two seeds that draw the
same palette still differ a little. The nudge is a perceptual rotation through `Oklab`, which keeps a
soft colour soft at every hue. Listing a single palette pins every seed to that ramp; `NebulaPalettes`
holds the shipped set, which deliberately leaves out muddy yellow-greens.

`BandAffinity` then weights the coverage mask by the galactic band. Real nebulae lie in the galactic
plane, and following it is what keeps the clouds and the star band reading as one structure instead of
two unrelated pictures sharing a frame.

Warping preserves seamlessness. The warp field repeats with the image width, so displacing by it cannot
break the structure field's own repeat.

## Options and validation

Every options type exposes `Validate()`, which returns a list of human-readable problems and is empty
when the type is usable. Parents call into their children, so `StarfieldOptions.Validate` reports
everything wrong with a configuration in one pass instead of failing on the first problem.

`StarfieldPresets.Default()` returns the shipped look: nebulae along a galactic band, the haze of
unresolved stars, and three star layers named `far`, `mid` and `near`. The layers exist to give the
field depth. `far` is dense, faint and small and reads as distance; `mid` carries most of the visible
stars; `near` is sparse and bright and supplies the few anchors the eye lands on. Their clustering
response falls off with prominence, so the faint layer traces the band closely while the bright
foreground layer stays nearly uniform.

## Per-layer output

`RenderLayerFiles` renders each layer again on its own with `AlphaMode.FromLight`, producing transparent
PNGs named `{stem}-{index}-{layer}.png`. The index matches composite order, so the natural sort order is
back to front. Each file carries no background, so compositing them in order over the background
reproduces the opaque render.
