using System.Buffers.Binary;
using System.IO.Compression;
using static System.FormattableString;

namespace Imaging.Core.Png;

/// <summary>
/// Writes a PNG one row at a time, so an image never has to exist in memory all at once.
/// </summary>
/// <remarks>
/// <para>
/// This is what makes very wide output practical. Rows are filtered, compressed and flushed as they
/// arrive, so peak memory is a function of the row length and the caller's band size, not of the image
/// area. A 10000×1080 image costs the same working set as a 10000×64 one.
/// </para>
/// <para>
/// Usage is strictly sequential: construct, call <see cref="WriteRows"/> until exactly
/// <see cref="Height"/> rows have been written, then <see cref="Complete"/> (or dispose, which
/// completes for you). Rows arrive in top-to-bottom order and cannot be revisited.
/// </para>
/// <para>
/// Instances are not thread-safe. One writer belongs to one thread; parallelism belongs upstream, in
/// whatever produces the rows.
/// </para>
/// </remarks>
public sealed class PngStreamWriter : IDisposable
{
    private const int DefaultIdatChunkSize = 1 << 20;

    private static ReadOnlySpan<byte> Signature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private readonly Stream _destination;
    private readonly bool _leaveOpen;
    private readonly IdatChunkWriter _chunks;
    private readonly ZLibStream _compressor;
    private readonly int _bytesPerPixel;
    private readonly byte[] _previousRow;
    private readonly byte[] _filteredRow;
    private bool _completed;
    private bool _disposed;

    /// <summary>Starts a PNG, writing the signature and header immediately.</summary>
    /// <param name="destination">
    /// The stream to write to. It must be writable, and it is not rewound or seeked, so a network or
    /// pipe stream works. Ownership stays with the caller unless <paramref name="leaveOpen"/> is
    /// <see langword="false"/>.
    /// </param>
    /// <param name="width">Image width in pixels. Must be positive.</param>
    /// <param name="height">Image height in pixels. Must be positive.</param>
    /// <param name="colorType">Whether an alpha channel is stored.</param>
    /// <param name="compressionLevel">
    /// How hard Deflate works. <see cref="CompressionLevel.Fastest"/> roughly halves encode time on
    /// large images at a cost in file size.
    /// </param>
    /// <param name="leaveOpen">
    /// <see langword="true"/> to leave <paramref name="destination"/> open when this writer is disposed.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/> or <paramref name="height"/> is not positive.</exception>
    /// <exception cref="ArgumentException"><paramref name="destination"/> is not writable.</exception>
    public PngStreamWriter(
        Stream destination,
        int width,
        int height,
        PngColorType colorType,
        CompressionLevel compressionLevel = CompressionLevel.Optimal,
        bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        if (!destination.CanWrite)
        {
            throw new ArgumentException("The destination stream must be writable.", nameof(destination));
        }

        _destination = destination;
        _leaveOpen = leaveOpen;
        Width = width;
        Height = height;
        ColorType = colorType;
        _bytesPerPixel = BytesPerPixel(colorType);
        BytesPerRow = width * _bytesPerPixel;

        _previousRow = new byte[BytesPerRow];
        _filteredRow = new byte[BytesPerRow + 1];

        WriteSignatureAndHeader();

        _chunks = new IdatChunkWriter(destination, DefaultIdatChunkSize);
        _compressor = new ZLibStream(_chunks, compressionLevel, leaveOpen: true);
    }

    /// <summary>Gets the image width in pixels.</summary>
    /// <value>The width passed to the constructor.</value>
    public int Width { get; }

    /// <summary>Gets the image height in pixels.</summary>
    /// <value>The height passed to the constructor.</value>
    public int Height { get; }

    /// <summary>Gets the colour type being written.</summary>
    /// <value>The colour type passed to the constructor.</value>
    public PngColorType ColorType { get; }

    /// <summary>Gets the length of one unfiltered row.</summary>
    /// <value><see cref="Width"/> multiplied by three for RGB, or by four for RGBA.</value>
    public int BytesPerRow { get; }

    /// <summary>Gets how many rows have been written so far.</summary>
    /// <value>Starts at zero and must reach <see cref="Height"/> before the file is complete.</value>
    public int RowsWritten { get; private set; }

