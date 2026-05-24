using System.Collections.ObjectModel;
using StreamMusicPlayer.Models;
using StreamMusicPlayer.Rules;

namespace StreamMusicPlayer.ViewModels;

public sealed class AutomationEventMenuOption
{
    public string DisplayName { get; init; } = string.Empty;
    public AutomationSourceType SourceType { get; init; }
    public string EventType { get; init; } = string.Empty;
    public AutomationEventSettings Settings { get; init; } = new();
    public ObservableCollection<AutomationEventMenuOption> Children { get; } = [];
    public bool CanAdd => !string.IsNullOrWhiteSpace(EventType);
}
