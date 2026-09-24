namespace Imaging.Core.Colors;

/// <summary>
/// Converts a black-body temperature to a colour, which is what gives stars their believable tints.
/// </summary>
/// <remarks>
/// The fit is the widely used piecewise approximation of the Planckian locus. It is accurate enough for
/// imagery and cheap enough for a per-star call; it is not a colorimetric reference.
/// </remarks>
public static class Blackbody
{
    /// <summary>The coldest temperature the fit is defined for.</summary>
    public const float MinimumKelvin = 1000.0f;

    /// <summary>The hottest temperature the fit is defined for.</summary>
    public const float MaximumKelvin = 40000.0f;

    /// <summary>
    /// Returns the linear-light colour of a black body, normalised so its brightest component is one.
    /// </summary>
    /// <param name="kelvin">
    /// Temperature in kelvin. Values are clamped to
    /// <see cref="MinimumKelvin"/>..<see cref="MaximumKelvin"/>.
    /// </param>
    /// <returns>
    /// A colour whose largest component is exactly one, so callers scale by their own intensity without
    /// the temperature secretly changing how bright the result is.
    /// </returns>
    public static LinearRgb FromTemperature(float kelvin)
    {
        var t = Math.Clamp(kelvin, MinimumKelvin, MaximumKelvin) / 100.0f;

        var red = t <= 66.0f
            ? 255.0f
            : 329.698727446f * MathF.Pow(t - 60.0f, -0.1332047592f);

        var green = t <= 66.0f
            ? (99.4708025861f * MathF.Log(t)) - 161.1195681661f
            : 288.1221695283f * MathF.Pow(t - 60.0f, -0.0755148492f);

        var blue = t >= 66.0f
            ? 255.0f
            : t <= 19.0f
                ? 0.0f
                : (138.5177312231f * MathF.Log(t - 10.0f)) - 305.0447927307f;

        var linear = new LinearRgb(
            Srgb.ToLinear(Math.Clamp(red, 0.0f, 255.0f) / 255.0f),
            Srgb.ToLinear(Math.Clamp(green, 0.0f, 255.0f) / 255.0f),
            Srgb.ToLinear(Math.Clamp(blue, 0.0f, 255.0f) / 255.0f));

        var peak = linear.MaxComponent;
        return peak > 0.0f ? linear * (1.0f / peak) : LinearRgb.White;
    }
}
