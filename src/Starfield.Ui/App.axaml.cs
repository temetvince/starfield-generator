using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Starfield.App.Rendering;
using Starfield.App.ViewModels;

namespace Starfield.Ui;

/// <summary>The application: always dark, one window, rendering in this process.</summary>
public sealed partial class App : Application
{
    /// <inheritdoc/>
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc/>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow { DataContext = new MainViewModel(new StarfieldRenderService()) };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
