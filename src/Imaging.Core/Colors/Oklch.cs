namespace Imaging.Core.Colors;

/// <summary>
/// <see cref="Oklab"/> in polar form: lightness, chroma and hue, which is the form a gradient between two
/// colours wants, since hue can then travel the short way round the wheel instead of through grey.
/// </summary>
/// <param name="L">Perceived lightness. Zero is black and one is the display white point.</param>
/// <param name="C">Chroma: how colourful the colour is. Not negative; zero is grey.</param>
/// <param name="H">Hue, in turns of the colour wheel, in <c>[0, 1)</c>. Meaningless when <paramref name="C"/> is zero.</param>
public readonly record struct Oklch(float L, float C, float H)
{
    /// <summary>Converts from the rectangular form.</summary>
    /// <param name="colour">The colour to convert.</param>
    /// <returns>The same colour in polar form, with hue normalised to <c>[0, 1)</c>.</returns>
    public static Oklch FromOklab(Oklab colour)
    {
        var chroma = colour.Chroma;
        var hue = MathF.Atan2(colour.B, colour.A) / MathF.Tau;
        return new Oklch(colour.L, chroma, hue < 0.0f ? hue + 1.0f : hue);
    }

    /// <summary>Converts to the rectangular form.</summary>
    /// <returns>The same colour as <see cref="Oklab"/>.</returns>
    public Oklab ToOklab()
    {
        var angle = H * MathF.Tau;
        return new Oklab(L, C * MathF.Cos(angle), C * MathF.Sin(angle));
    }

    /// <summary>Blends between two colours, taking the hue the short way round the wheel.</summary>
    /// <param name="start">The colour returned at <paramref name="amount"/> zero.</param>
    /// <param name="end">The colour returned at <paramref name="amount"/> one.</param>
    /// <param name="amount">Blend position, normally in <c>[0, 1]</c>.</param>
    /// <returns>
    /// The blend. Lightness and chroma move linearly. Hue moves along the shorter arc, and when either
    /// end is grey, whose hue is meaningless, the other end's hue is used throughout so the blend does
    /// not spin through arbitrary colours.
    /// </returns>
    public static Oklch Lerp(Oklch start, Oklch end, float amount)
    {
        const float GreyChroma = 1e-4f;

        var startHue = start.C < GreyChroma ? end.H : start.H;
        var endHue = end.C < GreyChroma ? start.H : end.H;

        var delta = endHue - startHue;
        if (delta > 0.5f)
        {
            delta -= 1.0f;
        }
        else if (delta < -0.5f)
        {
            delta += 1.0f;
        }

        var hue = startHue + (delta * amount);
        hue -= MathF.Floor(hue);

        return new Oklch(
            start.L + ((end.L - start.L) * amount),
            start.C + ((end.C - start.C) * amount),
            hue);
    }
}