    /// <summary>Reports how many bytes one row of the given colour type occupies per pixel.</summary>
    /// <param name="colorType">The colour type to measure.</param>
    /// <returns>Three for RGB, four for RGBA.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="colorType"/> is not a known value.</exception>
    public static int BytesPerPixel(PngColorType colorType) => colorType switch
    {
        PngColorType.Rgb => 3,
        PngColorType.Rgba => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(colorType)),
    };

    /// <summary>Appends one or more complete rows of pixel data.</summary>
    /// <param name="rows">
    /// Unfiltered pixel bytes for a whole number of rows, in the channel order implied by
    /// <see cref="ColorType"/>. The buffer is read, never retained.
    /// </param>
    /// <exception cref="ArgumentException">
    /// The length of <paramref name="rows"/> is not a whole multiple of <see cref="BytesPerRow"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The writer has already been completed, or the rows would take the total past <see cref="Height"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
    public void WriteRows(ReadOnlySpan<byte> rows)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_completed)
        {
            throw new InvalidOperationException("The PNG has already been completed.");
        }

        if (rows.Length % BytesPerRow != 0)
        {
            throw new ArgumentException(
                Invariant($"Row data must be a whole number of {BytesPerRow}-byte rows but was {rows.Length} bytes."),
                nameof(rows));
        }

        var count = rows.Length / BytesPerRow;
        if (RowsWritten + count > Height)
        {
            throw new InvalidOperationException(
                Invariant($"Writing {count} more rows would exceed the declared height of {Height}."));
        }

        for (var row = 0; row < count; row++)
        {
            var raw = rows.Slice(row * BytesPerRow, BytesPerRow);
            FilterRow(raw);
            _compressor.Write(_filteredRow);
            raw.CopyTo(_previousRow);
            RowsWritten++;
        }
    }

    /// <summary>Finishes the file: flushes compression and writes the trailing chunk.</summary>
    /// <remarks>Calling this more than once is harmless; the second call does nothing.</remarks>
    /// <exception cref="InvalidOperationException">Fewer than <see cref="Height"/> rows have been written.</exception>
    /// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
    public void Complete()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_completed)
        {
            return;
        }

        if (RowsWritten != Height)
        {
            throw new InvalidOperationException(
                Invariant($"The PNG declares {Height} rows but only {RowsWritten} were written."));
        }

        _compressor.Dispose();
        _chunks.FlushChunk();
        WriteChunk(_destination, "IEND"u8, []);
        _destination.Flush();
        _completed = true;
    }

    /// <summary>
    /// Completes the file if it is not already complete, then releases the compressor.
    /// </summary>
    /// <remarks>
    /// Disposing a writer that has received every row produces a valid PNG. Disposing one that has not
    /// leaves a truncated file rather than throwing, because disposal runs on failure paths too.
    /// </remarks>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (!_completed && RowsWritten == Height)
        {
            Complete();
        }
        else if (!_completed)
        {
            _compressor.Dispose();
            _chunks.FlushChunk();
        }

        _chunks.Dispose();
        _disposed = true;

        if (!_leaveOpen)
        {
            _destination.Dispose();
        }
    }

    private static void WriteChunk(Stream destination, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> header = stackalloc byte[8];
        BinaryPrimitives.WriteUInt32BigEndian(header, (uint)data.Length);
        type.CopyTo(header[4..]);
        destination.Write(header);
        destination.Write(data);

        var crc = Crc32.Finish(Crc32.Update(Crc32.Update(Crc32.Start(), type), data));
        Span<byte> trailer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(trailer, crc);
        destination.Write(trailer);
    }

    private static int PaethPredictor(int left, int above, int upperLeft)
    {
        var estimate = left + above - upperLeft;
        var distanceLeft = Math.Abs(estimate - left);
        var distanceAbove = Math.Abs(estimate - above);
        var distanceUpperLeft = Math.Abs(estimate - upperLeft);

        return distanceLeft <= distanceAbove && distanceLeft <= distanceUpperLeft
            ? left
            : distanceAbove <= distanceUpperLeft ? above : upperLeft;
    }

    private static int AbsoluteDelta(int value) => (sbyte)value < 0 ? 256 - value : value;

    private void WriteSignatureAndHeader()
    {
        _destination.Write(Signature);

        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, Width);
        BinaryPrimitives.WriteInt32BigEndian(header[4..], Height);
        header[8] = 8;                    // Bit depth.
        header[9] = (byte)ColorType;
        header[10] = 0;                   // Compression method: Deflate.
        header[11] = 0;                   // Filter method: adaptive.
        header[12] = 0;                   // Interlace method: none.
        WriteChunk(_destination, "IHDR"u8, header);
    }

    /// <summary>
    /// Picks the filter that makes the row cheapest to compress and materialises it into
    /// <see cref="_filteredRow"/>, with the filter tag in the leading byte.
    /// </summary>
    /// <param name="raw">The unfiltered row.</param>
    private void FilterRow(ReadOnlySpan<byte> raw)
    {
        // The heuristic from the PNG specification: the filter whose output has the smallest sum of
        // absolute signed byte values usually compresses best, and is far cheaper than trying Deflate.
        byte best = 0;
        var bestScore = long.MaxValue;

        for (byte filter = 0; filter <= 4; filter++)
        {
            var score = ScoreFilter(filter, raw);
            if (score < bestScore)
            {
                bestScore = score;
                best = filter;
            }
        }

        _filteredRow[0] = best;
        ApplyFilter(best, raw, _filteredRow.AsSpan(1));
    }

    private long ScoreFilter(byte filter, ReadOnlySpan<byte> raw)
    {
        ReadOnlySpan<byte> prior = _previousRow;
        var bpp = _bytesPerPixel;
        long score = 0;

        switch (filter)
        {
            case 0:
                foreach (var value in raw)
                {
                    score += AbsoluteDelta(value);
                }

                break;

            case 1:
                for (var i = 0; i < raw.Length; i++)
                {
                    var left = i >= bpp ? raw[i - bpp] : 0;
                    score += AbsoluteDelta((raw[i] - left) & 0xFF);
                }

                break;

            case 2:
                for (var i = 0; i < raw.Length; i++)
                {
                    score += AbsoluteDelta((raw[i] - prior[i]) & 0xFF);
                }

                break;

            case 3:
                for (var i = 0; i < raw.Length; i++)
                {
                    var left = i >= bpp ? raw[i - bpp] : 0;
                    score += AbsoluteDelta((raw[i] - ((left + prior[i]) >> 1)) & 0xFF);
                }

                break;

            default:
                for (var i = 0; i < raw.Length; i++)
                {
                    var left = i >= bpp ? raw[i - bpp] : 0;
                    var upperLeft = i >= bpp ? prior[i - bpp] : 0;
                    score += AbsoluteDelta((raw[i] - PaethPredictor(left, prior[i], upperLeft)) & 0xFF);
                }

                break;
        }

        return score;
    }

    private void ApplyFilter(byte filter, ReadOnlySpan<byte> raw, Span<byte> destination)
    {
        ReadOnlySpan<byte> prior = _previousRow;
        var bpp = _bytesPerPixel;

        switch (filter)
        {
            case 0:
                raw.CopyTo(destination);
                break;

            case 1:
                for (var i = 0; i < raw.Length; i++)
                {
                    var left = i >= bpp ? raw[i - bpp] : 0;
                    destination[i] = (byte)(raw[i] - left);
                }

                break;

            case 2:
                for (var i = 0; i < raw.Length; i++)
                {
                    destination[i] = (byte)(raw[i] - prior[i]);
                }

                break;

            case 3:
                for (var i = 0; i < raw.Length; i++)
                {
                    var left = i >= bpp ? raw[i - bpp] : 0;
                    destination[i] = (byte)(raw[i] - ((left + prior[i]) >> 1));
                }

                break;

            default:
                for (var i = 0; i < raw.Length; i++)
                {
                    var left = i >= bpp ? raw[i - bpp] : 0;
                    var upperLeft = i >= bpp ? prior[i - bpp] : 0;
                    destination[i] = (byte)(raw[i] - PaethPredictor(left, prior[i], upperLeft));
                }

                break;
        }
    }

    /// <summary>
    /// Collects compressed bytes and emits them as fixed-size IDAT chunks.
    /// </summary>
    /// <remarks>
    /// Buffering to a chunk size rather than to the whole image is the second half of the streaming
    /// story: without it the compressed image would have to be held in memory to learn its length
    /// before the single chunk header could be written.
    /// </remarks>
    private sealed class IdatChunkWriter : Stream
    {
        private readonly Stream _destination;
        private readonly byte[] _buffer;
        private int _count;

        internal IdatChunkWriter(Stream destination, int chunkSize)
        {
            _destination = destination;
            _buffer = new byte[chunkSize];
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => _destination.Flush();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            Write(buffer.AsSpan(offset, count));
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            var remaining = buffer;
            while (!remaining.IsEmpty)
            {
                var room = _buffer.Length - _count;
                var take = Math.Min(room, remaining.Length);
                remaining[..take].CopyTo(_buffer.AsSpan(_count));
                _count += take;
                remaining = remaining[take..];

                if (_count == _buffer.Length)
                {
                    FlushChunk();
                }
            }
        }

        public override void WriteByte(byte value) => Write([value]);

        /// <summary>Emits whatever is buffered as one IDAT chunk.</summary>
        internal void FlushChunk()
        {
            if (_count == 0)
            {
                return;
            }

            WriteChunk(_destination, "IDAT"u8, _buffer.AsSpan(0, _count));
            _count = 0;
        }
    }
}
