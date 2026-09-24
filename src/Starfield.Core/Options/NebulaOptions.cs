using Imaging.Core.Noise;
using static System.FormattableString;

namespace Starfield.Core.Options;

/// <summary>
/// The gas clouds behind the stars: where they are, how they are shaped and what colour they glow.
/// </summary>
/// <remarks>
/// <para>
/// Three noise fields do the work. <see cref="Coverage"/> decides which regions of a wide image have
/// any cloud at all, so a panorama gets clear sky between nebulae instead of uniform haze.
/// <see cref="Structure"/> carves the filaments inside those regions. <see cref="Warp"/> displaces the
/// structure field so its filaments curl instead of running straight.
/// </para>
/// <para>
/// All three are sampled in turns of the image width, which keeps cloud features round rather than
/// stretched when the image is much wider than it is tall.
/// </para>
/// </remarks>
public sealed record NebulaOptions
{
    /// <summary>Gets whether the nebula layer is rendered at all.</summary>
    /// <value><see langword="true"/> by default.</value>
    public bool Enabled { get; init; } = true;

    /// <summary>Gets the overall brightness multiplier for the clouds.</summary>
    /// <value>Not negative. Around <c>0.5</c> keeps the nebula behind the stars rather than in front of them.</value>
    public float Intensity { get; init; } = 0.55f;

    /// <summary>Gets the noise field that decides where clouds appear across the image.</summary>
    /// <value>
    /// Never <see langword="null"/>. Low frequencies here mean few, large nebulae; higher frequencies
    /// scatter more, smaller ones.
    /// </value>
    public FractalNoiseOptions Coverage { get; init; } = new()
    {
        BaseFrequency = 2.0f,
        Octaves = 3,
        Gain = 0.55f,
        Shape = FractalNoiseShape.Brownian,
    };

    /// <summary>Gets the noise field that shapes the filaments inside a cloud.</summary>
    /// <value>
    /// Never <see langword="null"/>. The ridged shape is what produces wispy strands rather than
    /// featureless fog.
    /// </value>
    public FractalNoiseOptions Structure { get; init; } = new()
    {
        BaseFrequency = 5.0f,
        Octaves = 7,
        Gain = 0.55f,
        Shape = FractalNoiseShape.Ridged,
    };

    /// <summary>Gets the noise field that displaces the structure field.</summary>
    /// <value>Never <see langword="null"/>. Low octave counts are enough, since only the coarse push matters.</value>
    public FractalNoiseOptions Warp { get; init; } = new()
    {
        BaseFrequency = 3.0f,
        Octaves = 3,
        Gain = 0.5f,
        Shape = FractalNoiseShape.Brownian,
    };

    /// <summary>Gets how far the warp field displaces structure, in turns of the image width.</summary>
    /// <value>
    /// Not negative. Around <c>0.05</c> curls the filaments convincingly; large values dissolve them
    /// into mush.
    /// </value>
    public float WarpStrength { get; init; } = 0.06f;

    /// <summary>Gets how strongly the clouds gather onto the galactic band.</summary>
    /// <value>
    /// In <c>[0, 1]</c>. Zero scatters nebulae anywhere the coverage field allows; one confines them to
    /// the band. Real nebulae lie in the galactic plane, so a value near <c>0.7</c> keeps the clouds and
    /// the star band reading as one structure rather than two unrelated images.
    /// </value>
    /// <remarks>Ignored when clustering is disabled, since there is then no band to gather onto.</remarks>
    public float BandAffinity { get; init; } = 0.7f;

    /// <summary>Gets the coverage level below which no cloud is drawn.</summary>
    /// <value>In <c>[0, 1)</c>. Raising it opens up more empty sky between nebulae.</value>
    public float CoverageThreshold { get; init; } = 0.48f;

    /// <summary>Gets how gradually a cloud fades in above the coverage threshold.</summary>
    /// <value>Greater than zero. Small values give hard-edged clouds; large values give soft ones.</value>
    public float CoverageSoftness { get; init; } = 0.28f;

    /// <summary>Gets the structure level below which the cloud is transparent.</summary>
    /// <value>In <c>[0, 1)</c>. Raising it thins the filaments and darkens the gaps between them.</value>
    public float DensityThreshold { get; init; } = 0.45f;

    /// <summary>Gets the exponent applied to cloud density.</summary>
    /// <value>
    /// Greater than zero. Values above one push density toward the dense cores, which reads as more
    /// contrast between filament and void.
    /// </value>
    public float DensityContrast { get; init; } = 2.2f;

    /// <summary>Gets the colour ramps a seed may choose between.</summary>
    /// <value>
    /// At least one palette. Each seed picks one, so listing several is what makes different seeds
    /// produce clouds of different colours, and listing exactly one pins every seed to that ramp. The
    /// shipped set is <see cref="NebulaPalettes.Default"/>.
    /// </value>
    public IReadOnlyList<NebulaPaletteOptions> Palettes { get; init; } = NebulaPalettes.Default();

    /// <summary>Gets how far a seed may nudge the hues of the palette it chose.</summary>
    /// <value>
    /// In <c>[0, 1]</c>, in turns of the colour wheel. A seed rotates its palette up to half this far
    /// either way, perceptually, so two seeds that pick the same palette still differ a little. The
    /// default is a small nudge; zero renders every palette exactly as authored, and one lets a seed
    /// land anywhere on the wheel, which is more variety than most palettes survive.
    /// </value>
    public float HueVariation { get; init; } = 0.06f;

    /// <summary>Reports why these options cannot be used, if they cannot.</summary>
    /// <returns>A human-readable list of problems, empty when the options are valid.</returns>
    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();

        if (!(Intensity >= 0.0f))
        {
            problems.Add(Invariant($"Nebula Intensity must not be negative but was {Intensity}."));
        }

        if (!(WarpStrength >= 0.0f))
        {
            problems.Add(Invariant($"Nebula WarpStrength must not be negative but was {WarpStrength}."));
        }

        if (BandAffinity is < 0.0f or > 1.0f || float.IsNaN(BandAffinity))
        {
            problems.Add(Invariant($"Nebula BandAffinity must be in [0, 1] but was {BandAffinity}."));
        }

        if (CoverageThreshold is < 0.0f or >= 1.0f || float.IsNaN(CoverageThreshold))
        {
            problems.Add(Invariant($"Nebula CoverageThreshold must be in [0, 1) but was {CoverageThreshold}."));
        }

        if (!(CoverageSoftness > 0.0f))
        {
            problems.Add(Invariant($"Nebula CoverageSoftness must be greater than 0 but was {CoverageSoftness}."));
        }

        if (DensityThreshold is < 0.0f or >= 1.0f || float.IsNaN(DensityThreshold))
        {
            problems.Add(Invariant($"Nebula DensityThreshold must be in [0, 1) but was {DensityThreshold}."));
        }

        if (!(DensityContrast > 0.0f))
        {
            problems.Add(Invariant($"Nebula DensityContrast must be greater than 0 but was {DensityContrast}."));
        }

        if (HueVariation is < 0.0f or > 1.0f || float.IsNaN(HueVariation))
        {
            problems.Add(Invariant($"Nebula HueVariation must be in [0, 1] but was {HueVariation}."));
        }

        if (Palettes.Count == 0)
        {
            problems.Add("Nebula Palettes must contain at least one palette.");
        }

        problems.AddRange(Coverage.Validate());
        problems.AddRange(Structure.Validate());
        problems.AddRange(Warp.Validate());

        foreach (var palette in Palettes)
        {
            problems.AddRange(palette.Validate());
        }

        return problems;
    }
}
