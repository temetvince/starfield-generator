using Starfield.App.Mvvm;
using Starfield.App.Rendering;

namespace Starfield.App.ViewModels;

/// <summary>
/// The window's state: the form, the render in progress, the finished image and the backdrop.
/// </summary>
/// <remarks>
/// The preview area shows one thing at a time: a loader while rendering, otherwise the image, a
/// notice, or the placeholder. The <c>Show*</c> properties decide which, so the view needs no logic.
/// </remarks>
public sealed class MainViewModel : ObservableObject
{
    private const string StatusColour = "#B9BFE3";
    private const string ProblemColour = "#FF8A9B";

    private readonly IRenderService _renderer;

    private CancellationTokenSource? _cancellation;

    /// <summary>Creates the window state.</summary>
    /// <param name="renderer">How images are rendered.</param>
    /// <exception cref="ArgumentNullException"><paramref name="renderer"/> is <see langword="null"/>.</exception>
    public MainViewModel(IRenderService renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);

        _renderer = renderer;

        Request = new RenderRequestViewModel();
        RenderCommand = new AsyncRelayCommand(RenderAsync, () => !IsRunning);
        CancelCommand = new RelayCommand(Cancel, () => IsRunning);
        RandomSeedCommand = new RelayCommand(Request.RandomiseSeed);

        Request.PropertyChanged += (_, _) => Refresh();
        Refresh();
    }

    /// <summary>Gets the form.</summary>
    public RenderRequestViewModel Request { get; }

    /// <summary>Gets the command that renders the image.</summary>
    public AsyncRelayCommand RenderCommand { get; }

    /// <summary>Gets the command that stops a render.</summary>
    public RelayCommand CancelCommand { get; }

    /// <summary>Gets the command that picks a random seed.</summary>
    public RelayCommand RandomSeedCommand { get; }

    /// <summary>Gets the form's problems, on one line.</summary>
    public string Problems
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(HasProblems));
                RaiseNoticeChanged();
            }
        }
    } = "";

    /// <summary>Gets whether <see cref="Problems"/> has anything to show.</summary>
    public bool HasProblems => Problems.Length > 0;

    /// <summary>Gets a one-line description of the last thing that happened, or empty when nothing has yet.</summary>
    public string Status
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(HasStatus));
                RaiseNoticeChanged();
            }
        }
    } = "";

    /// <summary>Gets whether <see cref="Status"/> has anything to show.</summary>
    public bool HasStatus => Status.Length > 0;

    /// <summary>Gets whether a render is in progress.</summary>
    public bool IsRunning
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                RenderCommand.RaiseCanExecuteChanged();
                CancelCommand.RaiseCanExecuteChanged();
                RaiseNoticeChanged();
            }
        }
    }

    /// <summary>Gets what the preview area should say: the problems if there are any, else the status.</summary>
    public string Notice => HasProblems ? Problems : Status;

    /// <summary>Gets whether <see cref="Notice"/> has anything to show.</summary>
    public bool HasNotice => Notice.Length > 0;

    /// <summary>Gets the colour for <see cref="Notice"/>: red for problems, grey for everything else.</summary>
    public string NoticeColour => HasProblems ? ProblemColour : StatusColour;

    /// <summary>Gets whether the preview area shows its placeholder: nothing rendering, no image, nothing to say.</summary>
    public bool ShowPlaceholder => !IsRunning && !HasPreview && !HasNotice;

    /// <summary>Gets whether the notice sits in the middle of an otherwise empty preview area.</summary>
    public bool ShowNoticeCentred => !IsRunning && !HasPreview && HasNotice;

    /// <summary>Gets whether the notice sits as a caption over the image.</summary>
    public bool ShowNoticeCaption => !IsRunning && HasPreview && HasNotice;

    /// <summary>Gets the full path of the last image rendered, or empty before the first one.</summary>
    public string PreviewPath
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(HasPreview));
                RaiseNoticeChanged();
            }
        }
    } = "";

    /// <summary>Gets whether there is an image to show.</summary>
    public bool HasPreview => PreviewPath.Length > 0;

    /// <summary>Gets the PNG drawn behind the window, or <see langword="null"/> until <see cref="LoadBackdropAsync"/> has run.</summary>
    public byte[]? BackdropPng
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(HasBackdrop));
            }
        }
    }

    /// <summary>Gets whether the backdrop is ready, which is when the window may show its controls.</summary>
    public bool HasBackdrop => BackdropPng is not null;

    /// <summary>Recomputes the problems from the form.</summary>
    public void Refresh() => Problems = string.Join(" ", Request.Build().Problems);

    /// <summary>Renders a fresh sky for the window's background.</summary>
    /// <returns>A task that completes once <see cref="BackdropPng"/> is set.</returns>
    public async Task LoadBackdropAsync()
    {
        var seed = (ulong)Random.Shared.NextInt64();
        BackdropPng = await _renderer.RenderPngAsync(Backdrop.Options(seed), CancellationToken.None).ConfigureAwait(true);
    }

    private async Task RenderAsync()
    {
        Refresh();
        var built = Request.Build();
        if (!built.IsValid)
        {
            Status = "";
            return;
        }

        var outputPath = Path.GetFullPath(built.OutputPath);

        using var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        IsRunning = true;
        PreviewPath = "";
        Status = "";

        try
        {
            await _renderer
                .RenderAsync(built.Options!, outputPath, NoProgress.Instance, cancellation.Token)
                .ConfigureAwait(true);

            PreviewPath = outputPath;
            Status = $"Saved {outputPath}";
        }
        catch (OperationCanceledException)
        {
            Status = "Cancelled. Discard the partly written file.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Status = $"Rendering failed: {exception.Message}";
        }
        finally
        {
            _cancellation = null;
            IsRunning = false;
        }
    }

    private void Cancel() => _cancellation?.Cancel();

    private void RaiseNoticeChanged()
    {
        OnPropertyChanged(nameof(Notice));
        OnPropertyChanged(nameof(HasNotice));
        OnPropertyChanged(nameof(NoticeColour));
        OnPropertyChanged(nameof(ShowPlaceholder));
        OnPropertyChanged(nameof(ShowNoticeCentred));
        OnPropertyChanged(nameof(ShowNoticeCaption));
    }

    /// <summary>Discards progress: the window shows an indeterminate loader, since band batches report too coarsely to animate.</summary>
    private sealed class NoProgress : IProgress<double>
    {
        public static NoProgress Instance { get; } = new();

        public void Report(double value)
        {
        }
    }
}
