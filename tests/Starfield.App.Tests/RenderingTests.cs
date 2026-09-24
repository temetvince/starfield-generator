using Starfield.App.Rendering;
using Starfield.Core.Options;

namespace Starfield.App.Tests;

/// <summary>Checks the in-process renderer.</summary>
public sealed class RenderingTests
{
    [Fact]
    public async Task StarfieldRenderService_WritesAPngAndReportsProgress()
    {
        var directory = Directory.CreateTempSubdirectory("starfield-render");
        var path = Path.Combine(directory.FullName, "sky.png");
        var progress = new List<double>();
        var options = StarfieldPresets.Default() with { Width = 96, Height = 48, Seed = 3 };

        try
        {
            await new StarfieldRenderService().RenderAsync(options, path, new SynchronousProgress(progress.Add), CancellationToken.None);

            Assert.True(File.Exists(path));
            Assert.True(new FileInfo(path).Length > 0);
            Assert.Equal(1.0, progress[^1]);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task StarfieldRenderService_RenderPng_ReturnsAPngFile()
    {
        var options = StarfieldPresets.Default() with { Width = 64, Height = 32, Seed = 5 };

        var png = await new StarfieldRenderService().RenderPngAsync(options, CancellationToken.None);

        Assert.True(png.Length > 8);
        Assert.Equal<byte>([0x89, 0x50, 0x4E, 0x47], png[..4]);
    }

    [Fact]
    public async Task StarfieldRenderService_Cancellation_Throws()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var options = StarfieldPresets.Default() with { Width = 96, Height = 48 };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new StarfieldRenderService().RenderAsync(options, "never.png", new SynchronousProgress(_ => { }), cancellation.Token));
    }

    private sealed class SynchronousProgress(Action<double> handler) : IProgress<double>
    {
        public void Report(double value) => handler(value);
    }
}
