using Microsoft.Extensions.Logging;

namespace Imaging.Core;

/// <summary>
/// The library's log messages, defined once as source-generated delegates.
/// </summary>
/// <remarks>
/// Going through the generator rather than the <c>ILogger</c> extension methods keeps every message
/// allocation-free when its level is disabled, which matters because some of these sit next to a render
/// loop.
/// </remarks>
internal static partial class Log
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Rendering {Width}x{Height} from {LayerCount} layer(s) in {BandCount} band(s) on {Workers} worker(s).")]
    internal static partial void RenderStarted(ILogger logger, int width, int height, int layerCount, int bandCount, int workers);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Render {Percent}% complete ({BandsDone}/{BandCount} bands).")]
    internal static partial void RenderProgress(ILogger logger, int percent, int bandsDone, int bandCount);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Information,
        Message = "Reduced parallelism from {Requested} to {Allowed} worker(s) to stay within {BudgetBytes} bytes of band buffers.")]
    internal static partial void ParallelismReduced(ILogger logger, int requested, int allowed, long budgetBytes);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Information,
        Message = "Rendered {Width}x{Height} in {Seconds:F2}s.")]
    internal static partial void RenderCompleted(ILogger logger, int width, int height, double seconds);
}
