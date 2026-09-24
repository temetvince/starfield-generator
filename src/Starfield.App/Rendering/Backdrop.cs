using Starfield.Core.Options;

namespace Starfield.App.Rendering;

/// <summary>
/// The sky drawn behind the window: a modest render of the built-in look, dimmed so the controls read.
/// </summary>
public static class Backdrop
{
    /// <summary>Gets the backdrop's width in pixels.</summary>
    public const int Width = 1920;

    /// <summary>Gets the backdrop's height in pixels.</summary>
    public const int Height = 1080;

    /// <summary>Describes the backdrop for one seed.</summary>
    /// <param name="seed">The seed, so each launch can show a different sky.</param>
    /// <returns>Valid options for a screen-sized, non-tiling, slightly darkened field.</returns>
    public static StarfieldOptions Options(ulong seed) => StarfieldPresets.Default() with
    {
        Width = Width,
        Height = Height,
        Seed = seed,
        SeamlessX = false,
        Exposure = 0.7f,
    };
}
