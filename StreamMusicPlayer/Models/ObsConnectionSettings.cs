namespace StreamMusicPlayer.Models;

public sealed class ObsConnectionSettings
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 4455;
    public string Password { get; set; } = string.Empty;
    public bool AutoConnectOnStartup { get; set; }
    public bool ReconnectAutomatically { get; set; } = true;
    public int ReconnectIntervalSeconds { get; set; } = 10;
}
