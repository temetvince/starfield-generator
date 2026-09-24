using static System.FormattableString;

namespace Imaging.Core.Noise;

/// <summary>Selects how each octave is folded into the fractal sum.</summary>
public enum FractalNoiseShape
{
    /// <summary>Plain fractional Brownian motion. Soft, cloud-like, symmetric about the midpoint.</summary>
    Brownian = 0,

    /// <summary>Absolute value inverted, which sharpens the zero crossings into filaments and ridges.</summary>
    Ridged = 1,

    /// <summary>Absolute value kept, which turns the field into billowing puffs with dark seams.</summary>
    Billow = 2,
}

/// <summary>
/// Shape of a fractal noise field: how many octaves, how fast they shrink, and how they are folded.
/// </summary>
/// <remarks>
/// Instances are immutable. The defaults are a general-purpose cloud field; every property may be
/// overridden with an object initialiser.
/// </remarks>
public sealed record FractalNoiseOptions
{
    /// <summary>Gets the number of octaves summed.</summary>
    /// <value>Between 1 and 16. Each extra octave adds finer detail and costs one more noise sample per pixel.</value>
    public int Octaves { get; init; } = 5;

    /// <summary>Gets how many lattice cells the first octave spans across the image width.</summary>
    /// <value>
    /// At least one. This is a count across the full width rather than a pixel frequency, which is what
    /// lets the field tile: the period is always a whole number of cells.
    /// </value>
    public float BaseFrequency { get; init; } = 3.0f;

    /// <summary>Gets the frequency multiplier applied per octave.</summary>
    /// <value>Greater than one. Two is the conventional choice.</value>
    public float Lacunarity { get; init; } = 2.0f;

    /// <summary>Gets the amplitude multiplier applied per octave.</summary>
    /// <value>
    /// In <c>(0, 1]</c>. Values near <c>0.5</c> give natural-looking clouds; higher values make the fine
    /// detail as strong as the coarse shape.
    /// </value>
    public float Gain { get; init; } = 0.5f;

    /// <summary>Gets how the octaves are folded together.</summary>
    /// <value>The fold applied to each octave before summing.</value>
    public FractalNoiseShape Shape { get; init; } = FractalNoiseShape.Brownian;

    /// <summary>Gets whether the field repeats horizontally across the image width.</summary>
    /// <value>
    /// <see langword="true"/> to wrap every octave to a whole number of cells, which makes the left and
    /// right edges of the image join seamlessly.
    /// </value>
    public bool SeamlessX { get; init; } = true;

    /// <summary>Gets the distance along y at which the field repeats, in the same units as x.</summary>
    /// <value>
    /// Zero, the default, for no vertical repeat. A positive value makes every octave wrap after a
    /// whole number of lattice cells that spans this distance, with the cells stretched by at most
    /// half a cell to make the count whole. A renderer that samples in turns of the image width passes
    /// the image's height divided by its width.
    /// </value>
    public float VerticalPeriod { get; init; }

    /// <summary>Reports why these options cannot be used, if they cannot.</summary>
    /// <returns>A human-readable list of problems, empty when the options are valid.</returns>
    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();

        if (Octaves is < 1 or > 16)
        {
            problems.Add(Invariant($"Octaves must be between 1 and 16 but was {Octaves}."));
        }

        if (!(BaseFrequency >= 1.0f))
        {
            problems.Add(Invariant($"BaseFrequency must be at least 1 but was {BaseFrequency}."));
        }

        if (!(Lacunarity > 1.0f))
        {
            problems.Add(Invariant($"Lacunarity must be greater than 1 but was {Lacunarity}."));
        }

        if (Gain is <= 0.0f or > 1.0f)
        {
            problems.Add(Invariant($"Gain must be in (0, 1] but was {Gain}."));
        }

        if (!(VerticalPeriod >= 0.0f))
        {
            problems.Add(Invariant($"VerticalPeriod must not be negative but was {VerticalPeriod}."));
        }

        return problems;
    }
}
