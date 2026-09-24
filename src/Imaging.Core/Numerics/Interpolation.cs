namespace Imaging.Core.Numerics;

/// <summary>Small blending helpers used throughout shading code.</summary>
public static class Interpolation
{
    /// <summary>Blends linearly between two values.</summary>
    /// <param name="start">The value returned at <paramref name="amount"/> zero.</param>
    /// <param name="end">The value returned at <paramref name="amount"/> one.</param>
    /// <param name="amount">Blend position. Values outside <c>[0, 1]</c> extrapolate.</param>
    /// <returns>The blended value.</returns>
    public static float Lerp(float start, float end, float amount) => start + ((end - start) * amount);

    /// <summary>Maps a value onto a smooth zero-to-one ramp between two edges.</summary>
    /// <param name="edge0">Where the ramp reaches zero.</param>
    /// <param name="edge1">Where the ramp reaches one. Equal edges make the ramp a hard step.</param>
    /// <param name="value">The value to map.</param>
    /// <returns>
    /// A value in <c>[0, 1]</c>, flat outside the edges and following the cubic <c>3t² - 2t³</c> between
    /// them, so the result has no visible kink where it meets the flat parts.
    /// </returns>
    public static float Smoothstep(float edge0, float edge1, float value)
    {
        var span = edge1 - edge0;
        if (span <= 0.0f)
        {
            return value < edge0 ? 0.0f : 1.0f;
        }

        var t = Math.Clamp((value - edge0) / span, 0.0f, 1.0f);
        return t * t * (3.0f - (2.0f * t));
    }
}
