using Imaging.Core.Colors;
using static System.FormattableString;

namespace Starfield.Core.Options;

/// <summary>
/// One population of stars: how many, how big, how bright and what colour.
/// </summary>
/// <remarks>
/// Depth is built from several of these rather than from one. A distant layer is dense, faint and
/// small; a near layer is sparse, bright and spiked. Exported separately, they become parallax planes.
/// </remarks>
public sealed record StarLayerOptions
{
    /// <summary>Gets the layer's identifier, used in logs and in exported file names.</summary>
    /// <value>Non-empty, and unique among the layers of one field. Keep it filename-safe.</value>
    public string Name { get; init; } = "stars";

    /// <summary>Gets how many stars the layer places per million pixels.</summary>
    /// <value>
    /// Not negative. Because it is a density rather than a count, widening the image adds stars instead
    /// of stretching the ones already there.
    /// </value>
    public float DensityPerMegapixel { get; init; } = 700.0f;

    /// <summary>Gets the smallest star core radius, in pixels.</summary>
    /// <value>Greater than zero. Values below one still render, as a partly lit pixel.</value>
    public float MinRadius { get; init; } = 0.35f;

    /// <summary>Gets the largest star core radius, in pixels.</summary>
    /// <value>At least <see cref="MinRadius"/>.</value>
    public float MaxRadius { get; init; } = 1.1f;

    /// <summary>Gets the dimmest star's peak brightness in linear light.</summary>
    /// <value>Not negative. Values near zero produce stars that only dithering keeps visible.</value>
    public float MinIntensity { get; init; } = 0.15f;

    /// <summary>Gets the brightest star's peak brightness in linear light.</summary>
    /// <value>At least <see cref="MinIntensity"/>. Values well above one are normal; tone mapping absorbs them.</value>
    public float MaxIntensity { get; init; } = 4.0f;

    /// <summary>Gets how strongly the population is skewed toward faint stars.</summary>
    /// <value>
    /// Greater than zero. One spreads brightness evenly, which looks artificial. Around three gives the
    /// "many faint, a few blazing" distribution a real sky has.
    /// </value>
    public float IntensityExponent { get; init; } = 3.0f;

    /// <summary>Gets how steeply a star's brightness falls away from its centre.</summary>
    /// <value>
    /// Greater than one. This is the exponent of the Moffat profile the renderer draws stars with, so
    /// the wings fall off as the distance to the power of twice this. Around <c>2.5</c> matches a real
    /// point source. Lower values spread every star into a soft ball; much higher ones make stars so
    /// tight they alias into single pixels.
    /// </value>
    public float FalloffExponent { get; init; } = 2.5f;

    /// <summary>Gets the cut-off radius as a multiple of the core radius.</summary>
    /// <value>
    /// At least one. Beyond it a star contributes nothing, which bounds how far its work spreads. It
    /// also sets the width of the aureole.
    /// </value>
    public float GlowRadiusScale { get; init; } = 7.0f;

    /// <summary>Gets the brightness of the wide aureole around a star, relative to its core.</summary>
    /// <value>
    /// In <c>[0, 1]</c>, though anything above about <c>0.1</c> starts to look like lens flare rather
    /// than scattered light. Zero renders bare points.
    /// </value>
    public float GlowStrength { get; init; } = 0.05f;

    /// <summary>Gets how strongly this layer follows the field's clustering.</summary>
    /// <value>
    /// In <c>[0, 1]</c>. Zero spreads the layer evenly regardless of clustering; one gives it the full
    /// variation. Distant populations should follow it closely, while a sparse foreground layer looks
    /// better nearly uniform.
    /// </value>
    public float ClusteringResponse { get; init; } = 1.0f;

    /// <summary>Gets the coolest star temperature in the layer, in kelvin.</summary>
    /// <value>At least <see cref="Blackbody.MinimumKelvin"/>. Around 3000 K reads as a deep orange.</value>
    public float MinTemperatureKelvin { get; init; } = 3200.0f;

