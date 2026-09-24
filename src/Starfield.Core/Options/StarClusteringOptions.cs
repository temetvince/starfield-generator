using Imaging.Core.Colors;
using Imaging.Core.Noise;
using static System.FormattableString;

namespace Starfield.Core.Options;

/// <summary>
/// Where stars gather. Two structures shape the whole field: a broad galactic band, and clumps of
/// varying scale scattered through it.
/// </summary>
/// <remarks>
/// <para>
/// A sky of evenly scattered stars reads as static, not as space. Real skies have a bright band across
/// them and star clouds within it. These settings describe both, as a multiplier on every layer's
/// density: the band is a soft ridge running the width of the image, and the clumping field breaks that
/// ridge into clouds and voids.
/// </para>
/// <para>
/// Each star layer decides how much of this it follows, through
/// <see cref="StarLayerOptions.ClusteringResponse"/>.
/// </para>
/// <para>
/// Turning clustering on changes how many stars an image holds, because the settings multiply each
/// layer's density rather than redistributing a fixed count. A field with the defaults below averages
/// roughly its layer densities; raise <see cref="Floor"/> to lift the empty regions.
/// </para>
/// </remarks>
public sealed record StarClusteringOptions
{
    /// <summary>Gets whether stars are clustered at all.</summary>
    /// <value><see langword="true"/> by default. When false, every layer is spread evenly.</value>
    public bool Enabled { get; init; } = true;

    /// <summary>Gets the density multiplier where neither the band nor a clump reaches.</summary>
    /// <value>
    /// Not negative. Zero empties the voids completely, which looks artificial; around <c>0.25</c>
    /// leaves a thin scattering of stars everywhere.
    /// </value>
    public float Floor { get; init; } = 0.28f;

    /// <summary>Gets how much density the galactic band adds at its centre line.</summary>
    /// <value>Not negative. Zero removes the band and leaves only clumping.</value>
    public float BandStrength { get; init; } = 1.5f;

    /// <summary>Gets where the band sits vertically.</summary>
    /// <value>A fraction of the image height, where zero is the top and one the bottom.</value>
    public float BandCentre { get; init; } = 0.45f;

    /// <summary>Gets how far the band reaches from its centre line before fading out.</summary>
    /// <value>
    /// A fraction of the image height, greater than zero. Around <c>0.3</c> gives a band that fills
    /// much of a wide strip while leaving darker sky above and below.
    /// </value>
    public float BandWidth { get; init; } = 0.32f;

    /// <summary>Gets how far the band's centre line meanders as it crosses the image.</summary>
    /// <value>
    /// A fraction of the image height, not negative. This is what stops the band from being a straight
    /// horizontal stripe. The meander is driven by noise rather than a tilt, which is what lets a
    /// seamless image still join at the edges.
    /// </value>
    public float BandWobble { get; init; } = 0.22f;

    /// <summary>Gets the noise field that breaks the band into clouds and voids.</summary>
    /// <value>
    /// Never <see langword="null"/>. Low frequencies give a few large star clouds; higher ones scatter
    /// many small ones.
    /// </value>
    public FractalNoiseOptions Clumping { get; init; } = new()
    {
        BaseFrequency = 4.0f,
        Octaves = 4,
        Gain = 0.55f,
        Shape = FractalNoiseShape.Brownian,
    };

    /// <summary>Gets how much density the clumping field adds at its peaks.</summary>
    /// <value>Not negative. Zero leaves a smooth band with no cloud structure inside it.</value>
    public float ClumpStrength { get; init; } = 1.1f;

    /// <summary>Gets the exponent applied to the clumping field.</summary>
    /// <value>
    /// Greater than zero. Values above one push the clouds apart, giving tighter knots of stars with
    /// emptier gaps between them.
    /// </value>
    public float ClumpContrast { get; init; } = 1.8f;

    /// <summary>Gets the noise field that carves dark dust lanes through the band.</summary>
    /// <value>
    /// Never <see langword="null"/>. The ridged shape is what makes lanes rather than blotches: it turns
    /// the field's zero crossings into the long thin filaments that dust actually forms.
    /// </value>
    public FractalNoiseOptions Dust { get; init; } = new()
    {
        BaseFrequency = 5.0f,
        Octaves = 5,
        Gain = 0.5f,
        Shape = FractalNoiseShape.Ridged,
    };

