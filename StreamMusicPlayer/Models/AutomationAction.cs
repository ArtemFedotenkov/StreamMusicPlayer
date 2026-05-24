namespace StreamMusicPlayer.Models;

using System.ComponentModel;
using System.Runtime.CompilerServices;

public sealed class AutomationAction : INotifyPropertyChanged
{
    private AutomationTargetType targetType = AutomationTargetType.Player;
    private string actionType = AutomationActionTypes.Player.PlayPlaylist;
    private bool enabled = true;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string RuleId { get; set; } = string.Empty;

    public AutomationTargetType TargetType
    {
        get => targetType;
        set => SetProperty(ref targetType, value);
    }

    public string ActionType
    {
        get => actionType;
        set => SetProperty(ref actionType, value);
    }

    public string SettingsJson { get; set; } = "{}";

    public bool Enabled
    {
        get => enabled;
        set => SetProperty(ref enabled, value);
    }

    public int SortOrder { get; set; }

    public override string ToString()
    {
        return $"{TargetType}: {ActionType}";
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
