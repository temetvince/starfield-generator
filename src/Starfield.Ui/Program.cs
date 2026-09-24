using Avalonia;

namespace Starfield.Ui;

/// <summary>The desktop entry point.</summary>
internal static class Program
{
    /// <summary>Starts the application.</summary>
    /// <param name="args">Command-line arguments, passed through to Avalonia.</param>
    /// <returns>The process exit code.</returns>
    [STAThread]
    public static int Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    /// <summary>Configures Avalonia. Also used by the designer.</summary>
    /// <returns>The configured builder.</returns>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
