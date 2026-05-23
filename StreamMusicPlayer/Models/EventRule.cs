namespace StreamMusicPlayer.Models;

public sealed class EventRule
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New Rule";
    public bool Enabled { get; set; } = true;
    public string TriggerType { get; set; } = "SceneChanged";
    public string TriggerSceneName { get; set; } = string.Empty;
    public string ActionType { get; set; } = "PlayPlaylist";
    public string ActionJson { get; set; } = "{}";
    public int Priority { get; set; } = 100;
    public double DelaySeconds { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
