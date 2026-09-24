namespace Imaging.Core.Png;

/// <summary>The PNG colour types this library writes, using the values from the PNG specification.</summary>
public enum PngColorType
{
    /// <summary>Three 8-bit channels, no alpha. Three bytes per pixel.</summary>
    Rgb = 2,

    /// <summary>Three 8-bit channels plus 8-bit alpha. Four bytes per pixel.</summary>
    Rgba = 6,
}
