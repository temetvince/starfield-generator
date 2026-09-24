using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Imaging.Core.Png;

namespace Imaging.TestSupport;

/// <summary>
/// A minimal PNG reader used to check what the encoder actually wrote.
/// </summary>
/// <remarks>
/// <para>
/// This exists so the encoder is verified against the file format rather than against itself. It parses
/// the chunk structure, checks every chunk's CRC, inflates the pixel data and reverses the row filters.
/// A failure here means a real decoder would also have rejected or misread the file.
/// </para>
/// <para>
/// It handles only what this library writes: 8 bits per channel, RGB or RGBA, no interlacing.
/// </para>
/// </remarks>
public sealed class DecodedPng
{
    private static ReadOnlySpan<byte> Signature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private readonly byte[] _pixels;

    private DecodedPng(int width, int height, PngColorType colorType, byte[] pixels)
    {
        Width = width;
        Height = height;
        ColorType = colorType;
        _pixels = pixels;
    }

    /// <summary>Gets the image width in pixels.</summary>
    /// <value>Taken from the header chunk.</value>
    public int Width { get; }

    /// <summary>Gets the image height in pixels.</summary>
    /// <value>Taken from the header chunk.</value>
    public int Height { get; }

    /// <summary>Gets the colour type the file declares.</summary>
    /// <value>Either RGB or RGBA.</value>
    public PngColorType ColorType { get; }

    /// <summary>Gets how many bytes each pixel occupies.</summary>
    /// <value>Three for RGB, four for RGBA.</value>
    public int BytesPerPixel => ColorType == PngColorType.Rgba ? 4 : 3;

    /// <summary>Gets the unfiltered pixel bytes, row by row.</summary>
    /// <value>Exactly <c>Width * Height * BytesPerPixel</c> bytes.</value>
    public ReadOnlySpan<byte> Pixels => _pixels;

    /// <summary>Parses a PNG file.</summary>
    /// <param name="file">The complete file contents.</param>
    /// <returns>The decoded image.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="file"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">
    /// The file is malformed, a chunk CRC does not match, or the file uses a feature this reader does
    /// not support.
    /// </exception>
    public static DecodedPng Decode(byte[] file)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (file.Length < Signature.Length || !file.AsSpan(0, Signature.Length).SequenceEqual(Signature))
        {
            throw new InvalidDataException("The file does not start with the PNG signature.");
        }

        var width = 0;
        var height = 0;
        var colorType = PngColorType.Rgb;
        var headerSeen = false;
        var endSeen = false;
        using var compressed = new MemoryStream();

        var offset = Signature.Length;
        while (offset < file.Length)
        {
            if (offset + 8 > file.Length)
            {
                throw new InvalidDataException("The file ends inside a chunk header.");
            }

            var length = BinaryPrimitives.ReadInt32BigEndian(file.AsSpan(offset));
            if (length < 0 || offset + 12 + length > file.Length)
            {
                throw new InvalidDataException("A chunk declares a length that runs past the end of the file.");
            }

            var type = Encoding.ASCII.GetString(file, offset + 4, 4);
            ReadOnlySpan<byte> data = file.AsSpan(offset + 8, length);

            var declaredCrc = BinaryPrimitives.ReadUInt32BigEndian(file.AsSpan(offset + 8 + length));
            var actualCrc = ComputeCrc(file.AsSpan(offset + 4, 4 + length));
            if (declaredCrc != actualCrc)
            {
                throw new InvalidDataException($"Chunk '{type}' has a bad CRC.");
            }

            switch (type)
            {
                case "IHDR":
                    (width, height, colorType) = ReadHeader(data);
                    headerSeen = true;
                    break;

                case "IDAT":
                    compressed.Write(data);
                    break;

                case "IEND":
                    endSeen = true;
                    break;

                default:
                    break;
            }

            offset += 12 + length;
        }

        if (!headerSeen)
        {
            throw new InvalidDataException("The file has no header chunk.");
        }

        if (!endSeen)
        {
            throw new InvalidDataException("The file has no end chunk.");
        }

