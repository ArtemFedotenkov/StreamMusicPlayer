using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace StreamMusicPlayer.Models;

public sealed class AutomationRule : INotifyPropertyChanged
{
    private string name = "New Rule";
    private bool enabled = true;
    private int priority = 100;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public string Name
    {
        get => name;
        set => SetProperty(ref name, value);
    }

    public bool Enabled
    {
        get => enabled;
        set => SetProperty(ref enabled, value);
    }

    public int Priority
    {
        get => priority;
        set => SetProperty(ref priority, value);
    }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ObservableCollection<AutomationEvent> Events { get; } = [];
    public ObservableCollection<AutomationAction> Actions { get; } = [];

    public override string ToString()
    {
        return Name;
    }

    private void SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return;
        }

        storage = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
