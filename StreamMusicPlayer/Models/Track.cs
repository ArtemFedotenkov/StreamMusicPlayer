namespace StreamMusicPlayer.Models;

public sealed class Track
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string PlaylistId { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string DisplayTitle { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public int SortOrder { get; set; }
    public bool Enabled { get; set; } = true;
    public bool LastKnownExists { get; set; } = true;
    public TrackStatus Status => LastKnownExists ? TrackStatus.Ok : TrackStatus.Missing;

    public string DurationText => Duration == TimeSpan.Zero
        ? "--:--"
        : Duration.ToString(Duration.TotalHours >= 1 ? @"h\:mm\:ss" : @"mm\:ss");
}
