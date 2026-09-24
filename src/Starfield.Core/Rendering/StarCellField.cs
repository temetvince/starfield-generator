using Imaging.Core.Colors;
using Imaging.Core.Numerics;
using Imaging.Core.Rendering;
using Starfield.Core.Options;

namespace Starfield.Core.Rendering;

/// <summary>
/// Places a layer's stars on a fixed grid of cells, so any slice of the image can be repopulated
/// on demand without generating or storing the others.
/// </summary>
/// <remarks>
/// <para>
/// This is the trick that makes banded rendering work for point sources. Each cell owns a random stream
/// derived from its own coordinates, so the stars in a cell depend on nothing but the seed, the layer
/// and the cell. A band asks for the cells it overlaps, plus a margin as wide as the largest star's
/// reach, and gets exactly the same stars every time.
/// </para>
/// <para>
/// Instances are immutable and safe to use from several band workers at once.
/// </para>
/// </remarks>
public sealed class StarCellField
{
    private const float TargetCellSize = 64.0f;

    /// <summary>Smallest random scaling applied to a star's radius.</summary>
    private const float MinRadiusJitter = 0.85f;

    /// <summary>
    /// Largest random scaling applied to a star's radius.
    /// </summary>
    /// <remarks>
    /// <see cref="MaxInfluenceRadius"/> must account for this. If the margin assumed unjittered radii, a
    /// band could skip the cell holding an over-sized star and lose the part of its glow that reached
    /// into the band, which would show up as a seam at the band boundary.
    /// </remarks>
    private const float MaxRadiusJitter = 1.2f;

    /// <summary>
    /// Offset from a pixel's index to its centre, which is where shading measures distance from.
    /// </summary>
    /// <remarks>
    /// Row <c>r</c> is shaded at <c>r + 0.5</c>, so a star centred just past the last row of a band can
    /// still light that row. Culling against the row index alone would drop it, and the dropped rim
    /// would appear as a faint seam exactly on the band boundary.
    /// </remarks>
    private const float PixelCentre = 0.5f;

    private readonly StarLayerOptions _layer;
    private readonly ImageSize _image;
    private readonly ulong _seed;
    private readonly int _layerIndex;
    private readonly bool _seamlessX;
    private readonly int _cellsX;
    private readonly int _cellsY;
    private readonly float _cellWidth;
    private readonly float _cellHeight;
    private readonly float _candidatesPerCell;
    private readonly float _spikeAngleBase;
    private readonly float _spikeAngleJitter;
    private readonly StarDensityField? _density;
    private readonly float _response;
    private readonly float _acceptanceCeiling;

    /// <summary>Prepares the cell grid for one star layer.</summary>
    /// <param name="layer">The population to place. Must already have passed its own validation.</param>
    /// <param name="image">The image the stars are placed in.</param>
    /// <param name="seed">The field's seed.</param>
    /// <param name="layerIndex">
    /// This layer's position in the field, which keeps two layers with identical settings from producing
    /// identical stars.
    /// </param>
    /// <param name="seamlessX">
    /// <see langword="true"/> to emit a wrapped copy of any star whose reach crosses an image edge, so
    /// the seam has no gap.
    /// </param>
    /// <param name="density">
    /// The clustering field that gathers stars into a band and clouds, or <see langword="null"/> to
    /// spread the layer evenly.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="layer"/> is <see langword="null"/>.</exception>
    public StarCellField(
        StarLayerOptions layer,
        ImageSize image,
        ulong seed,
        int layerIndex,
        bool seamlessX,
        StarDensityField? density = null)
    {
        ArgumentNullException.ThrowIfNull(layer);

        _layer = layer;
        _image = image;
        _seed = Hash64.Combine(seed, (ulong)layerIndex);
        _layerIndex = layerIndex;
        _seamlessX = seamlessX;

        _cellsX = Math.Max(1, (int)MathF.Round(image.Width / TargetCellSize));
        _cellsY = Math.Max(1, (int)MathF.Round(image.Height / TargetCellSize));
        _cellWidth = (float)image.Width / _cellsX;
        _cellHeight = (float)image.Height / _cellsY;

        // Clustering is applied by rejection: each cell offers candidates at the highest density the
        // field can reach, and each candidate survives in proportion to the density where it landed.
        // Scaling the count per cell instead would make density change in visible cell-sized steps.
        _density = density;
        _response = Math.Clamp(layer.ClusteringResponse, 0.0f, 1.0f);
        _acceptanceCeiling = density is null
            ? 1.0f
            : 1.0f + (_response * (density.MaximumMultiplier - 1.0f));

        var starsPerCell = layer.DensityPerMegapixel * _cellWidth * _cellHeight / 1_000_000.0f;
        _candidatesPerCell = starsPerCell * MathF.Max(_acceptanceCeiling, 1.0f);

        var maxGlow = layer.MaxRadius * MaxRadiusJitter * layer.GlowRadiusScale;
        MaxInfluenceRadius = layer.Spikes.Enabled
            ? MathF.Max(maxGlow, maxGlow * layer.Spikes.LengthScale)
            : maxGlow;

        _spikeAngleBase = layer.Spikes.AngleDegrees * MathF.PI / 180.0f;
        _spikeAngleJitter = layer.Spikes.AngleJitterDegrees * MathF.PI / 180.0f;
    }

    /// <summary>Gets the furthest any star in this layer can reach from its own centre.</summary>
    /// <value>In pixels. This is the margin a band must add when asking for cells.</value>
    public float MaxInfluenceRadius { get; }

