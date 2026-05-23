using NAudio.Wave;

namespace StreamMusicPlayer.Audio;

public static class AudioMetadataReader
{
    public static TimeSpan TryReadDuration(string filePath)
    {
        try
        {
            using var reader = new AudioFileReader(filePath);
            return reader.TotalTime;
        }
        catch
        {
            return TimeSpan.Zero;
        }
    }
}
