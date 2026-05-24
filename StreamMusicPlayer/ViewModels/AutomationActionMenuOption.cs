using System.Collections.ObjectModel;
using StreamMusicPlayer.Models;
using StreamMusicPlayer.Rules;

namespace StreamMusicPlayer.ViewModels;

public sealed class AutomationActionMenuOption
{
    public string DisplayName { get; init; } = string.Empty;
    public AutomationTargetType TargetType { get; init; }
    public string ActionType { get; init; } = string.Empty;
    public AutomationActionSettings Settings { get; init; } = new();
    public ObservableCollection<AutomationActionMenuOption> Children { get; } = [];
    public bool CanAdd => !string.IsNullOrWhiteSpace(ActionType);
}
