using Imaging.Core.Colors;
using static System.FormattableString;

namespace Starfield.Core.Options;

/// <summary>
/// One anchor of a nebula's colour ramp, expressed the way a preset file spells it.
/// </summary>
/// <remarks>
/// This is the configuration-facing twin of <see cref="GradientStop"/>. It keeps colours as hex text so
/// a preset stays readable and hand-editable, and converts to linear light only when a renderer is built.
/// </remarks>
public sealed record ColorStopOptions
{
    /// <summary>Gets where the stop sits along the ramp.</summary>
    /// <value>
    /// In <c>[0, 1]</c>, where zero is the faintest edge of a cloud and one is its densest core.
    /// </value>
    public float Position { get; init; }

    /// <summary>Gets the colour at this position.</summary>
    /// <value>An sRGB hex string such as <c>#7A5FFF</c>. The leading hash is optional.</value>
    public string Color { get; init; } = "#FFFFFF";

    /// <summary>Converts this stop to the renderer's form.</summary>
    /// <returns>The equivalent gradient stop in linear light.</returns>
    /// <exception cref="FormatException"><see cref="Color"/> is not a hex colour.</exception>
    public GradientStop ToGradientStop() => new(Position, HexColor.Parse(Color));

    /// <summary>Reports why this stop cannot be used, if it cannot.</summary>
    /// <returns>A human-readable list of problems, empty when the stop is valid.</returns>
    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();

        if (Position is < 0.0f or > 1.0f || float.IsNaN(Position))
        {
            problems.Add(Invariant($"Colour stop Position must be in [0, 1] but was {Position}."));
        }

        if (!HexColor.TryParse(Color, out _))
        {
            problems.Add(Invariant($"Colour stop Color '{Color}' is not a hex colour such as #7A5FFF."));
        }

        return problems;
    }
}
