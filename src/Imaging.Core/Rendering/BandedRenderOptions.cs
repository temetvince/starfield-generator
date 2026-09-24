using System.IO.Compression;
using static System.FormattableString;

namespace Imaging.Core.Rendering;

/// <summary>
/// How a render is divided into bands, how many run at once, and how the result is encoded.
/// </summary>
/// <remarks>
/// Band height is the memory dial. Peak working set is roughly
/// <c>width × BandHeight × 16 bytes × workers</c>, so a taller band buys slightly better throughput at
/// a directly proportional cost in memory. The default suits images tens of thousands of pixels wide.
/// </remarks>
public sealed record BandedRenderOptions
{
    /// <summary>Gets how many image rows each band covers.</summary>
    /// <value>
    /// At least one. Bands taller than the image are clipped to it, so a small image still renders in a
    /// single band.
    /// </value>
    public int BandHeight { get; init; } = 64;

    /// <summary>Gets how many bands render at the same time.</summary>
    /// <value>
    /// Zero, the default, means one worker per logical processor. Values above the band count are
    /// clamped down.
    /// </value>
    public int MaxDegreeOfParallelism { get; init; }

    /// <summary>Gets the ceiling on memory held by in-flight bands.</summary>
    /// <value>
    /// In bytes, 512 MiB by default. Zero removes the ceiling. Workers are dropped until the pool fits,
    /// which is what stops a very wide image from turning a high processor count into gigabytes of
    /// scratch. Parallelism never falls below one worker, so the budget slows a render down rather than
    /// failing it.
    /// </value>
    public long MaxWorkingSetBytes { get; init; } = 512L * 1024 * 1024;

    /// <summary>Gets how hard Deflate works on the pixel data.</summary>
    /// <value>
    /// <see cref="CompressionLevel.Optimal"/> by default.
    /// <see cref="CompressionLevel.Fastest"/> is markedly quicker on very large images.
    /// </value>
    public CompressionLevel CompressionLevel { get; init; } = CompressionLevel.Optimal;

    /// <summary>Gets the settings for the final encode step.</summary>
    /// <value>Never <see langword="null"/>.</value>
    public ImageEncodingOptions Encoding { get; init; } = new();

    /// <summary>Gets where completion is reported as the render advances.</summary>
    /// <value>
    /// <see langword="null"/> for no reporting. Otherwise it receives the fraction of bands written, in
    /// <c>[0, 1]</c>, once per batch of bands and always ending with one, from the rendering thread.
    /// </value>
    public IProgress<double>? Progress { get; init; }

    /// <summary>Reports why these options cannot be used, if they cannot.</summary>
    /// <returns>A human-readable list of problems, empty when the options are valid.</returns>
    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();

        if (BandHeight < 1)
        {
            problems.Add(Invariant($"BandHeight must be at least 1 but was {BandHeight}."));
        }

        if (MaxDegreeOfParallelism < 0)
        {
            problems.Add(Invariant($"MaxDegreeOfParallelism must not be negative but was {MaxDegreeOfParallelism}."));
        }

        if (MaxWorkingSetBytes < 0)
        {
            problems.Add(Invariant($"MaxWorkingSetBytes must not be negative but was {MaxWorkingSetBytes}."));
        }

        problems.AddRange(Encoding.Validate());
        return problems;
    }
}
