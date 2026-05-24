namespace StreamMusicPlayer.Models;

using System.ComponentModel;
using System.Runtime.CompilerServices;

public sealed class AutomationEvent : INotifyPropertyChanged
{
    private AutomationSourceType sourceType = AutomationSourceType.Obs;
    private string eventType = AutomationEventTypes.Obs.SceneChanged;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string RuleId { get; set; } = string.Empty;

    public AutomationSourceType SourceType
    {
        get => sourceType;
        set => SetProperty(ref sourceType, value);
    }

    public string EventType
    {
        get => eventType;
        set => SetProperty(ref eventType, value);
    }

    public string SettingsJson { get; set; } = "{}";
    public int SortOrder { get; set; }

    public override string ToString()
    {
        return $"{SourceType}: {EventType}";
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
