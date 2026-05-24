namespace StreamMusicPlayer.Rules;

public sealed class AutomationEventSettings
{
    public string SceneName { get; set; } = string.Empty;
    public string PlaylistId { get; set; } = string.Empty;
    public string PlaylistName { get; set; } = string.Empty;
    public string TrackId { get; set; } = string.Empty;
    public string TrackName { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public int SceneItemId { get; set; }
    public string FilterName { get; set; } = string.Empty;
    public bool? FilterEnabled { get; set; }
}

public sealed class AutomationActionSettings
{
    public string SceneName { get; set; } = string.Empty;
    public string PlaylistId { get; set; } = string.Empty;
    public string TrackId { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public int SceneItemId { get; set; }
    public string FilterName { get; set; } = string.Empty;
    public bool FilterEnabled { get; set; } = true;
    public int Volume { get; set; } = 100;
    public double CrossfadeSeconds { get; set; } = 2;
}
