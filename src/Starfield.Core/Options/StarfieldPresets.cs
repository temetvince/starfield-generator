using Imaging.Core.Noise;

namespace Starfield.Core.Options;

/// <summary>Ready-made option sets, including the one the tool falls back to when nothing is supplied.</summary>
public static class StarfieldPresets
{
    /// <summary>
    /// Builds the default look: broad nebulae behind a clustered field of point-like stars.
    /// </summary>
    /// <returns>
    /// A fresh, valid set of options at 10000×1080. Because the type is immutable, callers customise it
    /// with a <c>with</c> expression rather than by mutating the result.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The three star layers exist to give the field depth. The dense faint layer reads as distance, the
    /// mid layer carries most of the visible stars, and the sparse bright layer supplies the few anchors
    /// the eye lands on. Their clustering response falls off with prominence: the faint layer traces the
    /// galactic band closely, while the bright foreground layer is nearly uniform.
    /// </para>
    /// <para>
    /// Stars are drawn as points, not discs. Radii sit near a pixel and the falloff exponent is left at
    /// its realistic default, so brightness shows as intensity and halo rather than as size. Diffraction
    /// spikes are off; switch them on per layer for a more stylised look.
    /// </para>
    /// </remarks>
    public static StarfieldOptions Default() => new()
    {
        Nebula = new NebulaOptions
        {
            Intensity = 0.45f,
            BandAffinity = 0.7f,
            Coverage = new FractalNoiseOptions
            {
                BaseFrequency = 3.5f,
                Octaves = 4,
                Gain = 0.55f,
                Shape = FractalNoiseShape.Brownian,
            },
            CoverageThreshold = 0.3f,
            CoverageSoftness = 0.36f,
            DensityThreshold = 0.44f,
            DensityContrast = 2.4f,
        },
        Clustering = new StarClusteringOptions
        {
            Floor = 0.15f,
            BandStrength = 1.6f,
            BandCentre = 0.46f,
            BandWidth = 0.22f,
            BandWobble = 0.2f,
            Clumping = new FractalNoiseOptions
            {
                BaseFrequency = 6.0f,
                Octaves = 6,
                Gain = 0.6f,
                Shape = FractalNoiseShape.Brownian,
            },
            ClumpStrength = 2.0f,
            ClumpContrast = 1.8f,
            DustStrength = 0.72f,
            DustThreshold = 0.5f,
            HazeIntensity = 0.19f,
            HazeContrast = 2.2f,
        },
        StarLayers =
        [
            new StarLayerOptions
            {
                Name = "far",
                DensityPerMegapixel = 4200.0f,
                MinRadius = 0.34f,
                MaxRadius = 0.5f,
                MinIntensity = 0.07f,
                MaxIntensity = 1.1f,
                IntensityExponent = 3.5f,
                FalloffExponent = 2.6f,
                GlowRadiusScale = 3.5f,
                GlowStrength = 0.015f,
                MinTemperatureKelvin = 3000.0f,
                MaxTemperatureKelvin = 9000.0f,
                Saturation = 0.4f,
                ClusteringResponse = 1.0f,
            },
            new StarLayerOptions
            {
                Name = "mid",
                DensityPerMegapixel = 520.0f,
                MinRadius = 0.42f,
                MaxRadius = 0.68f,
                MinIntensity = 0.3f,
                MaxIntensity = 2.6f,
                IntensityExponent = 3.2f,
                FalloffExponent = 2.5f,
                GlowRadiusScale = 6.0f,
                GlowStrength = 0.035f,
                MinTemperatureKelvin = 3200.0f,
                MaxTemperatureKelvin = 11000.0f,
                Saturation = 0.6f,
                ClusteringResponse = 0.75f,
            },
            new StarLayerOptions
            {
                Name = "near",
                DensityPerMegapixel = 34.0f,
                MinRadius = 0.55f,
                MaxRadius = 0.95f,
                MinIntensity = 1.4f,
                MaxIntensity = 7.0f,
                IntensityExponent = 2.4f,
                FalloffExponent = 2.4f,
                GlowRadiusScale = 10.0f,
                GlowStrength = 0.055f,
                MinTemperatureKelvin = 3000.0f,
                MaxTemperatureKelvin = 13000.0f,
                Saturation = 0.75f,
                ClusteringResponse = 0.3f,
            },
        ],
    };
}
