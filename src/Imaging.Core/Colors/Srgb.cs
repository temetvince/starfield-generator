namespace Imaging.Core.Colors;

/// <summary>
/// Conversions between linear light and the sRGB encoding that a PNG file stores.
/// </summary>
/// <remarks>
/// Rendering adds light, so it happens in linear space. Files store a perceptual encoding. Mixing the
/// two up is what makes generated imagery look either washed out or crushed, so every conversion in
/// this library goes through here.
/// </remarks>
public static class Srgb
{
    /// <summary>Decodes an sRGB-encoded component to linear light.</summary>
    /// <param name="encoded">Encoded component, normally in <c>[0, 1]</c>.</param>
    /// <returns>The linear-light component.</returns>
    public static float ToLinear(float encoded)
        => encoded <= 0.04045f
            ? encoded / 12.92f
            : MathF.Pow((encoded + 0.055f) / 1.055f, 2.4f);

    /// <summary>Encodes a linear-light component to sRGB.</summary>
    /// <param name="linear">Linear-light component. Negative values clamp to zero.</param>
    /// <returns>The encoded component, unclamped above one so callers can decide how to handle overshoot.</returns>
    public static float FromLinear(float linear)
    {
        return linear <= 0.0f
            ? 0.0f
            : linear <= 0.0031308f
            ? linear * 12.92f
            : (1.055f * MathF.Pow(linear, 1.0f / 2.4f)) - 0.055f;
    }

    /// <summary>Decodes a stored byte to linear light.</summary>
    /// <param name="encoded">The stored channel byte.</param>
    /// <returns>The linear-light component in <c>[0, 1]</c>.</returns>
    public static float ByteToLinear(byte encoded) => ToLinear(encoded / 255.0f);

    /// <summary>
    /// Quantises an sRGB-encoded component to a stored byte, offsetting it first by a dither amount.
    /// </summary>
    /// <param name="encoded">Encoded component. Values outside <c>[0, 1]</c> clamp.</param>
    /// <param name="dither">
    /// Offset in byte units, normally in <c>[-1, 1]</c>, that breaks up the flat steps a smooth gradient
    /// would otherwise show. Pass zero for exact quantisation.
    /// </param>
    /// <returns>The stored byte.</returns>
    public static byte Quantise(float encoded, float dither)
    {
        var scaled = (encoded * 255.0f) + dither;
        return scaled <= 0.0f ? (byte)0
            : scaled >= 255.0f ? (byte)255
            : (byte)(scaled + 0.5f);
    }
}
