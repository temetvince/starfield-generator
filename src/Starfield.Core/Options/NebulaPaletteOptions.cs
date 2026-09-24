namespace Starfield.Core.Options;

/// <summary>
/// One named colour ramp a nebula may be painted with, from its faint rim to its dense core.
/// </summary>
/// <remarks>
/// A nebula picks one palette per seed from the list in <see cref="NebulaOptions.Palettes"/>. Keeping
/// every palette hand-authored is what guarantees each seed lands on a ramp somebody chose to look
/// good, rather than on whatever a formula produced.
/// </remarks>
public sealed record NebulaPaletteOptions
{
    /// <summary>Gets the palette's name, used in logs so a render can say which ramp it chose.</summary>
    /// <value>Not empty or whitespace.</value>
    public string Name { get; init; } = "unnamed";

    /// <summary>Gets the colour ramp applied across cloud density.</summary>
    /// <value>
    /// At least one stop. Position zero is the faint outer edge and position one the dense core, so a
    /// ramp from a cool rim to a warm centre is what makes a cloud read as three-dimensional.
    /// </value>
    public IReadOnlyList<ColorStopOptions> ColorStops { get; init; } = [];

    /// <summary>Reports why this palette cannot be used, if it cannot.</summary>
    /// <returns>A human-readable list of problems, empty when the palette is valid.</returns>
    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();

        if (string.IsNullOrWhiteSpace(Name))
        {
            problems.Add("Nebula palette Name must not be empty.");
        }

        if (ColorStops.Count == 0)
        {
            problems.Add($"Nebula palette '{Name}' must contain at least one colour stop.");
        }

        foreach (var stop in ColorStops)
        {
            problems.AddRange(stop.Validate());
        }

        return problems;
    }
}
