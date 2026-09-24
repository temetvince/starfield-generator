namespace Imaging.Core.Colors;

/// <summary>Selects the curve that maps unbounded linear light into displayable range.</summary>
public enum ToneMappingCurve
{
    /// <summary>Clip anything above white. Cheapest, and the harshest on bright cores.</summary>
    Clamp = 0,

    /// <summary>The <c>x / (1 + x)</c> curve. Never quite reaches white, so highlights stay soft.</summary>
    Reinhard = 1,

    /// <summary>The <c>1 - e^-x</c> curve. Reaches white faster than Reinhard with a gentle shoulder.</summary>
    Exponential = 2,

    /// <summary>The filmic ACES-style curve. Keeps saturation in bright cores instead of blowing them to white.</summary>
    Filmic = 3,
}

/// <summary>
/// Applies exposure and a tone curve, turning accumulated emissive light into displayable colour.
/// </summary>
/// <remarks>
/// Emissive rendering produces values far above one: a star core can be hundreds. Tone mapping is what
/// makes that legible instead of a field of identical white discs, so it runs once per pixel just
/// before encoding, never during accumulation.
/// </remarks>
public static class ToneMapper
{
    /// <summary>Applies exposure then the chosen curve to a colour.</summary>
    /// <param name="colour">Accumulated linear light. Components may exceed one.</param>
    /// <param name="curve">The curve to apply.</param>
    /// <param name="exposure">
    /// Multiplier applied before the curve, which must not be negative. One leaves the render as
    /// authored; larger values brighten it.
    /// </param>
    /// <returns>A colour with every component in <c>[0, 1]</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="exposure"/> is negative.</exception>
    public static LinearRgb Apply(LinearRgb colour, ToneMappingCurve curve, float exposure)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(exposure);

        var exposed = colour * exposure;
        return new LinearRgb(
            ApplyChannel(exposed.R, curve),
            ApplyChannel(exposed.G, curve),
            ApplyChannel(exposed.B, curve));
    }

    private static float ApplyChannel(float value, ToneMappingCurve curve)
    {
        if (value <= 0.0f)
        {
            return 0.0f;
        }

        var mapped = curve switch
        {
            ToneMappingCurve.Reinhard => value / (1.0f + value),
            ToneMappingCurve.Exponential => 1.0f - MathF.Exp(-value),
            ToneMappingCurve.Filmic => Filmic(value),
            ToneMappingCurve.Clamp => value,
            _ => value,
        };

        return MathF.Min(mapped, 1.0f);
    }

    private static float Filmic(float value)
    {
        // Narkowicz's fit of the ACES filmic curve.
        const float ShoulderStrength = 2.51f;
        const float LinearStrength = 0.03f;
        const float LinearAngle = 2.43f;
        const float ToeStrength = 0.59f;
        const float ToeNumerator = 0.14f;

        var numerator = value * ((ShoulderStrength * value) + LinearStrength);
        var denominator = (value * ((LinearAngle * value) + ToeStrength)) + ToeNumerator;
        return numerator / denominator;
    }
}
