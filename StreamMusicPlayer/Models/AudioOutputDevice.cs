namespace StreamMusicPlayer.Models;

public sealed class AudioOutputDevice
{
    public const string DefaultId = "default";

    public string Id { get; init; } = DefaultId;

    public string DisplayName { get; init; } = "System default";

    public int DeviceNumber { get; init; } = -1;

    public bool IsDefault { get; init; }

    public override string ToString()
    {
        return DisplayName;
    }
}
