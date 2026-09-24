using Starfield.App.Rendering;
using Starfield.Core.Options;

namespace Starfield.App.Tests;

/// <summary>A renderer that reports progress and either finishes, fails or waits to be cancelled.</summary>
internal sealed class FakeRenderService : IRenderService
{
    public bool WaitForCancellation { get; set; }

    public Exception? Failure { get; set; }

    public byte[] Png { get; set; } = [1, 2, 3];

    public StarfieldOptions? LastOptions { get; private set; }

    public string? LastOutputPath { get; private set; }

    public StarfieldOptions? LastPngOptions { get; private set; }

    public async Task RenderAsync(
        StarfieldOptions options,
        string outputPath,
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        LastOptions = options;
        LastOutputPath = outputPath;
        progress.Report(0.5);

        if (WaitForCancellation)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        if (Failure is not null)
        {
            throw Failure;
        }

        await Task.Yield();
    }

    public async Task<byte[]> RenderPngAsync(StarfieldOptions options, CancellationToken cancellationToken)
    {
        LastPngOptions = options;
        await Task.Yield();
        return Png;
    }
}
