using System.Diagnostics;
using Imaging.Core.Png;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Imaging.Core.Rendering;

/// <summary>
/// Drives a set of layers over an image band by band and streams the result out as a PNG.
/// </summary>
/// <remarks>
/// <para>
/// This is the piece that makes image size independent of memory. Bands are rendered in parallel into a
/// fixed pool of buffers and then written in strict top-to-bottom order, so the only thing that grows
/// with image height is the output file.
/// </para>
/// <para>
/// A renderer holds no per-render state and may be reused for any number of renders, including
/// concurrent ones.
/// </para>
/// </remarks>
/// <remarks>Creates a renderer.</remarks>
/// <param name="logger">
/// Where lifecycle and progress go. Pass <see langword="null"/> for a silent renderer.
/// </param>
public sealed class BandedImageRenderer(ILogger<BandedImageRenderer>? logger = null)
{
    private readonly ILogger<BandedImageRenderer> _logger = logger ?? NullLogger<BandedImageRenderer>.Instance;

    /// <summary>Renders every layer over the whole image and writes a complete PNG.</summary>
    /// <param name="destination">
    /// Where the PNG goes. Must be writable. The stream is left open and is not seeked, so it may be a
    /// pipe or a network stream.
    /// </param>
    /// <param name="size">The image dimensions.</param>
    /// <param name="layers">
    /// The layers to composite, applied in order. An empty list is legal and yields the background.
    /// </param>
    /// <param name="options">Band size, parallelism and encoding. Must pass its own validation.</param>
    /// <param name="cancellationToken">
    /// Cancels the render. The destination is then left holding a partial file, which the caller is
    /// responsible for discarding.
    /// </param>
    /// <exception cref="ArgumentNullException">Any reference argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="options"/> is not valid.</exception>
    /// <exception cref="OperationCanceledException">The token was signalled.</exception>
    public void Render(
        Stream destination,
        ImageSize size,
        IReadOnlyList<ILayerRenderer> layers,
        BandedRenderOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(layers);
        ArgumentNullException.ThrowIfNull(options);

        var problems = options.Validate();
        if (problems.Count > 0)
        {
            throw new ArgumentException(string.Join(" ", problems), nameof(options));
        }

        var bandHeight = Math.Min(options.BandHeight, size.Height);
        var bandCount = ((size.Height - 1) / bandHeight) + 1;

        var colorType = options.Encoding.ColorType;
        var bytesPerPixel = PngStreamWriter.BytesPerPixel(colorType);
        var bytesPerRow = size.Width * bytesPerPixel;

        var requested = Math.Clamp(
            options.MaxDegreeOfParallelism > 0 ? options.MaxDegreeOfParallelism : Environment.ProcessorCount,
            1,
            bandCount);
        var workers = LimitWorkersToBudget(requested, bandHeight, size.Width, bytesPerPixel, options.MaxWorkingSetBytes);

        var buffers = new RgbBandBuffer[workers];
        var encoded = new byte[workers][];
        for (var slot = 0; slot < workers; slot++)
        {
            buffers[slot] = new RgbBandBuffer(size.Width, bandHeight);
            encoded[slot] = new byte[BandEncoder.RequiredLength(size.Width, bandHeight, colorType)];
        }

        Log.RenderStarted(_logger, size.Width, size.Height, layers.Count, bandCount, workers);
        var startedAt = Stopwatch.GetTimestamp();

        using var png = new PngStreamWriter(
            destination,
            size.Width,
            size.Height,
            colorType,
            options.CompressionLevel,
            leaveOpen: true);

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = workers,
            CancellationToken = cancellationToken,
        };

        var reportedPercent = 0;

        for (var firstBand = 0; firstBand < bandCount; firstBand += workers)
        {
            var inFlight = Math.Min(workers, bandCount - firstBand);

            Parallel.For(0, inFlight, parallelOptions, slot =>
            {
                var band = DescribeBand(size, bandHeight, firstBand + slot);
                var buffer = buffers[slot];
                buffer.Height = band.Height;
                buffer.Clear();

                for (var layer = 0; layer < layers.Count; layer++)
                {
                    layers[layer].RenderBand(band, buffer, cancellationToken);
                }

                BandEncoder.Encode(buffer, band, options.Encoding, encoded[slot]);
            });

            for (var slot = 0; slot < inFlight; slot++)
            {
                var band = DescribeBand(size, bandHeight, firstBand + slot);
                png.WriteRows(encoded[slot].AsSpan(0, band.Height * bytesPerRow));
            }

            reportedPercent = ReportProgress(firstBand + inFlight, bandCount, reportedPercent);
            options.Progress?.Report((double)(firstBand + inFlight) / bandCount);
        }

        png.Complete();

        var seconds = Stopwatch.GetElapsedTime(startedAt).TotalSeconds;
        Log.RenderCompleted(_logger, size.Width, size.Height, seconds);
    }

    /// <summary>
    /// Drops workers until the pool of band buffers fits the memory budget.
    /// </summary>
    /// <param name="requested">How many workers were asked for.</param>
    /// <param name="bandHeight">Rows per band.</param>
    /// <param name="width">Image width in pixels.</param>
    /// <param name="bytesPerPixel">Bytes one encoded pixel occupies.</param>
    /// <param name="budget">The ceiling in bytes, or zero for no ceiling.</param>
    /// <returns>The worker count to use, never below one.</returns>
    private int LimitWorkersToBudget(int requested, int bandHeight, int width, int bytesPerPixel, long budget)
    {
        if (budget <= 0)
        {
            return requested;
        }

        // Each worker holds one float RGB accumulation buffer plus one encoded band.
        var perWorker = (long)bandHeight * width * ((3 * sizeof(float)) + bytesPerPixel);
        var affordable = (int)Math.Clamp(budget / perWorker, 1, requested);

        if (affordable < requested)
        {
            Log.ParallelismReduced(_logger, requested, affordable, budget);
        }

        return affordable;
    }

    private static BandRegion DescribeBand(ImageSize size, int bandHeight, int bandIndex)
    {
        var top = bandIndex * bandHeight;
        return new BandRegion(size, top, Math.Min(bandHeight, size.Height - top));
    }

    /// <summary>
    /// Logs progress at ten-percent steps, which keeps a long render legible without one line per band.
    /// </summary>
    /// <param name="bandsDone">How many bands have been written.</param>
    /// <param name="bandCount">How many bands the image has.</param>
    /// <param name="reportedPercent">The last percentage already logged.</param>
    /// <returns>The percentage now logged, to pass back on the next call.</returns>
    private int ReportProgress(int bandsDone, int bandCount, int reportedPercent)
    {
        var percent = (int)((long)bandsDone * 100 / bandCount);
        if (percent < reportedPercent + 10 && bandsDone < bandCount)
        {
            return reportedPercent;
        }

        Log.RenderProgress(_logger, percent, bandsDone, bandCount);
        return percent;
    }
}
