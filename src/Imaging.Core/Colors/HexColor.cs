using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Imaging.Core.Colors;

/// <summary>
/// Parses and formats the <c>#RRGGBB</c> colour notation that configuration files use.
/// </summary>
/// <remarks>
/// The notation is sRGB-encoded, which is what a colour picker shows. Parsing therefore decodes to
/// linear light, and formatting encodes back, so a value survives a round trip through a preset file.
/// </remarks>
public static class HexColor
{
    /// <summary>Parses an sRGB hex colour into linear light.</summary>
    /// <param name="text">
    /// The notation to parse. A leading <c>#</c> is optional, and both the three-digit shorthand
    /// (<c>#abc</c>) and the six-digit form (<c>#aabbcc</c>) are accepted, case-insensitively.
    /// </param>
    /// <param name="colour">The parsed colour, or <see cref="LinearRgb.Black"/> when parsing fails.</param>
    /// <returns><see langword="true"/> when <paramref name="text"/> was a valid colour.</returns>
    public static bool TryParse([NotNullWhen(true)] string? text, out LinearRgb colour)
    {
        colour = LinearRgb.Black;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var digits = text.AsSpan().Trim();
        if (digits.Length > 0 && digits[0] == '#')
        {
            digits = digits[1..];
        }

        return digits.Length == 3
            ? TryParseComponents(digits, 1, out colour)
            : digits.Length == 6 && TryParseComponents(digits, 2, out colour);
    }

    /// <summary>Parses an sRGB hex colour, throwing when the notation is malformed.</summary>
    /// <param name="text">The notation to parse, in the forms accepted by <see cref="TryParse"/>.</param>
    /// <returns>The parsed colour in linear light.</returns>
    /// <exception cref="FormatException"><paramref name="text"/> is not a valid colour.</exception>
    public static LinearRgb Parse(string? text)
        => TryParse(text, out var colour)
            ? colour
            : throw new FormatException($"'{text}' is not a hex colour such as #7A5FFF.");

    /// <summary>Formats a linear-light colour as <c>#RRGGBB</c>.</summary>
    /// <param name="colour">The colour to format. Components outside <c>[0, 1]</c> clamp.</param>
    /// <returns>The sRGB hex notation, in upper case, with a leading <c>#</c>.</returns>
    public static string ToHex(LinearRgb colour)
    {
        var red = Srgb.Quantise(Srgb.FromLinear(colour.R), 0.0f);
        var green = Srgb.Quantise(Srgb.FromLinear(colour.G), 0.0f);
        var blue = Srgb.Quantise(Srgb.FromLinear(colour.B), 0.0f);
        return string.Create(CultureInfo.InvariantCulture, $"#{red:X2}{green:X2}{blue:X2}");
    }

    private static bool TryParseComponents(ReadOnlySpan<char> digits, int width, out LinearRgb colour)
    {
        colour = LinearRgb.Black;

        Span<float> channels = stackalloc float[3];
        for (var channel = 0; channel < 3; channel++)
        {
            var slice = digits.Slice(channel * width, width);
            if (!byte.TryParse(slice, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsed))
            {
                return false;
            }

            // The shorthand repeats each digit, so 'a' means 0xAA rather than 0x0A.
            var expanded = width == 1 ? (parsed * 16) + parsed : parsed;
            channels[channel] = Srgb.ToLinear(expanded / 255.0f);
        }

        colour = new LinearRgb(channels[0], channels[1], channels[2]);
        return true;
    }
}
