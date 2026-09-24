namespace Imaging.Core.Colors;

/// <summary>
/// A colour in the OKLab perceptual space, where equal distances look equally different to the eye.
/// </summary>
/// <param name="L">Perceived lightness. Zero is black and one is the display white point.</param>
/// <param name="A">Green-to-red axis. Negative is green, positive is red; zero, with <paramref name="B"/> zero, is neutral.</param>
/// <param name="B">Blue-to-yellow axis. Negative is blue, positive is yellow.</param>
/// <remarks>
/// <para>
/// Operations that should read as "the same colour, turned" or "the same colour, calmer" go wrong in
/// linear RGB: a rotation about the grey axis there sends a soft magenta to a neon lime. Doing them
/// here and converting back keeps lightness and colourfulness as the eye sees them, which is what
/// lets a generated palette stay tasteful for every input.
/// </para>
/// <para>
/// The space is Björn Ottosson's OKLab. The conversions are exact to float precision for colours
/// inside the display gamut; a colour outside it converts back with negative components clamped away.
/// </para>
/// </remarks>
public readonly record struct Oklab(float L, float A, float B)
{
    /// <summary>Gets how colourful the colour is: its distance from the neutral axis.</summary>
    /// <value>Not negative. Zero for any grey.</value>
    public float Chroma => MathF.Sqrt((A * A) + (B * B));

    /// <summary>Converts a linear-light colour to OKLab.</summary>
    /// <param name="colour">The colour to convert. Components above one are legal and give a lightness above one.</param>
    /// <returns>The same colour in OKLab.</returns>
    public static Oklab FromLinear(LinearRgb colour)
    {
        var l = MathF.Cbrt((0.4122214708f * colour.R) + (0.5363325363f * colour.G) + (0.0514459929f * colour.B));
        var m = MathF.Cbrt((0.2119034982f * colour.R) + (0.6806995451f * colour.G) + (0.1073969566f * colour.B));
        var s = MathF.Cbrt((0.0883024619f * colour.R) + (0.2817188376f * colour.G) + (0.6299787005f * colour.B));

        return new Oklab(
            (0.2104542553f * l) + (0.7936177850f * m) - (0.0040720468f * s),
            (1.9779984951f * l) - (2.4285922050f * m) + (0.4505937099f * s),
            (0.0259040371f * l) + (0.7827717662f * m) - (0.8086757660f * s));
    }

    /// <summary>Converts this colour back to linear light.</summary>
    /// <returns>
    /// The colour in linear light. A component that the display cannot show, because the colour is more
    /// saturated than the gamut allows at its hue, clamps to zero rather than going negative.
    /// </returns>
    public LinearRgb ToLinear()
    {
        var l = Cube(L + (0.3963377774f * A) + (0.2158037573f * B));
        var m = Cube(L - (0.1055613458f * A) - (0.0638541728f * B));
        var s = Cube(L - (0.0894841775f * A) - (1.2914855480f * B));

        return new LinearRgb(
            MathF.Max(0.0f, (4.0767416621f * l) - (3.3077115913f * m) + (0.2309699292f * s)),
            MathF.Max(0.0f, (-1.2684380046f * l) + (2.6097574011f * m) - (0.3413193965f * s)),
            MathF.Max(0.0f, (-0.0041960863f * l) - (0.7034186147f * m) + (1.7076147010f * s)));
    }

    /// <summary>Rotates the hue, keeping lightness and chroma exactly.</summary>
    /// <param name="turns">
    /// How far to rotate, in turns of the colour wheel. Zero and any whole number return the colour
    /// unchanged; a half turn gives the opposite hue.
    /// </param>
    /// <returns>The rotated colour.</returns>
    public Oklab RotateHue(float turns)
    {
        var angle = turns * MathF.Tau;
        var cos = MathF.Cos(angle);
        var sin = MathF.Sin(angle);
        return new Oklab(L, (A * cos) - (B * sin), (A * sin) + (B * cos));
    }

    private static float Cube(float value) => value * value * value;
}
