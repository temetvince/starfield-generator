using Imaging.Core.Rendering;
using Starfield.Core.Options;

namespace Starfield.Core.Rendering;

/// <summary>
/// Draws one population of stars into a band: cores, halos and, on the brightest, diffraction spikes.
/// </summary>
/// <remarks>
/// <para>
/// Every star is a continuous falloff evaluated per pixel rather than a drawn disc, so sub-pixel stars
/// land as partly lit pixels and nothing aliases into hard squares.
/// </para>
/// <para>
/// The renderer is immutable apart from a thread-local scratch list, so any number of band workers may
/// use one instance at the same time.
/// </para>
/// </remarks>
public sealed class StarLayerRenderer : ILayerRenderer
{
    /// <summary>
    /// How wide the faint aureole is, as a fraction of the star's cut-off radius.
    /// </summary>
    /// <remarks>
    /// Small enough that the aureole is a halo hugging the star rather than a wash across the frame,
    /// large enough that it is clearly a separate feature from the core.
    /// </remarks>
    private const float AureoleRadiusFraction = 0.3f;

    /// <summary>
    /// Per-thread scratch for the stars of the band being rendered, so a long render does not allocate
    /// a fresh list per band per layer.
    /// </summary>
    [ThreadStatic]
    private static List<Star>? _scratchStars;

    private readonly StarLayerOptions _layer;
    private readonly StarCellField _field;

    /// <summary>Creates a renderer for one star layer.</summary>
    /// <param name="layer">The population to draw. Must already have passed its own validation.</param>
    /// <param name="image">The image the stars are placed in.</param>
    /// <param name="seed">The field's seed.</param>
    /// <param name="layerIndex">This layer's position in the field.</param>
    /// <param name="seamlessX"><see langword="true"/> to wrap stars across the left and right edges.</param>
    /// <param name="seamlessY"><see langword="true"/> to make the layer tile vertically as well.</param>
    /// <param name="density">
    /// The clustering field shared by every layer, or <see langword="null"/> to spread this layer
    /// evenly.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="layer"/> is <see langword="null"/>.</exception>
    public StarLayerRenderer(
        StarLayerOptions layer,
        ImageSize image,
        ulong seed,
        int layerIndex,
        bool seamlessX,
        bool seamlessY = false,
        StarDensityField? density = null)
    {
        ArgumentNullException.ThrowIfNull(layer);

        _layer = layer;
        _field = new StarCellField(layer, image, seed, layerIndex, seamlessX, seamlessY, density);
    }

    /// <inheritdoc/>
    public string Name => _layer.Name;

    /// <inheritdoc/>
    public void RenderBand(BandRegion band, RgbBandBuffer target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);

        var stars = _scratchStars ??= [];
        stars.Clear();
        _field.Collect(band.Top, band.Bottom - 1, stars);

        Span<float> armCosines = stackalloc float[_layer.Spikes.Arms];
        Span<float> armSines = stackalloc float[_layer.Spikes.Arms];

        foreach (var star in stars)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Splat(star, band, target, armCosines, armSines);
        }
    }

    private void Splat(Star star, BandRegion band, RgbBandBuffer target, Span<float> armCosines, Span<float> armSines)
    {
        var reach = star.InfluenceRadius;
        var firstX = (int)MathF.Floor(star.X - reach);
        var lastX = (int)MathF.Ceiling(star.X + reach);
        var firstY = Math.Max(band.Top, (int)MathF.Floor(star.Y - reach));
        var lastY = Math.Min(band.Bottom - 1, (int)MathF.Ceiling(star.Y + reach));

        firstX = Math.Max(firstX, 0);
        lastX = Math.Min(lastX, band.Image.Width - 1);

        if (firstX > lastX || firstY > lastY)
        {
            return;
        }

        // The Moffat profile: a sharp peak with power-law wings. A Gaussian of the same width would
        // saturate into a flat disc with a hard rim, which is what makes rendered stars read as shiny
        // balls rather than points of light.
        var coreSquared = star.CoreRadius * star.CoreRadius;
        var beta = _layer.FalloffExponent;

        // The aureole is the same profile widened and flattened: the faint scattered light that only
        // the brightest stars show. Its exponent is fixed because it is the shallow tail that reads as
        // atmosphere, not a per-layer choice.
        var aureoleSquared = MathF.Max(star.GlowRadius * AureoleRadiusFraction, 0.5f);
        aureoleSquared *= aureoleSquared;
        var aureoleStrength = _layer.GlowStrength;
        var reachSquared = reach * reach;

        var arms = star.HasSpikes ? _layer.Spikes.Arms : 0;
        for (var arm = 0; arm < arms; arm++)
        {
            var angle = star.SpikeAngle + (arm * MathF.Tau / arms);
            armCosines[arm] = MathF.Cos(angle);
            armSines[arm] = MathF.Sin(angle);
        }

        var spikeFalloff = star.HasSpikes ? 3.0f / star.SpikeLength : 0.0f;
        var spikeWidthScale = 1.0f / (2.0f * _layer.Spikes.Thickness * _layer.Spikes.Thickness);

        for (var y = firstY; y <= lastY; y++)
        {
            var dy = y + 0.5f - star.Y;
            var localRow = y - band.Top;

            for (var x = firstX; x <= lastX; x++)
            {
                var dx = x + 0.5f - star.X;
                var distanceSquared = (dx * dx) + (dy * dy);
                if (distanceSquared > reachSquared)
                {
                    continue;
                }

                var amount = MathF.Pow(1.0f + (distanceSquared / coreSquared), -beta);

                if (aureoleStrength > 0.0f)
                {
                    var spread = 1.0f + (distanceSquared / aureoleSquared);
                    amount += aureoleStrength / (spread * MathF.Sqrt(spread));
                }

                for (var arm = 0; arm < arms; arm++)
                {
                    var along = (dx * armCosines[arm]) + (dy * armSines[arm]);
                    if (along <= 0.0f)
                    {
                        continue;
                    }

                    var across = (dy * armCosines[arm]) - (dx * armSines[arm]);
                    amount += _layer.Spikes.Intensity
                        * MathF.Exp(-along * spikeFalloff)
                        * MathF.Exp(-across * across * spikeWidthScale);
                }

                target.Add(x, localRow, star.Emission * amount);
            }
        }
    }
}
