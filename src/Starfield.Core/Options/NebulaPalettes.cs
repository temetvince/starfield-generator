using Imaging.Core.Colors;

namespace Starfield.Core.Options;

/// <summary>
/// The shipped nebula palettes: a spread of colour families that each read as a believable cloud.
/// </summary>
/// <remarks>
/// Every ramp follows the same shape, a dark low-saturation rim rising through two saturated mid tones
/// to a bright, near-white core, so palettes differ in hue rather than in how the cloud is lit. Muddy
/// yellow-greens are left out on purpose: against black they read as sickly rather than luminous.
/// </remarks>
public static class NebulaPalettes
{
    private static readonly IReadOnlyList<NebulaPaletteOptions> Shipped =
    [
        Ramp("violet-dusk", "#0A1A4A", "#3B2A7A", "#8B3A86", "#D46A6A", "#FFD9A0"),
        Ramp("crimson-ember", "#260A1C", "#6A1230", "#B8324A", "#EA7A58", "#FFDDB4"),
        Ramp("teal-lagoon", "#061C2C", "#0E4A5A", "#1F8C8A", "#6CC4B0", "#E8FFF4"),
        Ramp("amber-dust", "#2A1508", "#6A3A10", "#B8702A", "#E8B060", "#FFF0C8"),
        Ramp("cobalt-ice", "#060E30", "#12307A", "#2A6AC0", "#7AB0E8", "#EAF4FF"),
        Ramp("rose-quartz", "#240C2C", "#5A1A5E", "#A83A8C", "#E070B0", "#FFE2F2"),
        Ramp("sea-gold", "#08221E", "#10554A", "#3A9A78", "#C8B860", "#FFF4C0"),
        Ramp("indigo-coral", "#0C0C34", "#2A2A7A", "#4A6AB8", "#E08A70", "#FFE0C0"),
        Ramp("ultraviolet-flame", "#0A1030", "#4A1A50", "#B03040", "#F07040", "#FFE8C0"),
        Ramp("lavender-mist", "#1A1030", "#4A3A8A", "#8A80C8", "#C8D8E8", "#FFFFF4"),
    ];

    /// <summary>Gets the shipped palettes.</summary>
    /// <returns>The same immutable list on every call, with at least one entry.</returns>
    public static IReadOnlyList<NebulaPaletteOptions> Default() => Shipped;

    /// <summary>Builds a palette that runs from one colour at the rim to another at the core.</summary>
    /// <param name="name">The palette's name.</param>
    /// <param name="rim">The colour of the faint outer edge.</param>
    /// <param name="core">The colour of the dense centre.</param>
    /// <param name="stopCount">How many evenly spaced stops to write. At least two.</param>
    /// <returns>
    /// A valid palette whose first stop is <paramref name="rim"/> and last is <paramref name="core"/>,
    /// with the stops between blended in <see cref="Oklch"/>, so the hue travels the short way round the
    /// wheel and the blend never dips through grey. Colours outside the display gamut clamp.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="stopCount"/> is less than two.</exception>
    public static NebulaPaletteOptions Between(string name, LinearRgb rim, LinearRgb core, int stopCount = 5)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfLessThan(stopCount, 2);

        var start = Oklch.FromOklab(Oklab.FromLinear(rim));
        var end = Oklch.FromOklab(Oklab.FromLinear(core));
        var stops = new List<ColorStopOptions>(stopCount);

        for (var index = 0; index < stopCount; index++)
        {
            var position = (float)index / (stopCount - 1);
            var colour = Oklch.Lerp(start, end, position).ToOklab().ToLinear();
            stops.Add(new ColorStopOptions { Position = position, Color = HexColor.ToHex(colour) });
        }

        return new NebulaPaletteOptions { Name = name, ColorStops = stops };
    }

    private static NebulaPaletteOptions Ramp(string name, string rim, string outer, string inner, string bright, string core) => new()
    {
        Name = name,
        ColorStops =
        [
            new ColorStopOptions { Position = 0.0f, Color = rim },
            new ColorStopOptions { Position = 0.35f, Color = outer },
            new ColorStopOptions { Position = 0.65f, Color = inner },
            new ColorStopOptions { Position = 0.85f, Color = bright },
            new ColorStopOptions { Position = 1.0f, Color = core },
        ],
    };
}
