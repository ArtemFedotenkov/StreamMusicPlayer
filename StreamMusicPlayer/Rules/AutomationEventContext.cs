using StreamMusicPlayer.Models;

namespace StreamMusicPlayer.Rules;

public sealed class AutomationEventContext
{
    public AutomationSourceType SourceType { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string SceneName { get; init; } = string.Empty;
    public string PlaylistId { get; init; } = string.Empty;
    public string TrackId { get; init; } = string.Empty;
    public string SourceName { get; init; } = string.Empty;
    public int? SceneItemId { get; init; }
    public string FilterName { get; init; } = string.Empty;
    public bool? FilterEnabled { get; init; }
}
