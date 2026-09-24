using System.IO.Compression;
using Imaging.Core.Colors;
using Imaging.Core.Rendering;
using static System.FormattableString;

namespace Starfield.Core.Options;

/// <summary>
/// Everything needed to render one star field: image size, seed, look and output tuning.
/// </summary>
/// <remarks>
/// <para>
/// The record is the whole contract between a preset file and the renderer. It is immutable, so a
/// render cannot change the options it was handed, and it round-trips through JSON without loss.
/// </para>
/// <para>
/// Call <see cref="Validate"/> before rendering. The renderer rejects invalid options rather than
/// guessing at what was meant.
/// </para>
/// </remarks>
public sealed record StarfieldOptions
{
    /// <summary>Gets the image width in pixels.</summary>
    /// <value>
    /// Positive. Very wide values are the point of the design: cost grows with area, not with peak
    /// memory.
    /// </value>
    public int Width { get; init; } = 10000;

    /// <summary>Gets the image height in pixels.</summary>
    /// <value>Positive.</value>
    public int Height { get; init; } = 1080;

    /// <summary>Gets the seed every random decision derives from.</summary>
    /// <value>
    /// Any value. The same seed and options always produce the same image, on any machine and at any
    /// band size or thread count.
    /// </value>
    public ulong Seed { get; init; } = 1;

    /// <summary>Gets whether the left and right edges join seamlessly.</summary>
    /// <value>
    /// <see langword="true"/> to make the image tile horizontally, which is what a scrolling parallax
    /// backdrop needs. Stars that straddle the seam are drawn on both sides, and every noise field wraps
    /// to a whole number of cells.
    /// </value>
    public bool SeamlessX { get; init; } = true;

    /// <summary>Gets whether the top and bottom edges join seamlessly as well.</summary>
    /// <value>
    /// <see langword="true"/> by default, which makes the image a tile that repeats in both directions:
    /// every noise field also wraps to a whole number of cells vertically, stars that straddle the top or
    /// bottom are drawn on both sides, and the galactic band is measured around the tile so it becomes
    /// a ring rather than stopping at the edge. <see langword="false"/> leaves the top and bottom as
    /// plain edges, which keeps the full vertical detail of the noise on a very wide, short image.
    /// </value>
    public bool SeamlessY { get; init; } = true;

    /// <summary>Gets the colour behind everything.</summary>
    /// <value>
    /// An sRGB hex string. Near-black rather than pure black usually looks better, since deep space in
    /// an image is rarely absolute zero.
    /// </value>
    public string Background { get; init; } = "#02030A";

    /// <summary>Gets the tone curve applied to accumulated light.</summary>
    /// <value>The filmic curve by default.</value>
    public ToneMappingCurve ToneMapping { get; init; } = ToneMappingCurve.Filmic;

    /// <summary>Gets the exposure multiplier applied before tone mapping.</summary>
    /// <value>Not negative. This is the one dial to reach for when a whole image is too dim or too hot.</value>
    public float Exposure { get; init; } = 1.0f;

    /// <summary>Gets how strongly 8-bit quantisation is dithered.</summary>
    /// <value>
    /// Not negative, in byte units. Leave it at one for nebulae: their gradients are exactly the case
    /// where banding shows.
    /// </value>
    public float DitherStrength { get; init; } = 1.0f;

    /// <summary>Gets how many image rows each render band covers.</summary>
    /// <value>
    /// At least one. This trades memory for throughput and has no effect on the pixels produced.
    /// </value>
    public int BandHeight { get; init; } = 64;

    /// <summary>Gets how many bands render at once.</summary>
    /// <value>Zero, the default, uses one worker per logical processor.</value>
    public int MaxDegreeOfParallelism { get; init; }

    /// <summary>Gets the ceiling on memory held by in-flight bands.</summary>
    /// <value>
    /// In bytes, 512 MiB by default; zero removes the ceiling. On a very wide image this is what keeps a
    /// high processor count from turning into gigabytes of scratch buffers, by rendering fewer bands at
    /// once instead.
    /// </value>
    public long MaxWorkingSetBytes { get; init; } = 512L * 1024 * 1024;

