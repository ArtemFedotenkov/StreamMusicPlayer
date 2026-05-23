using System.IO;

namespace StreamMusicPlayer.Data;

public static class AppDataPaths
{
    public static string RootDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "StreamMusicPlayer");

    public static string DatabasePath => Path.Combine(RootDirectory, "app.db");

    public static string LogsDirectory => Path.Combine(RootDirectory, "logs");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }
}
