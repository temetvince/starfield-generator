using Imaging.Core.Numerics;

namespace Imaging.Core.Noise;

/// <summary>
/// Gradient (Perlin) noise on an integer lattice, optionally wrapping horizontally.
/// </summary>
/// <remarks>
/// <para>
/// Sampling is a pure function of the coordinates and the seed. No state is carried between calls, so
/// any number of threads may sample the same instance at once, and a pixel produces the same value
/// whether it was rendered alone or as part of a band.
/// </para>
/// <para>
/// Horizontal wrapping is what makes a generated backdrop loop. Passing a period wraps the lattice
/// columns modulo that period, so the value at <c>x</c> and at <c>x + period</c> is identical by
/// construction rather than by blending two renders together.
/// </para>
/// </remarks>
/// <remarks>Creates a noise field.</remarks>
/// <param name="seed">Any value. Different seeds give unrelated fields.</param>
public sealed class TilingPerlinNoise(ulong seed)
{
    private readonly ulong _seed = Hash64.Mix(seed);

    /// <summary>Samples the field.</summary>
    /// <param name="x">Horizontal position in lattice units, where one unit is one lattice cell.</param>
    /// <param name="y">Vertical position in lattice units.</param>
    /// <param name="periodX">
    /// Number of lattice cells after which the field repeats horizontally. Values of one or less
    /// disable wrapping. Wrapping only holds if the caller also scales <paramref name="x"/> so that the
    /// image width spans exactly this many cells.
    /// </param>
    /// <param name="periodY">
    /// The number of lattice cells after which the field repeats vertically, or zero for no vertical
    /// repeat. A period of one is legal and gives a field with no vertical variation between rows of
    /// lattice points, which still joins seamlessly.
    /// </param>
    /// <returns>
    /// A value in roughly <c>[-1, 1]</c>. Two-dimensional gradient noise rarely reaches the extremes,
    /// so the practical range is nearer <c>[-0.9, 0.9]</c>.
    /// </returns>
    public float Sample(float x, float y, int periodX, int periodY = 0)
    {
        var x0 = (int)MathF.Floor(x);
        var y0 = (int)MathF.Floor(y);
        var fx = x - x0;
        var fy = y - y0;

        var x1 = x0 + 1;
        if (periodX > 1)
        {
            x0 = Wrap(x0, periodX);
            x1 = Wrap(x1, periodX);
        }

        var y1 = y0 + 1;
        if (periodY >= 1)
        {
            y0 = Wrap(y0, periodY);
            y1 = Wrap(y1, periodY);
        }

        var u = Fade(fx);
        var v = Fade(fy);

        var topLeft = GradientDot(x0, y0, fx, fy);
        var topRight = GradientDot(x1, y0, fx - 1.0f, fy);
        var bottomLeft = GradientDot(x0, y1, fx, fy - 1.0f);
        var bottomRight = GradientDot(x1, y1, fx - 1.0f, fy - 1.0f);

        var top = Lerp(topLeft, topRight, u);
        var bottom = Lerp(bottomLeft, bottomRight, u);

        // The 1.4142 factor rescales the natural 2D gradient-noise range toward [-1, 1].
        return Lerp(top, bottom, v) * 1.41421356f;
    }

    private static int Wrap(int value, int period)
    {
        var wrapped = value % period;
        return wrapped < 0 ? wrapped + period : wrapped;
    }

    private static float Fade(float t) => t * t * t * ((t * ((t * 6.0f) - 15.0f)) + 10.0f);

    private static float Lerp(float start, float end, float amount) => start + ((end - start) * amount);

    private float GradientDot(int latticeX, int latticeY, float dx, float dy)
    {
        // Eight evenly spaced directions keep the field isotropic without a permutation table.
        var hash = Hash64.Combine(_seed, latticeX, latticeY);
        return (hash & 7UL) switch
        {
            0UL => dx,
            1UL => -dx,
            2UL => dy,
            3UL => -dy,
            4UL => (dx + dy) * 0.70710678f,
            5UL => (dx - dy) * 0.70710678f,
            6UL => (-dx + dy) * 0.70710678f,
            _ => (-dx - dy) * 0.70710678f,
        };
    }
}
