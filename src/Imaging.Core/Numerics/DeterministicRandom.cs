namespace Imaging.Core.Numerics;

/// <summary>
/// A tiny reproducible random stream (the SplitMix64 generator) seeded from an explicit value.
/// </summary>
/// <remarks>
/// <para>
/// This is a mutable value type: every draw advances the state in place. Copying an instance forks the
/// stream, so both copies then produce the same sequence. Pass it by reference, or treat each copy as a
/// deliberate fork.
/// </para>
/// <para>
/// The type is not thread-safe. Give each thread its own instance, seeded from coordinates via
/// <see cref="Hash64"/>, rather than sharing one.
/// </para>
/// </remarks>
/// <remarks>Creates a stream positioned at the start of the sequence for <paramref name="seed"/>.</remarks>
/// <param name="seed">Any value. Nearby seeds still produce unrelated sequences.</param>
public struct DeterministicRandom(ulong seed) : IEquatable<DeterministicRandom>
{
    private ulong _state = Hash64.Mix(seed);

    /// <summary>Draws the next 64 raw bits and advances the stream.</summary>
    /// <returns>A uniformly distributed 64-bit value.</returns>
    public ulong NextBits()
    {
        _state += Hash64.GoldenGamma;
        return Hash64.Mix(_state);
    }

    /// <summary>Draws the next value in the half-open unit interval.</summary>
    /// <returns>A value in <c>[0, 1)</c> with 24 bits of resolution.</returns>
    public float NextUnit() => (NextBits() >> 40) * (1.0f / 16777216.0f);

    /// <summary>Draws the next value from a half-open range.</summary>
    /// <param name="minimum">Inclusive lower bound.</param>
    /// <param name="maximum">Exclusive upper bound. Values below <paramref name="minimum"/> invert the range.</param>
    /// <returns>A value in <c>[minimum, maximum)</c>.</returns>
    public float NextRange(float minimum, float maximum) => minimum + ((maximum - minimum) * NextUnit());

    /// <summary>
    /// Draws from a range with the distribution pushed toward one end, which is how a believable
    /// population of "many faint, few bright" values is produced.
    /// </summary>
    /// <param name="minimum">Inclusive lower bound.</param>
    /// <param name="maximum">Exclusive upper bound.</param>
    /// <param name="exponent">
    /// Shaping exponent, which must be greater than zero. A value of <c>1</c> is uniform; larger values
    /// crowd results toward <paramref name="minimum"/>; values below <c>1</c> crowd toward
    /// <paramref name="maximum"/>.
    /// </param>
    /// <returns>A value in <c>[minimum, maximum)</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="exponent"/> is not positive.</exception>
    public float NextBiased(float minimum, float maximum, float exponent)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(exponent);
        return minimum + ((maximum - minimum) * MathF.Pow(NextUnit(), exponent));
    }

    /// <summary>Draws an angle covering the full turn.</summary>
    /// <returns>An angle in radians in <c>[0, 2π)</c>.</returns>
    public float NextAngle() => NextUnit() * MathF.Tau;

    /// <summary>Draws a Boolean with the given probability of being <see langword="true"/>.</summary>
    /// <param name="probability">Probability in <c>[0, 1]</c>. Values outside the range saturate.</param>
    /// <returns><see langword="true"/> with the requested probability.</returns>
    public bool NextChance(float probability) => NextUnit() < probability;

    /// <inheritdoc/>
    public readonly bool Equals(DeterministicRandom other) => _state == other._state;

    /// <inheritdoc/>
    public override readonly bool Equals(object? obj) => obj is DeterministicRandom other && Equals(other);

    /// <inheritdoc/>
    public override readonly int GetHashCode() => _state.GetHashCode();

    /// <summary>Compares two streams by position.</summary>
    /// <param name="left">First stream.</param>
    /// <param name="right">Second stream.</param>
    /// <returns><see langword="true"/> when both would produce the same remaining sequence.</returns>
    public static bool operator ==(DeterministicRandom left, DeterministicRandom right) => left.Equals(right);

    /// <summary>Compares two streams by position.</summary>
    /// <param name="left">First stream.</param>
    /// <param name="right">Second stream.</param>
    /// <returns><see langword="true"/> when the streams differ.</returns>
    public static bool operator !=(DeterministicRandom left, DeterministicRandom right) => !left.Equals(right);
}
