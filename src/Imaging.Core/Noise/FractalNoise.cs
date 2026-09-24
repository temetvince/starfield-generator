using System.Collections.Immutable;
using Imaging.Core.Numerics;

namespace Imaging.Core.Noise;

/// <summary>
/// A sum of gradient-noise octaves, sampled in normalised image coordinates and normalised to <c>[0, 1]</c>.
/// </summary>
/// <remarks>
/// <para>
/// Coordinates are in turns of the image width: <c>x</c> runs from zero at the left edge to one at the
/// right edge, and <c>y</c> uses the same scale so that the field stays square rather than stretching
/// with the aspect ratio. For a 10000×1080 image, <c>y</c> therefore only reaches about <c>0.108</c>.
/// </para>
/// <para>
/// Each octave gets its own lattice, so octaves do not line up into visible grid artefacts. Octave
/// periods are whole numbers of cells, which is what keeps the seamless mode exact.
/// </para>
/// <para>
/// Instances are immutable and safe to sample concurrently.
/// </para>
/// </remarks>
public sealed class FractalNoise
{
    private readonly ImmutableArray<TilingPerlinNoise> _octaves;
    private readonly ImmutableArray<int> _periods;
    private readonly ImmutableArray<float> _amplitudes;
    private readonly float _amplitudeTotal;
    private readonly FractalNoiseShape _shape;
    private readonly bool _seamlessX;

    /// <summary>Creates a fractal field.</summary>
    /// <param name="seed">Any value. Different seeds give unrelated fields.</param>
    /// <param name="options">
    /// The field's shape. Must pass <see cref="FractalNoiseOptions.Validate"/>; the constructor rejects
    /// options that do not.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="options"/> is not valid.</exception>
    public FractalNoise(ulong seed, FractalNoiseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var problems = options.Validate();
        if (problems.Count > 0)
        {
            throw new ArgumentException(string.Join(" ", problems), nameof(options));
        }

        _shape = options.Shape;
        _seamlessX = options.SeamlessX;

        var octaves = ImmutableArray.CreateBuilder<TilingPerlinNoise>(options.Octaves);
        var periods = ImmutableArray.CreateBuilder<int>(options.Octaves);
        var amplitudes = ImmutableArray.CreateBuilder<float>(options.Octaves);

        var frequency = options.BaseFrequency;
        var amplitude = 1.0f;
        var total = 0.0f;

        for (var octave = 0; octave < options.Octaves; octave++)
        {
            octaves.Add(new TilingPerlinNoise(Hash64.Combine(seed, (ulong)octave)));
            periods.Add(Math.Max(1, (int)MathF.Round(frequency)));
            amplitudes.Add(amplitude);

            total += amplitude;
            frequency *= options.Lacunarity;
            amplitude *= options.Gain;
        }

        _octaves = octaves.MoveToImmutable();
        _periods = periods.MoveToImmutable();
        _amplitudes = amplitudes.MoveToImmutable();
        _amplitudeTotal = total;
    }

    /// <summary>Gets the number of octaves summed per sample.</summary>
    /// <value>The octave count the field was built with.</value>
    public int OctaveCount => _octaves.Length;

    /// <summary>Samples the field.</summary>
    /// <param name="x">Horizontal position in turns of the image width.</param>
    /// <param name="y">Vertical position on the same scale as <paramref name="x"/>.</param>
    /// <returns>A value in <c>[0, 1]</c>, centred near <c>0.5</c> for the Brownian shape.</returns>
    public float Sample(float x, float y)
    {
        var sum = 0.0f;

        for (var octave = 0; octave < _octaves.Length; octave++)
        {
            var period = _periods[octave];
            var raw = _octaves[octave].Sample(x * period, y * period, _seamlessX ? period : 0);
            sum += Fold(raw) * _amplitudes[octave];
        }

        var normalised = sum / _amplitudeTotal;
        return Math.Clamp(_shape == FractalNoiseShape.Brownian ? (normalised * 0.5f) + 0.5f : normalised, 0.0f, 1.0f);
    }

    private float Fold(float raw) => _shape switch
    {
        FractalNoiseShape.Ridged => 1.0f - MathF.Abs(raw),
        FractalNoiseShape.Billow => MathF.Abs(raw),
        FractalNoiseShape.Brownian => raw,
        _ => raw,
    };
}
