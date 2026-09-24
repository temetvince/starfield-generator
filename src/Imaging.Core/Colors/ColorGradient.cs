using System.Collections.Immutable;

namespace Imaging.Core.Colors;

/// <summary>One anchor of a <see cref="ColorGradient"/>.</summary>
/// <param name="Position">Where the stop sits along the gradient, normally in <c>[0, 1]</c>.</param>
/// <param name="Colour">The linear-light colour at that position.</param>
public readonly record struct GradientStop(float Position, LinearRgb Colour);

/// <summary>
/// A piecewise-linear ramp through linear-light colours, sampled by position.
/// </summary>
/// <remarks>
/// Instances are immutable and safe to sample from many threads at once, which matters because every
/// band worker samples the same gradient concurrently. Interpolation happens in linear light, so a ramp
/// through saturated colours keeps its brightness instead of dipping in the middle.
/// </remarks>
public sealed class ColorGradient
{
    private readonly ImmutableArray<GradientStop> _stops;

    /// <summary>Creates a gradient from at least one stop.</summary>
    /// <param name="stops">
    /// The stops, in any order. They are copied and sorted by position, so the caller's collection may
    /// change afterwards without affecting the gradient.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="stops"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="stops"/> is empty.</exception>
    public ColorGradient(IEnumerable<GradientStop> stops)
    {
        ArgumentNullException.ThrowIfNull(stops);

        _stops = [.. stops.OrderBy(stop => stop.Position)];

        if (_stops.Length == 0)
        {
            throw new ArgumentException("A gradient needs at least one stop.", nameof(stops));
        }
    }

    /// <summary>Gets the number of stops the gradient was built from.</summary>
    /// <value>Always at least one.</value>
    public int StopCount => _stops.Length;

    /// <summary>Samples the ramp.</summary>
    /// <param name="position">
    /// Position along the gradient. Positions before the first stop or after the last one clamp to that
    /// stop rather than extrapolating.
    /// </param>
    /// <returns>The interpolated colour.</returns>
    public LinearRgb Sample(float position)
    {
        if (position <= _stops[0].Position)
        {
            return _stops[0].Colour;
        }

        var last = _stops[^1];
        if (position >= last.Position)
        {
            return last.Colour;
        }

        for (var i = 1; i < _stops.Length; i++)
        {
            var upper = _stops[i];
            if (position > upper.Position)
            {
                continue;
            }

            var lower = _stops[i - 1];
            var span = upper.Position - lower.Position;
            var amount = span <= 0.0f ? 0.0f : (position - lower.Position) / span;
            return LinearRgb.Lerp(lower.Colour, upper.Colour, amount);
        }

        return last.Colour;
    }
}