    /// <summary>Gets how hard the PNG encoder compresses.</summary>
    /// <value>
    /// <see cref="CompressionLevel.Optimal"/> by default;
    /// <see cref="CompressionLevel.Fastest"/> is noticeably quicker on very large images.
    /// </value>
    public CompressionLevel CompressionLevel { get; init; } = CompressionLevel.Optimal;

    /// <summary>Gets the nebula settings.</summary>
    /// <value>Never <see langword="null"/>. Set <see cref="NebulaOptions.Enabled"/> to false for stars alone.</value>
    public NebulaOptions Nebula { get; init; } = new();

    /// <summary>Gets the settings that gather stars into a galactic band and star clouds.</summary>
    /// <value>
    /// Never <see langword="null"/>. Applies to every star layer, in proportion to each layer's
    /// <see cref="StarLayerOptions.ClusteringResponse"/>.
    /// </value>
    public StarClusteringOptions Clustering { get; init; } = new();

    /// <summary>Gets the star populations, rendered in order.</summary>
    /// <value>
    /// May be empty for a nebula with no stars. Names must be unique, because they become file names
    /// when layers are exported separately.
    /// </value>
    public IReadOnlyList<StarLayerOptions> StarLayers { get; init; } = [];

    /// <summary>Gets the image dimensions as a validated pair.</summary>
    /// <value>The size built from <see cref="Width"/> and <see cref="Height"/>.</value>
    /// <exception cref="ArgumentOutOfRangeException">Either dimension is not positive.</exception>
    public ImageSize Size => new(Width, Height);

    /// <summary>Builds the renderer-facing options for one output file.</summary>
    /// <param name="alphaMode">
    /// Whether the output is an opaque composite or a transparent layer. Transparent output ignores
    /// <see cref="Background"/>, so an exported layer carries no baked-in backdrop.
    /// </param>
    /// <returns>Band, parallelism and encoding settings for <see cref="BandedImageRenderer"/>.</returns>
    /// <exception cref="FormatException"><see cref="Background"/> is not a hex colour.</exception>
    public BandedRenderOptions ToRenderOptions(AlphaMode alphaMode) => new()
    {
        BandHeight = BandHeight,
        MaxDegreeOfParallelism = MaxDegreeOfParallelism,
        MaxWorkingSetBytes = MaxWorkingSetBytes,
        CompressionLevel = CompressionLevel,
        Encoding = new ImageEncodingOptions
        {
            Background = HexColor.Parse(Background),
            ToneMapping = ToneMapping,
            Exposure = Exposure,
            DitherStrength = DitherStrength,
            AlphaMode = alphaMode,
            DitherSeed = Seed,
        },
    };

    /// <summary>Reports why these options cannot be used, if they cannot.</summary>
    /// <returns>A human-readable list of problems, empty when the options are valid.</returns>
    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();

        if (Width < 1)
        {
            problems.Add(Invariant($"Width must be at least 1 but was {Width}."));
        }

        if (Height < 1)
        {
            problems.Add(Invariant($"Height must be at least 1 but was {Height}."));
        }

        if (!HexColor.TryParse(Background, out _))
        {
            problems.Add(Invariant($"Background '{Background}' is not a hex colour such as #02030A."));
        }

        if (!(Exposure >= 0.0f))
        {
            problems.Add(Invariant($"Exposure must not be negative but was {Exposure}."));
        }

        if (!(DitherStrength >= 0.0f))
        {
            problems.Add(Invariant($"DitherStrength must not be negative but was {DitherStrength}."));
        }

        if (BandHeight < 1)
        {
            problems.Add(Invariant($"BandHeight must be at least 1 but was {BandHeight}."));
        }

        if (MaxDegreeOfParallelism < 0)
        {
            problems.Add(Invariant($"MaxDegreeOfParallelism must not be negative but was {MaxDegreeOfParallelism}."));
        }

        if (MaxWorkingSetBytes < 0)
        {
            problems.Add(Invariant($"MaxWorkingSetBytes must not be negative but was {MaxWorkingSetBytes}."));
        }

        problems.AddRange(Nebula.Validate());
        problems.AddRange(Clustering.Validate());

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var layer in StarLayers)
        {
            problems.AddRange(layer.Validate());

            if (!names.Add(layer.Name))
            {
                problems.Add(Invariant($"Star layer name '{layer.Name}' is used more than once."));
            }
        }

        return problems;
    }
}
