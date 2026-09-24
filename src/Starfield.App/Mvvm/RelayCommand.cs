using System.Windows.Input;

namespace Starfield.App.Mvvm;

/// <summary>A command that runs a synchronous action.</summary>
/// <param name="execute">What the command does.</param>
/// <param name="canExecute">Whether it may run now, or <see langword="null"/> for always.</param>
public sealed class RelayCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    /// <inheritdoc/>
    public event EventHandler? CanExecuteChanged;

    /// <inheritdoc/>
    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;

    /// <inheritdoc/>
    public void Execute(object? parameter) => execute();

    /// <summary>Tells bound controls to ask <see cref="CanExecute"/> again.</summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>A command that runs an asynchronous action and refuses to overlap itself.</summary>
/// <param name="execute">What the command does. Must handle its own failures; nothing catches them here.</param>
/// <param name="canExecute">Whether it may run now, or <see langword="null"/> for whenever it is not already running.</param>
public sealed class AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null) : ICommand
{
    private bool _running;

    /// <inheritdoc/>
    public event EventHandler? CanExecuteChanged;

    /// <inheritdoc/>
    public bool CanExecute(object? parameter) => !_running && (canExecute?.Invoke() ?? true);

    /// <inheritdoc/>
    /// <remarks>Fire-and-forget by design, because <see cref="ICommand"/> is synchronous.</remarks>
    public async void Execute(object? parameter)
    {
        if (_running)
        {
            return;
        }

        _running = true;
        RaiseCanExecuteChanged();

        try
        {
            await execute().ConfigureAwait(true);
        }
        finally
        {
            _running = false;
            RaiseCanExecuteChanged();
        }
    }

    /// <summary>Tells bound controls to ask <see cref="CanExecute"/> again.</summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
