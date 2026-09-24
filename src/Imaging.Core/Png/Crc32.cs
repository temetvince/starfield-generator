namespace Imaging.Core.Png;

/// <summary>
/// The CRC-32 used by every PNG chunk, implemented against the specification's own table.
/// </summary>
/// <remarks>
/// This is the standard reflected CRC-32 with polynomial <c>0xEDB88320</c>. It is here rather than
/// taken from a package so the library keeps zero runtime dependencies for encoding.
/// </remarks>
internal static class Crc32
{
    private static readonly uint[] Table = BuildTable();

    /// <summary>Computes the CRC of a single buffer.</summary>
    /// <param name="data">The bytes to checksum.</param>
    /// <returns>The finalised CRC.</returns>
    internal static uint Compute(ReadOnlySpan<byte> data) => Finish(Update(Start(), data));

    /// <summary>Gets the starting value of a running CRC.</summary>
    /// <returns>The seed to pass to the first <see cref="Update"/>.</returns>
    internal static uint Start() => 0xFFFFFFFFU;

    /// <summary>Folds more bytes into a running CRC.</summary>
    /// <param name="running">The value returned by <see cref="Start"/> or a previous update.</param>
    /// <param name="data">The bytes to fold in.</param>
    /// <returns>The updated running value, which is not yet finalised.</returns>
    internal static uint Update(uint running, ReadOnlySpan<byte> data)
    {
        var crc = running;
        foreach (var value in data)
        {
            crc = Table[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        return crc;
    }

    /// <summary>Finalises a running CRC.</summary>
    /// <param name="running">The accumulated value.</param>
    /// <returns>The CRC to store in the chunk.</returns>
    internal static uint Finish(uint running) => running ^ 0xFFFFFFFFU;

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (uint index = 0; index < 256; index++)
        {
            var value = index;
            for (var bit = 0; bit < 8; bit++)
            {
                value = (value & 1) != 0 ? 0xEDB88320U ^ (value >> 1) : value >> 1;
            }

            table[index] = value;
        }

        return table;
    }
}
