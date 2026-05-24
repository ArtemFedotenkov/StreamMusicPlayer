namespace StreamMusicPlayer.Models;

public sealed class ApplicationSettings
{
    public AppTheme Theme { get; set; } = AppTheme.Light;

    public AppLanguage Language { get; set; } = AppLanguage.English;

    public string AudioOutputDeviceId { get; set; } = AudioOutputDevice.DefaultId;

    public int Volume { get; set; } = 100;

    public double CrossfadeSeconds { get; set; } = 2;
}
