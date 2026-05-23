using System.IO;
using Microsoft.Data.Sqlite;
using StreamMusicPlayer.Data;

namespace StreamMusicPlayer.Services;

public sealed class ApplicationDataResetService
{
    public void Reset()
    {
        if (File.Exists(AppDataPaths.DatabasePath))
        {
            ClearDatabase();
        }

        if (Directory.Exists(AppDataPaths.LogsDirectory))
        {
            foreach (var filePath in Directory.EnumerateFiles(AppDataPaths.LogsDirectory))
            {
                File.Delete(filePath);
            }
        }
    }

    private static void ClearDatabase()
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = AppDataPaths.DatabasePath
        }.ToString();

        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            DELETE FROM EventRules;
            DELETE FROM Tracks;
            DELETE FROM Playlists;
            DELETE FROM AppSettings;
            """;
        command.ExecuteNonQuery();
        transaction.Commit();
    }
}
