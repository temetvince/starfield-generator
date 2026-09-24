namespace Imaging.Core.Numerics;

/// <summary>
/// Deterministic 64-bit mixing used to derive reproducible pseudo-random streams from coordinates.
/// </summary>
/// <remarks>
/// <para>
/// Every method here is a pure function of its arguments. The same inputs produce the same output on
/// every machine and every run, which is what makes banded and parallel rendering reproducible: a tile
/// can derive its own stream from its own coordinates without talking to any other tile.
/// </para>
/// <para>
/// The mixer is the SplitMix64 finalizer. It is not cryptographic and must never be used to protect
/// anything; it exists purely to spread nearby inputs across the 64-bit range.
/// </para>
/// </remarks>
public static class Hash64
{
    /// <summary>The golden-ratio odd constant used to advance and decorrelate streams.</summary>
    internal const ulong GoldenGamma = 0x9E3779B97F4A7C15UL;

    /// <summary>
    /// Mixes one 64-bit value so that inputs differing by a single bit produce unrelated outputs.
    /// </summary>
    /// <param name="value">Any 64-bit value, including sequential counters.</param>
    /// <returns>The avalanched value. Distinct inputs may collide, but only at random-chance rates.</returns>
    public static ulong Mix(ulong value)
    {
        var z = value;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>Folds one value into a seed, producing an independent derived seed.</summary>
    /// <param name="seed">The seed to derive from.</param>
    /// <param name="value">The value to fold in.</param>
    /// <returns>A derived seed that is stable across runs and unrelated to <paramref name="seed"/>.</returns>
    public static ulong Combine(ulong seed, ulong value) => Mix(seed ^ (value + GoldenGamma + (seed << 6) + (seed >> 2)));

    /// <summary>Folds a two-dimensional coordinate into a seed.</summary>
    /// <param name="seed">The seed to derive from.</param>
    /// <param name="first">First coordinate, typically a cell column.</param>
    /// <param name="second">Second coordinate, typically a cell row.</param>
    /// <returns>A derived seed unique to the coordinate pair for practical purposes.</returns>
    public static ulong Combine(ulong seed, int first, int second)
        => Combine(Combine(seed, (uint)first), (uint)second);

    /// <summary>Folds a three-dimensional coordinate into a seed.</summary>
    /// <param name="seed">The seed to derive from.</param>
    /// <param name="first">First coordinate, typically a layer index.</param>
    /// <param name="second">Second coordinate, typically a cell column.</param>
    /// <param name="third">Third coordinate, typically a cell row.</param>
    /// <returns>A derived seed unique to the coordinate triple for practical purposes.</returns>
    public static ulong Combine(ulong seed, int first, int second, int third)
        => Combine(Combine(seed, first, second), (uint)third);

    /// <summary>Produces a value in the half-open unit interval from a seed and a counter.</summary>
    /// <param name="seed">The stream seed.</param>
    /// <param name="counter">The position within the stream.</param>
    /// <returns>A value in <c>[0, 1)</c> with 24 bits of resolution.</returns>
    public static float UnitInterval(ulong seed, ulong counter)
        => (Combine(seed, counter) >> 40) * (1.0f / 16777216.0f);
}
