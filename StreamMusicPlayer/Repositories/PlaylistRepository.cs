using System.IO;
using Microsoft.Data.Sqlite;
using StreamMusicPlayer.Models;

namespace StreamMusicPlayer.Repositories;

public sealed class PlaylistRepository
{
    private readonly string connectionString;

    public PlaylistRepository(string databasePath)
    {
        connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();
    }

    public IReadOnlyList<Playlist> LoadAll()
    {
        var playlists = new List<Playlist>();

        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT Id, Name, SortOrder, PlayMode, DefaultVolume, DefaultCrossfadeSeconds,
                       RepeatEnabled, ShuffleEnabled, CompletionAction, CompletionPlaylistId
                FROM Playlists
                ORDER BY SortOrder, Name;
                """;

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                playlists.Add(new Playlist
                {
                    Id = reader.GetString(0),
                    Name = reader.GetString(1),
                    SortOrder = reader.GetInt32(2),
                    PlayMode = Enum.TryParse<PlayMode>(reader.GetString(3), out var playMode) ? playMode : PlayMode.Sequential,
                    DefaultVolume = reader.GetInt32(4),
                    DefaultCrossfadeSeconds = reader.GetDouble(5),
                    RepeatEnabled = reader.GetInt32(6) == 1,
                    ShuffleEnabled = reader.GetInt32(7) == 1,
                    CompletionAction = Enum.TryParse<PlaylistCompletionAction>(reader.GetString(8), out var completionAction)
                        ? completionAction
                        : PlaylistCompletionAction.Stop,
                    CompletionPlaylistId = reader.IsDBNull(9) ? string.Empty : reader.GetString(9)
                });
            }
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT Id, PlaylistId, FilePath, DisplayTitle, Artist, DurationSeconds, SortOrder, Enabled, LastKnownExists
                FROM Tracks
                ORDER BY PlaylistId, SortOrder, DisplayTitle;
                """;

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var playlist = playlists.FirstOrDefault(item => item.Id == reader.GetString(1));
                if (playlist is null)
                {
                    continue;
                }

                playlist.Tracks.Add(new Track
                {
                    Id = reader.GetString(0),
                    PlaylistId = reader.GetString(1),
                    FilePath = reader.GetString(2),
                    DisplayTitle = reader.GetString(3),
                    Artist = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    Duration = TimeSpan.FromSeconds(reader.IsDBNull(5) ? 0 : reader.GetDouble(5)),
                    SortOrder = reader.GetInt32(6),
                    Enabled = reader.GetInt32(7) == 1,
                    LastKnownExists = reader.GetInt32(8) == 1
                });
            }
        }

        return playlists;
    }

    public void SaveAll(IEnumerable<Playlist> playlists)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var transaction = connection.BeginTransaction();
        ExecuteNonQuery(connection, transaction, "DELETE FROM Tracks;");
        ExecuteNonQuery(connection, transaction, "DELETE FROM Playlists;");

        var now = DateTimeOffset.UtcNow.ToString("O");
        foreach (var playlist in playlists)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    INSERT INTO Playlists (
                        Id, Name, SortOrder, PlayMode, DefaultVolume, DefaultCrossfadeSeconds,
                        RepeatEnabled, ShuffleEnabled, CompletionAction, CompletionPlaylistId, CreatedAt, UpdatedAt
                    )
                    VALUES (
                        $id, $name, $sortOrder, $playMode, $defaultVolume, $defaultCrossfadeSeconds,
                        $repeatEnabled, $shuffleEnabled, $completionAction, $completionPlaylistId, $createdAt, $updatedAt
                    );
                    """;
                command.Parameters.AddWithValue("$id", playlist.Id);
                command.Parameters.AddWithValue("$name", playlist.Name);
                command.Parameters.AddWithValue("$sortOrder", playlist.SortOrder);
                command.Parameters.AddWithValue("$playMode", playlist.PlayMode.ToString());
                command.Parameters.AddWithValue("$defaultVolume", playlist.DefaultVolume);
                command.Parameters.AddWithValue("$defaultCrossfadeSeconds", playlist.DefaultCrossfadeSeconds);
                command.Parameters.AddWithValue("$repeatEnabled", playlist.RepeatEnabled ? 1 : 0);
                command.Parameters.AddWithValue("$shuffleEnabled", playlist.ShuffleEnabled ? 1 : 0);
                command.Parameters.AddWithValue("$completionAction", playlist.CompletionAction.ToString());
                command.Parameters.AddWithValue("$completionPlaylistId", playlist.CompletionPlaylistId);
                command.Parameters.AddWithValue("$createdAt", now);
                command.Parameters.AddWithValue("$updatedAt", now);
                command.ExecuteNonQuery();
            }

            foreach (var track in playlist.Tracks)
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = """
                    INSERT INTO Tracks (
                        Id, PlaylistId, FilePath, DisplayTitle, Artist, DurationSeconds,
                        SortOrder, Enabled, LastKnownExists, CreatedAt, UpdatedAt
                    )
                    VALUES (
                        $id, $playlistId, $filePath, $displayTitle, $artist, $durationSeconds,
                        $sortOrder, $enabled, $lastKnownExists, $createdAt, $updatedAt
                    );
                    """;
                command.Parameters.AddWithValue("$id", track.Id);
                command.Parameters.AddWithValue("$playlistId", playlist.Id);
                command.Parameters.AddWithValue("$filePath", track.FilePath);
                command.Parameters.AddWithValue("$displayTitle", track.DisplayTitle);
                command.Parameters.AddWithValue("$artist", track.Artist);
                command.Parameters.AddWithValue("$durationSeconds", track.Duration.TotalSeconds);
                command.Parameters.AddWithValue("$sortOrder", track.SortOrder);
                command.Parameters.AddWithValue("$enabled", track.Enabled ? 1 : 0);
                command.Parameters.AddWithValue("$lastKnownExists", File.Exists(track.FilePath) ? 1 : 0);
                command.Parameters.AddWithValue("$createdAt", now);
                command.Parameters.AddWithValue("$updatedAt", now);
                command.ExecuteNonQuery();
            }
        }

        transaction.Commit();
    }

    private static void ExecuteNonQuery(SqliteConnection connection, SqliteTransaction transaction, string commandText)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        command.ExecuteNonQuery();
    }
}