        var bytesPerPixel = colorType == PngColorType.Rgba ? 4 : 3;
        var pixels = Unfilter(Inflate(compressed.ToArray()), width, height, bytesPerPixel);
        return new DecodedPng(width, height, colorType, pixels);
    }

    /// <summary>Reads one channel of one pixel.</summary>
    /// <param name="x">Column in <c>[0, Width)</c>.</param>
    /// <param name="y">Row in <c>[0, Height)</c>.</param>
    /// <param name="channel">Channel index: 0 red, 1 green, 2 blue, 3 alpha when present.</param>
    /// <returns>The stored byte.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Any argument is outside its valid range.</exception>
    public byte GetChannel(int x, int y, int channel)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(x, Width);
        ArgumentOutOfRangeException.ThrowIfNegative(y);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(y, Height);
        ArgumentOutOfRangeException.ThrowIfNegative(channel);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(channel, BytesPerPixel);

        return _pixels[(((y * Width) + x) * BytesPerPixel) + channel];
    }

    private static (int Width, int Height, PngColorType ColorType) ReadHeader(ReadOnlySpan<byte> data)
    {
        if (data.Length != 13)
        {
            throw new InvalidDataException("The header chunk is the wrong size.");
        }

        var width = BinaryPrimitives.ReadInt32BigEndian(data);
        var height = BinaryPrimitives.ReadInt32BigEndian(data[4..]);

        if (data[8] != 8)
        {
            throw new InvalidDataException("Only 8 bits per channel is supported.");
        }

        if (data[12] != 0)
        {
            throw new InvalidDataException("Interlaced files are not supported.");
        }

        var colorType = data[9] switch
        {
            2 => PngColorType.Rgb,
            6 => PngColorType.Rgba,
            _ => throw new InvalidDataException($"Colour type {data[9]} is not supported."),
        };

        return (width, height, colorType);
    }

    private static byte[] Inflate(byte[] compressed)
    {
        using var source = new MemoryStream(compressed);
        using var zlib = new ZLibStream(source, CompressionMode.Decompress);
        using var raw = new MemoryStream();
        zlib.CopyTo(raw);
        return raw.ToArray();
    }

    private static byte[] Unfilter(byte[] raw, int width, int height, int bytesPerPixel)
    {
        var stride = width * bytesPerPixel;
        var expected = (long)(stride + 1) * height;
        if (raw.LongLength != expected)
        {
            throw new InvalidDataException($"Expected {expected} filtered bytes but found {raw.LongLength}.");
        }

        var pixels = new byte[stride * height];

        for (var y = 0; y < height; y++)
        {
            int filter = raw[y * (stride + 1)];
            ReadOnlySpan<byte> line = raw.AsSpan((y * (stride + 1)) + 1, stride);
            var current = pixels.AsSpan(y * stride, stride);
            ReadOnlySpan<byte> prior = y > 0 ? pixels.AsSpan((y - 1) * stride, stride) : default;

            for (var i = 0; i < stride; i++)
            {
                var left = i >= bytesPerPixel ? current[i - bytesPerPixel] : 0;
                var above = prior.IsEmpty ? 0 : prior[i];
                var upperLeft = prior.IsEmpty || i < bytesPerPixel ? 0 : prior[i - bytesPerPixel];

                var value = filter switch
                {
                    0 => line[i],
                    1 => line[i] + left,
                    2 => line[i] + above,
                    3 => line[i] + ((left + above) >> 1),
                    4 => line[i] + Paeth(left, above, upperLeft),
                    _ => throw new InvalidDataException($"Row {y} uses unknown filter {filter}."),
                };

                current[i] = (byte)value;
            }
        }

        return pixels;
    }

    private static int Paeth(int left, int above, int upperLeft)
    {
        var estimate = left + above - upperLeft;
        var distanceLeft = Math.Abs(estimate - left);
        var distanceAbove = Math.Abs(estimate - above);
        var distanceUpperLeft = Math.Abs(estimate - upperLeft);

        return distanceLeft <= distanceAbove && distanceLeft <= distanceUpperLeft
            ? left
            : distanceAbove <= distanceUpperLeft ? above : upperLeft;
    }

    private static uint ComputeCrc(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFU;
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? 0xEDB88320U ^ (crc >> 1) : crc >> 1;
            }
        }

        return crc ^ 0xFFFFFFFFU;
    }
}
