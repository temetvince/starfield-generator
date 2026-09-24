namespace Starfield.Cli;

/// <summary>
/// The process exit codes the tool returns, so a caller can tell why a run stopped.
/// </summary>
public static class ExitCodes
{
    /// <summary>The image, layers or preset were written.</summary>
    public const int Success = 0;

    /// <summary>The request was not usable: an unknown option, an unreadable value, invalid settings or a bad preset.</summary>
    public const int UsageError = 1;

    /// <summary>The work failed: the output could not be written.</summary>
    public const int Failure = 2;

    /// <summary>Cancelled with Ctrl+C. Any partly written file should be discarded.</summary>
    public const int Cancelled = 3;
}
