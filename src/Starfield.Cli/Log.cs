using Microsoft.Extensions.Logging;

namespace Starfield.Cli;

/// <summary>
/// The tool's log messages, defined once as source-generated delegates.
/// </summary>
internal static partial class Log
{
    [LoggerMessage(
        EventId = 3000,
        Level = LogLevel.Information,
        Message = "Loaded preset {Path}.")]
    internal static partial void PresetLoaded(ILogger logger, string path);

    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Information,
        Message = "Wrote preset {Path}.")]
    internal static partial void PresetWritten(ILogger logger, string path);

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Error,
        Message = "Invalid options: {Problem}")]
    internal static partial void InvalidOptions(ILogger logger, string problem);

    [LoggerMessage(
        EventId = 3003,
        Level = LogLevel.Information,
        Message = "Wrote {Count} layer file(s) to {Directory}.")]
    internal static partial void LayersWritten(ILogger logger, int count, string directory);

    [LoggerMessage(
        EventId = 3004,
        Level = LogLevel.Error,
        Message = "{Action} failed.")]
    internal static partial void Failed(ILogger logger, Exception exception, string action);

    [LoggerMessage(
        EventId = 3005,
        Level = LogLevel.Warning,
        Message = "Cancelled. Any partly written file should be discarded.")]
    internal static partial void Cancelled(ILogger logger);
}