    /// <summary>Gets the hottest star temperature in the layer, in kelvin.</summary>
    /// <value>
    /// At least <see cref="MinTemperatureKelvin"/> and at most <see cref="Blackbody.MaximumKelvin"/>.
    /// Around 12000 K reads as a cold blue-white.
    /// </value>
    public float MaxTemperatureKelvin { get; init; } = 12000.0f;

    /// <summary>Gets how much of the temperature tint survives.</summary>
    /// <value>
    /// Not negative. Zero renders every star white; one keeps the full black-body tint; above one pushes
    /// past it. Real skies are subtle, so values below one usually look right.
    /// </value>
    public float Saturation { get; init; } = 0.7f;

    /// <summary>Gets the diffraction spikes for this layer.</summary>
    /// <value>Never <see langword="null"/>. Disabled by default.</value>
    public SpikeOptions Spikes { get; init; } = new();

    /// <summary>Reports why these options cannot be used, if they cannot.</summary>
    /// <returns>A human-readable list of problems, empty when the options are valid.</returns>
    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();

        if (string.IsNullOrWhiteSpace(Name))
        {
            problems.Add("Star layer Name must not be empty.");
        }

        if (!(DensityPerMegapixel >= 0.0f))
        {
            problems.Add(Invariant($"DensityPerMegapixel must not be negative but was {DensityPerMegapixel}."));
        }

        if (!(MinRadius > 0.0f))
        {
            problems.Add(Invariant($"MinRadius must be greater than 0 but was {MinRadius}."));
        }

        if (MaxRadius < MinRadius)
        {
            problems.Add(Invariant($"MaxRadius ({MaxRadius}) must not be less than MinRadius ({MinRadius})."));
        }

        if (!(MinIntensity >= 0.0f))
        {
            problems.Add(Invariant($"MinIntensity must not be negative but was {MinIntensity}."));
        }

        if (MaxIntensity < MinIntensity)
        {
            problems.Add(Invariant($"MaxIntensity ({MaxIntensity}) must not be less than MinIntensity ({MinIntensity})."));
        }

        if (!(IntensityExponent > 0.0f))
        {
            problems.Add(Invariant($"IntensityExponent must be greater than 0 but was {IntensityExponent}."));
        }

        if (!(GlowRadiusScale >= 1.0f))
        {
            problems.Add(Invariant($"GlowRadiusScale must be at least 1 but was {GlowRadiusScale}."));
        }

        if (GlowStrength is < 0.0f or > 1.0f || float.IsNaN(GlowStrength))
        {
            problems.Add(Invariant($"GlowStrength must be in [0, 1] but was {GlowStrength}."));
        }

        if (!(FalloffExponent > 1.0f))
        {
            problems.Add(Invariant($"FalloffExponent must be greater than 1 but was {FalloffExponent}."));
        }

        if (ClusteringResponse is < 0.0f or > 1.0f || float.IsNaN(ClusteringResponse))
        {
            problems.Add(Invariant($"ClusteringResponse must be in [0, 1] but was {ClusteringResponse}."));
        }

        if (MinTemperatureKelvin < Blackbody.MinimumKelvin)
        {
            problems.Add(Invariant($"MinTemperatureKelvin must be at least {Blackbody.MinimumKelvin} but was {MinTemperatureKelvin}."));
        }

        if (MaxTemperatureKelvin < MinTemperatureKelvin || MaxTemperatureKelvin > Blackbody.MaximumKelvin)
        {
            problems.Add(Invariant(
                $"MaxTemperatureKelvin must be between MinTemperatureKelvin and {Blackbody.MaximumKelvin} but was {MaxTemperatureKelvin}."));
        }

        if (!(Saturation >= 0.0f))
        {
            problems.Add(Invariant($"Saturation must not be negative but was {Saturation}."));
        }

        problems.AddRange(Spikes.Validate());
        return problems;
    }
}
