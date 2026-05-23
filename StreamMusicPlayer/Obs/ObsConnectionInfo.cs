namespace StreamMusicPlayer.Obs;

public sealed class ObsConnectionInfo
{
    public bool Connected { get; init; }
    public string StatusMessage { get; init; } = string.Empty;
    public string ObsVersion { get; init; } = string.Empty;
    public string WebSocketVersion { get; init; } = string.Empty;
    public IReadOnlyList<string> Scenes { get; init; } = [];
    public string CurrentScene { get; init; } = string.Empty;
}
