namespace StreamMusicPlayer.Rules;

public sealed class EventRuleActionParameters
{
    public string PlaylistName { get; set; } = string.Empty;
    public double CrossfadeSeconds { get; set; }
    public int Volume { get; set; } = 100;
}
