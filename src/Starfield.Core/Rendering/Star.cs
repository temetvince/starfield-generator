using Imaging.Core.Colors;

namespace Starfield.Core.Rendering;

/// <summary>
/// One star, resolved from its cell into everything the splatting loop needs.
/// </summary>
/// <param name="X">Horizontal centre in pixels. May sit outside the image when the star is a wrapped or off-edge copy.</param>
/// <param name="Y">Vertical centre in pixels.</param>
/// <param name="CoreRadius">Radius of the bright core in pixels, always positive.</param>
/// <param name="GlowRadius">Radius at which the halo has faded to nothing, never less than <paramref name="CoreRadius"/>.</param>
/// <param name="SpikeLength">Length of each diffraction arm in pixels, or zero when this star has no spikes.</param>
/// <param name="SpikeAngle">Rotation of the first arm in radians. Meaningless when <paramref name="SpikeLength"/> is zero.</param>
/// <param name="Emission">Peak light at the centre, already tinted and scaled by the star's brightness.</param>
/// <remarks>
/// Stars are produced on demand per band and never stored for a whole image, which is what keeps memory
/// flat as the image widens. Two bands that both overlap a star produce identical copies of it.
/// </remarks>
public readonly record struct Star(
    float X,
    float Y,
    float CoreRadius,
    float GlowRadius,
    float SpikeLength,
    float SpikeAngle,
    LinearRgb Emission)
{
    /// <summary>Gets the distance from the centre beyond which this star contributes nothing.</summary>
    /// <value>The larger of the glow radius and the spike length.</value>
    public float InfluenceRadius => MathF.Max(GlowRadius, SpikeLength);

    /// <summary>Gets whether diffraction arms are drawn for this star.</summary>
    /// <value><see langword="true"/> when the spike length is positive.</value>
    public bool HasSpikes => SpikeLength > 0.0f;
}