    /// <summary>Gets how much of the light a dust lane blocks at its darkest.</summary>
    /// <value>
    /// In <c>[0, 1]</c>. Zero removes the lanes. Dust dims the haze and thins the stars together, since
    /// it sits in front of both.
    /// </value>
    public float DustStrength { get; init; } = 0.7f;

    /// <summary>Gets the level below which the dust field is clear.</summary>
    /// <value>
    /// In <c>[0, 1)</c>. Raising it narrows the lanes into a few sharp rifts; lowering it spreads dust
    /// across the whole band.
    /// </value>
    public float DustThreshold { get; init; } = 0.52f;

    /// <summary>Gets how brightly the unresolved stars glow.</summary>
    /// <value>
    /// Not negative. Zero renders no haze at all. This is the diffuse light of stars too faint and too
    /// crowded to draw individually, and it is what makes a galactic band read as a band rather than as
    /// a patch of slightly denser dots.
    /// </value>
    public float HazeIntensity { get; init; } = 0.2f;

    /// <summary>Gets the colour of the unresolved-star glow.</summary>
    /// <value>
    /// An sRGB hex string. A pale, barely warm white suits it: the glow is the average of a great many
    /// stars, so it is far less colourful than any single one.
    /// </value>
    public string HazeColor { get; init; } = "#9AA0C8";

    /// <summary>Gets the exponent applied to the haze before it is drawn.</summary>
    /// <value>
    /// Greater than zero. Values above one keep the glow tight around the densest regions instead of
    /// washing the whole frame.
    /// </value>
    public float HazeContrast { get; init; } = 2.0f;

    /// <summary>Gets the largest multiplier these settings can produce.</summary>
    /// <value>
    /// The sum of the floor and both structures at full strength. Star placement uses it as the ceiling
    /// to sample against, so it must be a true upper bound rather than a typical value.
    /// </value>
    public float MaximumMultiplier => Floor + BandStrength + ClumpStrength;

    /// <summary>Reports why these options cannot be used, if they cannot.</summary>
    /// <returns>A human-readable list of problems, empty when the options are valid.</returns>
    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();

        if (!(Floor >= 0.0f))
        {
            problems.Add(Invariant($"Clustering Floor must not be negative but was {Floor}."));
        }

        if (!(BandStrength >= 0.0f))
        {
            problems.Add(Invariant($"Clustering BandStrength must not be negative but was {BandStrength}."));
        }

        if (float.IsNaN(BandCentre))
        {
            problems.Add("Clustering BandCentre must be a number.");
        }

        if (!(BandWidth > 0.0f))
        {
            problems.Add(Invariant($"Clustering BandWidth must be greater than 0 but was {BandWidth}."));
        }

        if (!(BandWobble >= 0.0f))
        {
            problems.Add(Invariant($"Clustering BandWobble must not be negative but was {BandWobble}."));
        }

        if (!(ClumpStrength >= 0.0f))
        {
            problems.Add(Invariant($"Clustering ClumpStrength must not be negative but was {ClumpStrength}."));
        }

        if (!(ClumpContrast > 0.0f))
        {
            problems.Add(Invariant($"Clustering ClumpContrast must be greater than 0 but was {ClumpContrast}."));
        }

        if (DustStrength is < 0.0f or > 1.0f || float.IsNaN(DustStrength))
        {
            problems.Add(Invariant($"Clustering DustStrength must be in [0, 1] but was {DustStrength}."));
        }

        if (DustThreshold is < 0.0f or >= 1.0f || float.IsNaN(DustThreshold))
        {
            problems.Add(Invariant($"Clustering DustThreshold must be in [0, 1) but was {DustThreshold}."));
        }

        if (!(HazeIntensity >= 0.0f))
        {
            problems.Add(Invariant($"Clustering HazeIntensity must not be negative but was {HazeIntensity}."));
        }

        if (!(HazeContrast > 0.0f))
        {
            problems.Add(Invariant($"Clustering HazeContrast must be greater than 0 but was {HazeContrast}."));
        }

        if (!HexColor.TryParse(HazeColor, out _))
        {
            problems.Add(Invariant($"Clustering HazeColor '{HazeColor}' is not a hex colour such as #9AA0C8."));
        }

        if (!(MaximumMultiplier > 0.0f))
        {
            problems.Add("Clustering leaves no density anywhere: Floor, BandStrength and ClumpStrength are all zero.");
        }

        problems.AddRange(Clumping.Validate());
        problems.AddRange(Dust.Validate());
        return problems;
    }
}
