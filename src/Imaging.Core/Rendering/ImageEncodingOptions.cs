using Imaging.Core.Colors;
using Imaging.Core.Png;
using static System.FormattableString;

namespace Imaging.Core.Rendering;

/// <summary>Chooses how accumulated light becomes stored pixels.</summary>
public enum AlphaMode
{
    /// <summary>Composite over the background colour and store no alpha. The result is an RGB file.</summary>
    Opaque = 0,

    /// <summary>
    /// Derive alpha from how much light landed and store colour unpremultiplied. The result is an RGBA
    /// file that composites back over black to exactly the opaque render, which is what a parallax layer
    /// needs.
    /// </summary>
    FromLight = 1,
}

/// <summary>
/// The final stage of the pipeline: exposure, tone curve, background, dithering and alpha.
/// </summary>
/// <remarks>
/// These settings deliberately live apart from the layers. The same accumulated light can be encoded as
/// an opaque backdrop or as a transparent parallax plane without re-rendering anything.
/// </remarks>
public sealed record ImageEncodingOptions
{
    /// <summary>Gets the light added under every pixel before tone mapping.</summary>
    /// <value>
    /// Black by default. Ignored when <see cref="AlphaMode"/> is <see cref="AlphaMode.FromLight"/>,
    /// because a transparent layer must not bake a backdrop into itself.
    /// </value>
    public LinearRgb Background { get; init; } = LinearRgb.Black;

    /// <summary>Gets the tone curve applied after exposure.</summary>
    /// <value>The filmic curve by default, which keeps colour in bright star cores.</value>
    public ToneMappingCurve ToneMapping { get; init; } = ToneMappingCurve.Filmic;

    /// <summary>Gets the multiplier applied to accumulated light before the tone curve.</summary>
    /// <value>Not negative. One renders as authored.</value>
    public float Exposure { get; init; } = 1.0f;

    /// <summary>Gets how strongly quantisation is dithered.</summary>
    /// <value>
    /// Not negative, in byte units. One is a good default: it hides the flat contour rings that a wide,
    /// slowly varying nebula would otherwise show once 8-bit quantisation lands. Zero disables it, which
    /// is what deterministic pixel-comparison tests want.
    /// </value>
    public float DitherStrength { get; init; } = 1.0f;

    /// <summary>Gets whether the encoder stores alpha.</summary>
    /// <value>Opaque by default.</value>
    public AlphaMode AlphaMode { get; init; } = AlphaMode.Opaque;

    /// <summary>Gets the seed used to place dither noise.</summary>
    /// <value>Any value. Two encodes of the same render with the same seed are byte-identical.</value>
    public ulong DitherSeed { get; init; }

    /// <summary>Gets the PNG colour type implied by <see cref="AlphaMode"/>.</summary>
    /// <value><see cref="PngColorType.Rgba"/> when alpha is derived, otherwise <see cref="PngColorType.Rgb"/>.</value>
    public PngColorType ColorType => AlphaMode == AlphaMode.FromLight ? PngColorType.Rgba : PngColorType.Rgb;

    /// <summary>Reports why these options cannot be used, if they cannot.</summary>
    /// <returns>A human-readable list of problems, empty when the options are valid.</returns>
    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();

        if (!(Exposure >= 0.0f))
        {
            problems.Add(Invariant($"Exposure must not be negative but was {Exposure}."));
        }

        if (!(DitherStrength >= 0.0f))
        {
            problems.Add(Invariant($"DitherStrength must not be negative but was {DitherStrength}."));
        }

        return problems;
    }
}
