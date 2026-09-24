using Microsoft.Extensions.Logging;

namespace Starfield.Core;

/// <summary>
/// The star field library's log messages, defined once as source-generated delegates.
/// </summary>
internal static partial class Log
{
    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Information,
        Message = "Compositing {Width}x{Height} star field from seed {Seed} with {LayerCount} layer(s).")]
    internal static partial void CompositeStarted(ILogger logger, int width, int height, ulong seed, int layerCount);

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "Rendering layer '{Layer}' on its own to {Path}.")]
    internal static partial void LayerStarted(ILogger logger, string layer, string path);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Information,
        Message = "Wrote {Path} ({Bytes} bytes).")]
    internal static partial void FileWritten(ILogger logger, string path, long bytes);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Debug,
        Message = "Seed {Seed} chose nebula palette '{Palette}', nudged by {Turns} turn(s).")]
    internal static partial void NebulaPaletteChosen(ILogger logger, ulong seed, string palette, float turns);
}
