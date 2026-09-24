using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Starfield.Ui.Converters;

/// <summary>
/// Converts between the form's <c>#RRGGBB</c> text and Avalonia's colour type, so the pickers can bind
/// to a view model that knows nothing about Avalonia.
/// </summary>
public sealed class HexColorConverter : IValueConverter
{
    /// <inheritdoc/>
    /// <returns>The colour for valid hex text; otherwise black, so the picker always has something to show.</returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string text && Color.TryParse(text.Trim(), out var colour) ? colour : Colors.Black;

    /// <inheritdoc/>
    /// <returns>The colour as upper-case <c>#RRGGBB</c>, alpha dropped, since the nebula ramp is opaque.</returns>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Color colour
            ? string.Create(CultureInfo.InvariantCulture, $"#{colour.R:X2}{colour.G:X2}{colour.B:X2}")
            : "";
}