    /// <summary>Collects every star that can affect a range of image rows.</summary>
    /// <param name="firstRow">First image row of interest, inclusive. May be negative.</param>
    /// <param name="lastRow">Last image row of interest, inclusive.</param>
    /// <param name="destination">
    /// Receives the stars. It is added to, not cleared, so a caller may reuse one list across bands by
    /// clearing it itself.
    /// </param>
    /// <remarks>
    /// Stars whose centre lies just outside the image are included, because their glow still reaches
    /// inside. Without them the top and bottom edges would thin out visibly.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <see langword="null"/>.</exception>
    public void Collect(int firstRow, int lastRow, List<Star> destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        if (_candidatesPerCell <= 0.0f || lastRow < firstRow)
        {
            return;
        }

        var margin = MaxInfluenceRadius + PixelCentre;
        var firstCellY = (int)MathF.Floor((firstRow - margin) / _cellHeight);
        var lastCellY = (int)MathF.Floor((lastRow + margin) / _cellHeight);

        // One ring of cells beyond the image keeps edge glow intact without inventing stars far outside.
        firstCellY = Math.Max(firstCellY, -1);
        lastCellY = Math.Min(lastCellY, _cellsY);

        for (var cellY = firstCellY; cellY <= lastCellY; cellY++)
        {
            for (var cellX = 0; cellX < _cellsX; cellX++)
            {
                CollectCell(cellX, cellY, firstRow, lastRow, destination);
            }
        }
    }

    private void CollectCell(int cellX, int cellY, int firstRow, int lastRow, List<Star> destination)
    {
        var random = new DeterministicRandom(Hash64.Combine(_seed, _layerIndex, cellX, cellY));

        var count = (int)_candidatesPerCell;
        if (random.NextChance(_candidatesPerCell - count))
        {
            count++;
        }

        for (var index = 0; index < count; index++)
        {
            // Every draw happens for every candidate, whether or not it survives clustering or culling,
            // so the stream stays in step no matter which rows the caller asked for.
            var x = (cellX + random.NextUnit()) * _cellWidth;
            var y = (cellY + random.NextUnit()) * _cellHeight;
            var brightness = MathF.Pow(random.NextUnit(), _layer.IntensityExponent);
            var radiusJitter = random.NextRange(MinRadiusJitter, MaxRadiusJitter);
            var temperature = random.NextRange(_layer.MinTemperatureKelvin, _layer.MaxTemperatureKelvin);
            var angleJitter = random.NextRange(-1.0f, 1.0f);
            var acceptance = random.NextUnit();

            if (!IsAccepted(x, y, acceptance))
            {
                continue;
            }

            var star = BuildStar(x, y, brightness, radiusJitter, temperature, angleJitter);

            // Rows are shaded at their centres, so the star reaches rows in
            // [Y - reach - 0.5, Y + reach - 0.5]. Overlap is tested against that, not against Y alone.
            var reach = star.InfluenceRadius;
            if (star.Y + reach < firstRow + PixelCentre || star.Y - reach > lastRow + PixelCentre)
            {
                continue;
            }

            destination.Add(star);

            if (!_seamlessX)
            {
                continue;
            }

            // The same half-pixel reasoning applies across the seam. Being generous here only costs a
            // copy that the splatting loop then clips away.
            if (star.X - reach < PixelCentre)
            {
                destination.Add(star with { X = star.X + _image.Width });
            }
            else if (star.X + reach > _image.Width - PixelCentre)
            {
                destination.Add(star with { X = star.X - _image.Width });
            }
        }
    }

    /// <summary>Decides whether a candidate survives clustering.</summary>
    /// <param name="x">Candidate position in pixels.</param>
    /// <param name="y">Candidate position in pixels.</param>
    /// <param name="acceptance">The candidate's own draw in <c>[0, 1)</c>.</param>
    /// <returns>
    /// <see langword="true"/> when the candidate becomes a star. The chance is the clustering
    /// multiplier where it landed, divided by the highest multiplier the field can reach, so the
    /// surviving population follows the field exactly.
    /// </returns>
    private bool IsAccepted(float x, float y, float acceptance)
    {
        if (_density is null || _response <= 0.0f)
        {
            return true;
        }

        var multiplier = 1.0f + (_response * (_density.Sample(x, y) - 1.0f));
        return acceptance * _acceptanceCeiling < multiplier;
    }

    private Star BuildStar(float x, float y, float brightness, float radiusJitter, float temperature, float angleJitter)
    {
        var intensity = Interpolation.Lerp(_layer.MinIntensity, _layer.MaxIntensity, brightness);
        var coreRadius = MathF.Max(
            0.05f,
            Interpolation.Lerp(_layer.MinRadius, _layer.MaxRadius, brightness) * radiusJitter);
        var glowRadius = coreRadius * _layer.GlowRadiusScale;

        var spikes = _layer.Spikes;
        var spiked = spikes.Enabled && brightness >= spikes.BrightnessThreshold;
        var spikeLength = spiked ? glowRadius * spikes.LengthScale : 0.0f;
        var spikeAngle = spiked ? _spikeAngleBase + (angleJitter * _spikeAngleJitter) : 0.0f;

        var tint = Blackbody.FromTemperature(temperature).WithSaturation(_layer.Saturation);

        return new Star(x, y, coreRadius, glowRadius, spikeLength, spikeAngle, tint * intensity);
    }
}
