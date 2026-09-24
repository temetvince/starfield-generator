using Imaging.Core.Colors;
using Imaging.Core.Rendering;
using Starfield.Core.Options;

namespace Starfield.Core.Rendering;

/// <summary>
/// Draws the diffuse glow of stars too faint and too crowded to render individually.
/// </summary>
/// <remarks>
/// <para>
/// This is what makes a galactic band look like one. Drawing more and fainter points eventually stops
/// helping: below about a pixel of separation the eye reads a crowd of dots as a smooth glow, and
/// rendering each of them costs everything and adds nothing. The haze supplies that glow directly, from
/// the same <see cref="StarDensityField"/> the individual stars are placed against, so the two always
/// agree about where the crowd is.
/// </para>
/// <para>
/// The renderer is immutable and safe to use from several band workers at once.
/// </para>
/// </remarks>
public sealed class StarHazeLayerRenderer : ILayerRenderer
{
    private readonly StarClusteringOptions _options;
    private readonly StarDensityField _density;
    private readonly LinearRgb _colour;
    private readonly float _normalise;

    /// <summary>Creates a haze renderer.</summary>
    /// <param name="options">
    /// The clustering settings. Must already have passed validation, since the colour is parsed here.
    /// </param>
    /// <param name="density">The field shared with the star layers.</param>
    /// <param name="name">The layer's identifier, used in logs and exported file names.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    /// <exception cref="FormatException"><see cref="StarClusteringOptions.HazeColor"/> is not a hex colour.</exception>
    public StarHazeLayerRenderer(StarClusteringOptions options, StarDensityField density, string name = "haze")
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(density);
        ArgumentNullException.ThrowIfNull(name);

        _options = options;
        _density = density;
        _colour = HexColor.Parse(options.HazeColor);
        Name = name;

        // The field's own ceiling normalises the glow, so changing the band or clump strengths moves
        // where the haze is without changing how bright its brightest point can be.
        var ceiling = density.MaximumMultiplier - options.Floor;
        _normalise = ceiling > 0.0f ? 1.0f / ceiling : 0.0f;
    }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public void RenderBand(BandRegion band, RgbBandBuffer target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (!_options.Enabled || _options.HazeIntensity <= 0.0f || _normalise <= 0.0f)
        {
            return;
        }

        for (var localRow = 0; localRow < band.Height; localRow++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var y = band.ToImageRow(localRow) + 0.5f;

            for (var x = 0; x < band.Image.Width; x++)
            {
                var crowding = (_density.Sample(x + 0.5f, y) - _options.Floor) * _normalise;
                if (crowding <= 0.0f)
                {
                    continue;
                }

                var glow = MathF.Pow(crowding, _options.HazeContrast) * _options.HazeIntensity;
                target.Add(x, localRow, _colour * glow);
            }
        }
    }
}
