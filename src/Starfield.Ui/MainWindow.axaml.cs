using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Starfield.App.ViewModels;

namespace Starfield.Ui;

/// <summary>The one window: a settings bar over the rendered image, with a sky behind.</summary>
/// <remarks>
/// Everything that needs the platform lives here: the save dialog, loading PNG bytes into brushes and
/// images, and painting the colour-ramp swatch. Everything else is bound to <see cref="MainViewModel"/>.
/// </remarks>
public sealed partial class MainWindow : Window
{
    /// <summary>Creates the window.</summary>
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Follow();
        Opened += OnOpened;
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void Follow()
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        viewModel.PropertyChanged += (_, eventArgs) =>
        {
            switch (eventArgs.PropertyName)
            {
                case nameof(MainViewModel.PreviewPath):
                    ShowPreview(viewModel.PreviewPath);
                    break;
                case nameof(MainViewModel.BackdropPng):
                    ShowBackdrop(viewModel.BackdropPng);
                    break;
                default:
                    break;
            }
        };

        viewModel.Request.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(RenderRequestViewModel.RampStops))
            {
                PaintRamp(viewModel.Request.RampStops);
            }
        };

        PaintRamp(viewModel.Request.RampStops);
    }

    private void PaintRamp(IReadOnlyList<string> stops)
    {
        if (this.FindControl<Border>("RampSwatch") is not { } swatch)
        {
            return;
        }

        if (stops.Count < 2)
        {
            swatch.Background = null;
            return;
        }

        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
        };

        for (var index = 0; index < stops.Count; index++)
        {
            brush.GradientStops.Add(new GradientStop(Color.Parse(stops[index]), (double)index / (stops.Count - 1)));
        }

        swatch.Background = brush;
    }

    private async void OnOpened(object? sender, EventArgs eventArgs)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        try
        {
            await viewModel.LoadBackdropAsync();
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        {
            // The backdrop is decoration; the plain dark background stays if it cannot be drawn.
        }
    }

    private void ShowBackdrop(byte[]? png)
    {
        if (png is null)
        {
            return;
        }

        using var stream = new MemoryStream(png);
        Background = new ImageBrush(new Bitmap(stream)) { Stretch = Stretch.UniformToFill };
    }

    private void ShowPreview(string path)
    {
        if (this.FindControl<Image>("PreviewImage") is not { } image)
        {
            return;
        }

        var previous = image.Source as IDisposable;
        image.Source = null;
        previous?.Dispose();

        if (path.Length > 0 && File.Exists(path))
        {
            image.Source = new Bitmap(path);
        }
    }

    private async void BrowseOutput(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Choose where to save the image",
            SuggestedFileName = Path.GetFileName(viewModel.Request.OutputPath),
            DefaultExtension = "png",
            FileTypeChoices = [new FilePickerFileType("PNG images") { Patterns = ["*.png"] }],
        });

        if (file?.TryGetLocalPath() is { } path)
        {
            viewModel.Request.OutputPath = path;
        }
    }
}
