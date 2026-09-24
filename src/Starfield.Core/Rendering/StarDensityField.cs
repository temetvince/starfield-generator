using Imaging.Core.Noise;
using Imaging.Core.Numerics;
using Imaging.Core.Rendering;
using Starfield.Core.Options;

namespace Starfield.Core.Rendering;

/// <summary>
/// The density multiplier that gathers stars into a galactic band and the star clouds within it.
/// </summary>
/// <remarks>
/// <para>
/// The field is a pure function of position, which is what lets star placement stay reproducible: a
/// star is accepted or rejected on the strength of the field where it landed, so the answer does not
/// depend on which band was being rendered or which thread asked.
/// </para>
/// <para>
/// The band's centre line meanders under a noise field rather than following a tilt. A tilted line
/// would arrive at the right-hand edge at a different height than it left the left-hand edge, which
/// would break a seamless image at exactly the join it is meant to hide.
/// </para>
/// <para>
/// Instances are immutable and safe to sample concurrently. One field is shared by every star layer.
/// </para>
/// </remarks>
public sealed class StarDensityField
{
    /// <summary>How much of the clumping structure survives well away from the band.</summary>
    private const float OutsideBandClumping = 0.25f;

    private readonly StarClusteringOptions _options;
    private readonly ImageSize _image;
    private readonly bool _seamlessY;
    private readonly FractalNoise _clumping;
    private readonly FractalNoise _wobble;
    private readonly FractalNoise _dust;
    private readonly float _bandCentrePixels;
    private readonly float _bandWidthPixels;
    private readonly float _wobblePixels;

    /// <summary>Prepares the field.</summary>
    /// <param name="options">The clustering settings. Must already have passed their own validation.</param>
    /// <param name="image">The image the stars are placed in.</param>
    /// <param name="seed">The field's seed.</param>
    /// <param name="seamlessX">
    /// <see langword="true"/> to wrap the noise fields to the image width, so the clustering joins
    /// across the seam.
    /// </param>
    /// <param name="seamlessY">
    /// <see langword="true"/> to make the field repeat every image height as well: the noise wraps
    /// vertically and the band's distance is measured around the tile, so it becomes a ring.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The clumping noise options are not valid.</exception>
    public StarDensityField(StarClusteringOptions options, ImageSize image, ulong seed, bool seamlessX, bool seamlessY = false)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options;
        _image = image;
        _seamlessY = seamlessY;

        // Noise is sampled in turns of the image width, so the vertical period is the aspect ratio.
        var verticalPeriod = seamlessY ? (float)image.Height / image.Width : 0.0f;
        _clumping = new FractalNoise(
            Hash64.Combine(seed, 201UL),
            options.Clumping with { SeamlessX = seamlessX, VerticalPeriod = verticalPeriod });
        _dust = new FractalNoise(
            Hash64.Combine(seed, 203UL),
            options.Dust with { SeamlessX = seamlessX, VerticalPeriod = verticalPeriod });
        _wobble = new FractalNoise(
            Hash64.Combine(seed, 202UL),
            new FractalNoiseOptions
            {
                BaseFrequency = 2.0f,
                Octaves = 3,
                Gain = 0.5f,
                Shape = FractalNoiseShape.Brownian,
                SeamlessX = seamlessX,
                VerticalPeriod = verticalPeriod,
            });

        _bandCentrePixels = options.BandCentre * image.Height;
        _bandWidthPixels = MathF.Max(options.BandWidth * image.Height, 1.0f);
        _wobblePixels = options.BandWobble * image.Height;
    }

    /// <summary>Gets the largest value <see cref="Sample"/> can return.</summary>
    /// <value>
    /// The ceiling star placement samples against. Always a true upper bound, never a typical value.
    /// </value>
    public float MaximumMultiplier => _options.Enabled ? _options.MaximumMultiplier : 1.0f;

    /// <summary>Samples only the galactic band's profile, without clumps or dust.</summary>
    /// <param name="x">Horizontal position in pixels.</param>
    /// <param name="y">Vertical position in pixels.</param>
    /// <returns>
    /// A value in <c>[0, 1]</c>, one on the band's centre line and falling away from it. This is what
    /// other layers use to place themselves in the galactic plane; the nebula does so because that is
    /// where real clouds are.
    /// </returns>
    public float SampleBand(float x, float y)
    {
        if (!_options.Enabled)
        {
            return 1.0f;
        }

        var centre = _bandCentrePixels + (((_wobble.Sample(x / _image.Width, 0.0f) * 2.0f) - 1.0f) * _wobblePixels);
        var offset = y - centre;

        // On a tile that repeats vertically the band is a ring: distance is measured the short way
        // round, so the rows just above the top edge see the band just below the bottom edge.
        if (_seamlessY)
        {
            offset -= _image.Height * MathF.Round(offset / _image.Height);
        }

        offset /= _bandWidthPixels;
        return MathF.Exp(-offset * offset);
    }

    /// <summary>Samples the density multiplier at a point.</summary>
    /// <param name="x">Horizontal position in pixels. Positions outside the image are legal.</param>
    /// <param name="y">Vertical position in pixels.</param>
    /// <returns>
    /// A multiplier in <c>[0, MaximumMultiplier]</c> to apply to a layer's density. One means the
    /// layer's density as configured.
    /// </returns>
    public float Sample(float x, float y)
    {
        if (!_options.Enabled)
        {
            return 1.0f;
        }

        // Both noise fields are sampled in turns of the image width, the same convention the nebula
        // uses, so clustering features stay round rather than stretching on a wide image.
        var scale = 1.0f / _image.Width;
        var u = x * scale;
        var v = y * scale;

        var band = SampleBand(x, y);

        // Clumps concentrate along the band, which is what makes it read as star clouds rather than as
        // a stripe over unrelated noise. They are not confined to it, so the outer sky keeps some
        // structure of its own.
        var clump = MathF.Pow(_clumping.Sample(u, v), _options.ClumpContrast);
        var clumpReach = OutsideBandClumping + ((1.0f - OutsideBandClumping) * band);

        var gathered = _options.Floor + (_options.BandStrength * band) + (_options.ClumpStrength * clump * clumpReach);

        // Dust sits in front of the band, so it dims the glow and thins the stars together. Confining it
        // to the band matters: lanes belong to the galactic plane, and scattering them across empty sky
        // would read as noise rather than as obscuration.
        var dust = Interpolation.Smoothstep(_options.DustThreshold, 1.0f, _dust.Sample(u, v));
        return gathered * (1.0f - (_options.DustStrength * dust * band));
    }
}
