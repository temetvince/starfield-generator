using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Starfield.App.Mvvm;

/// <summary>
/// A base for objects a view binds to: raises <see cref="PropertyChanged"/> when a property changes.
/// </summary>
/// <remarks>
/// Deliberately tiny, so the application layer carries no UI-framework dependency and can be tested as
/// plain objects.
/// </remarks>
public abstract class ObservableObject : INotifyPropertyChanged
{
    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Stores a new value and raises <see cref="PropertyChanged"/> if it differs from the old one.</summary>
    /// <typeparam name="T">The property type.</typeparam>
    /// <param name="field">The backing field.</param>
    /// <param name="value">The new value.</param>
    /// <param name="propertyName">The property, supplied by the compiler.</param>
    /// <returns><see langword="true"/> when the value changed.</returns>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>Raises <see cref="PropertyChanged"/>.</summary>
    /// <param name="propertyName">The property that changed, supplied by the compiler when omitted.</param>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
