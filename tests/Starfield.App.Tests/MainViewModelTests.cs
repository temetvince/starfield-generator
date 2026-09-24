using Starfield.App.Rendering;
using Starfield.App.ViewModels;

namespace Starfield.App.Tests;

/// <summary>Checks the window state: rendering, cancelling, failing and the backdrop.</summary>
public sealed class MainViewModelTests
{
    [Fact]
    public void PreviewArea_ShowsThePlaceholderUntilSomethingHappens()
    {
        var viewModel = new MainViewModel(new FakeRenderService());

        Assert.Equal("", viewModel.Status);
        Assert.False(viewModel.HasStatus);
        Assert.False(viewModel.HasNotice);
        Assert.True(viewModel.ShowPlaceholder);
        Assert.False(viewModel.ShowNoticeCentred);
        Assert.False(viewModel.ShowNoticeCaption);
    }

    [Fact]
    public void PreviewArea_ShowsProblemsInRedInTheMiddle()
    {
        var viewModel = new MainViewModel(new FakeRenderService());

        viewModel.Request.Width = "wide";

        Assert.Equal(viewModel.Problems, viewModel.Notice);
        Assert.Equal("#FF8A9B", viewModel.NoticeColour);
        Assert.True(viewModel.ShowNoticeCentred);
        Assert.False(viewModel.ShowPlaceholder);
    }

    [Fact]
    public void Problems_FollowTheForm()
    {
        var viewModel = new MainViewModel(new FakeRenderService());

        viewModel.Request.Width = "wide";

        Assert.True(viewModel.HasProblems);
        Assert.Contains("Width must be a whole number", viewModel.Problems, StringComparison.Ordinal);

        viewModel.Request.Width = "800";

        Assert.False(viewModel.HasProblems);
    }

    [Fact]
    public async Task Render_ProgressesAndShowsTheImage()
    {
        var renderer = new FakeRenderService();
        var viewModel = new MainViewModel(renderer);
        viewModel.Request.Seed = "7";
        viewModel.Request.OutputPath = "sky.png";

        await InvokeRenderAsync(viewModel);

        Assert.Equal(7UL, renderer.LastOptions!.Seed);
        Assert.Equal(Path.GetFullPath("sky.png"), renderer.LastOutputPath);
        Assert.True(viewModel.HasPreview);
        Assert.Equal(Path.GetFullPath("sky.png"), viewModel.PreviewPath);
        Assert.StartsWith("Saved ", viewModel.Status, StringComparison.Ordinal);
        Assert.True(viewModel.HasStatus);
        Assert.True(viewModel.ShowNoticeCaption);
        Assert.Equal("#B9BFE3", viewModel.NoticeColour);
        Assert.False(viewModel.ShowPlaceholder);
        Assert.False(viewModel.IsRunning);
    }

    [Fact]
    public async Task Render_WithProblems_DoesNotRender()
    {
        var renderer = new FakeRenderService();
        var viewModel = new MainViewModel(renderer);
        viewModel.Request.Height = "tall";

        await InvokeRenderAsync(viewModel);

        Assert.Null(renderer.LastOptions);
        Assert.False(viewModel.HasStatus);
        Assert.True(viewModel.HasProblems);
    }

    [Fact]
    public async Task Render_Failure_IsReportedNotThrown()
    {
        var renderer = new FakeRenderService { Failure = new IOException("disk full") };
        var viewModel = new MainViewModel(renderer);

        await InvokeRenderAsync(viewModel);

        Assert.Equal("Rendering failed: disk full", viewModel.Status);
        Assert.False(viewModel.HasPreview);
        Assert.True(viewModel.ShowNoticeCentred);
        Assert.False(viewModel.IsRunning);
    }

    [Fact]
    public async Task Cancel_StopsTheRenderAndSaysSo()
    {
        var viewModel = new MainViewModel(new FakeRenderService { WaitForCancellation = true });

        var run = InvokeRenderAsync(viewModel);
        Assert.True(viewModel.IsRunning);
        Assert.True(viewModel.CancelCommand.CanExecute(null));
        Assert.False(viewModel.HasStatus);
        Assert.False(viewModel.ShowPlaceholder);
        Assert.False(viewModel.ShowNoticeCentred);

        viewModel.CancelCommand.Execute(null);
        await run;

        Assert.False(viewModel.IsRunning);
        Assert.StartsWith("Cancelled", viewModel.Status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadBackdrop_RendersAScreenSizedSkyIntoMemory()
    {
        var renderer = new FakeRenderService { Png = [9, 8, 7] };
        var viewModel = new MainViewModel(renderer);
        Assert.False(viewModel.HasBackdrop);

        await viewModel.LoadBackdropAsync();

        Assert.True(viewModel.HasBackdrop);
        Assert.Equal<byte>([9, 8, 7], viewModel.BackdropPng!);
        Assert.Equal(Backdrop.Width, renderer.LastPngOptions!.Width);
        Assert.Equal(Backdrop.Height, renderer.LastPngOptions.Height);
        Assert.False(renderer.LastPngOptions.SeamlessX);
        Assert.Empty(renderer.LastPngOptions.Validate());
    }

    [Fact]
    public void RandomSeedCommand_FillsTheSeed()
    {
        var viewModel = new MainViewModel(new FakeRenderService());

        viewModel.RandomSeedCommand.Execute(null);

        Assert.NotEmpty(viewModel.Request.Seed);
        Assert.False(viewModel.HasProblems);
    }

    private static Task InvokeRenderAsync(MainViewModel viewModel)
    {
        var completion = new TaskCompletionSource();
        viewModel.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(MainViewModel.IsRunning) && !viewModel.IsRunning)
            {
                completion.TrySetResult();
            }
        };

        viewModel.RenderCommand.Execute(null);

        // A render that never started (invalid form) never toggles IsRunning, so do not wait for it.
        return viewModel.IsRunning ? completion.Task : Task.CompletedTask;
    }
}
