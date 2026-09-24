namespace Imaging.Core.Colors;

/// <summary>
/// A colour with linear-light components, which is the space all compositing in this library happens in.
/// </summary>
/// <param name="R">Red component. Zero is black; one is the display white point. Values above one are legal and mean "brighter than white" until tone mapping brings them back into range.</param>
/// <param name="G">Green component, on the same scale as <paramref name="R"/>.</param>
/// <param name="B">Blue component, on the same scale as <paramref name="R"/>.</param>
/// <remarks>
/// Components are deliberately unclamped. Emissive sources such as stars add up far past one, and
/// clamping early would flatten every bright core to the same white disc. Clamping happens once, at
/// encode time, after tone mapping.
/// </remarks>
public readonly record struct LinearRgb(float R, float G, float B)
{
    /// <summary>Gets the colour with all components at zero.</summary>
    /// <value>Black, the identity for addition.</value>
    public static LinearRgb Black => default;

    /// <summary>Gets the colour with all components at one.</summary>
    /// <value>The display white point.</value>
    public static LinearRgb White => new(1.0f, 1.0f, 1.0f);

    /// <summary>Gets the relative luminance of the colour.</summary>
    /// <value>The Rec. 709 luminance, which is negative only if a component is negative.</value>
    public float Luminance => (0.2126f * R) + (0.7152f * G) + (0.0722f * B);

    /// <summary>Gets the largest component, which is what alpha extraction keys off.</summary>
    /// <value>The maximum of the three components.</value>
    public float MaxComponent => MathF.Max(R, MathF.Max(G, B));

    /// <summary>Creates a neutral colour with all three components at the same level.</summary>
    /// <param name="level">The shared component level.</param>
    /// <returns>A grey (or, above one, an over-bright white) at that level.</returns>
    public static LinearRgb FromLevel(float level) => new(level, level, level);

    /// <summary>Adds two colours component-wise, which is how emissive light accumulates.</summary>
    /// <param name="left">First colour.</param>
    /// <param name="right">Second colour.</param>
    /// <returns>The component-wise sum.</returns>
    public static LinearRgb operator +(LinearRgb left, LinearRgb right)
        => new(left.R + right.R, left.G + right.G, left.B + right.B);

    /// <summary>Scales a colour uniformly.</summary>
    /// <param name="colour">The colour to scale.</param>
    /// <param name="scale">The multiplier. Negative values are legal but subtract light when added.</param>
    /// <returns>The scaled colour.</returns>
    public static LinearRgb operator *(LinearRgb colour, float scale)
        => new(colour.R * scale, colour.G * scale, colour.B * scale);

    /// <summary>Multiplies two colours component-wise, which is how a tint or mask is applied.</summary>
    /// <param name="left">First colour.</param>
    /// <param name="right">Second colour.</param>
    /// <returns>The component-wise product.</returns>
    public static LinearRgb operator *(LinearRgb left, LinearRgb right)
        => new(left.R * right.R, left.G * right.G, left.B * right.B);

    /// <summary>Named alternative to <see cref="op_Addition(LinearRgb, LinearRgb)"/>.</summary>
    /// <param name="left">First colour.</param>
    /// <param name="right">Second colour.</param>
    /// <returns>The component-wise sum.</returns>
    public static LinearRgb Add(LinearRgb left, LinearRgb right) => left + right;

    /// <summary>Named alternative to <see cref="op_Multiply(LinearRgb, float)"/>.</summary>
    /// <param name="colour">The colour to scale.</param>
    /// <param name="scale">The multiplier.</param>
    /// <returns>The scaled colour.</returns>
    public static LinearRgb Multiply(LinearRgb colour, float scale) => colour * scale;

    /// <summary>Named alternative to <see cref="op_Multiply(LinearRgb, LinearRgb)"/>.</summary>
    /// <param name="left">First colour.</param>
    /// <param name="right">Second colour.</param>
    /// <returns>The component-wise product.</returns>
    public static LinearRgb Multiply(LinearRgb left, LinearRgb right) => left * right;

    /// <summary>Blends linearly between two colours.</summary>
    /// <param name="start">The colour returned at <paramref name="amount"/> zero.</param>
    /// <param name="end">The colour returned at <paramref name="amount"/> one.</param>
    /// <param name="amount">Blend position. Values outside <c>[0, 1]</c> extrapolate.</param>
    /// <returns>The blended colour.</returns>
    public static LinearRgb Lerp(LinearRgb start, LinearRgb end, float amount)
        => new(
            start.R + ((end.R - start.R) * amount),
            start.G + ((end.G - start.G) * amount),
            start.B + ((end.B - start.B) * amount));

    /// <summary>Pulls a colour toward its own luminance, reducing how colourful it is.</summary>
    /// <param name="saturation">
    /// Saturation factor. Zero returns the neutral grey of the same luminance, one returns the colour
    /// unchanged, and values above one push past the original saturation.
    /// </param>
    /// <returns>The desaturated or over-saturated colour.</returns>
    public LinearRgb WithSaturation(float saturation) => Lerp(FromLevel(Luminance), this, saturation);

    /// <summary>Rotates the colour's hue as the eye sees it, keeping its lightness and colourfulness.</summary>
    /// <param name="turns">
    /// How far to rotate, in turns of the colour wheel. Zero and any whole number return the colour
    /// unchanged; a half turn gives the opposite hue.
    /// </param>
    /// <returns>
    /// The rotated colour. The rotation happens in <see cref="Oklab"/>, so a soft colour stays soft
    /// at every hue instead of turning neon where the RGB cube is wide. Greys are unchanged. A
    /// component the display gamut cannot hold at the new hue clamps to zero.
    /// </returns>
    public LinearRgb RotateHue(float turns) => Oklab.FromLinear(this).RotateHue(turns).ToLinear();
}
