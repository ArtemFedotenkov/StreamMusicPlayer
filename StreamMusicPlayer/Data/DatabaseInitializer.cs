using Microsoft.Data.Sqlite;

namespace StreamMusicPlayer.Data;

public sealed class DatabaseInitializer
{
    private readonly string connectionString;

    public DatabaseInitializer(string databasePath)
    {
        connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();
    }

    public void Initialize()
    {
        AppDataPaths.EnsureCreated();

        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Playlists (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                SortOrder INTEGER NOT NULL,
                PlayMode TEXT NOT NULL,
                DefaultVolume INTEGER NOT NULL,
                DefaultCrossfadeSeconds REAL NOT NULL,
                RepeatEnabled INTEGER NOT NULL,
                ShuffleEnabled INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Tracks (
                Id TEXT PRIMARY KEY,
                PlaylistId TEXT NOT NULL,
                FilePath TEXT NOT NULL,
                DisplayTitle TEXT NOT NULL,
                Artist TEXT,
                DurationSeconds REAL,
                SortOrder INTEGER NOT NULL,
                Enabled INTEGER NOT NULL,
                LastKnownExists INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                FOREIGN KEY (PlaylistId) REFERENCES Playlists(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS EventRules (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Enabled INTEGER NOT NULL,
                TriggerType TEXT NOT NULL,
                TriggerSceneName TEXT,
                ActionType TEXT NOT NULL,
                ActionJson TEXT NOT NULL,
                Priority INTEGER NOT NULL,
                DelaySeconds REAL NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS AppSettings (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();

        AddColumnIfMissing(connection, "Playlists", "CompletionAction", "TEXT NOT NULL DEFAULT 'Stop'");
        AddColumnIfMissing(connection, "Playlists", "CompletionPlaylistId", "TEXT NOT NULL DEFAULT ''");
        UpdateLegacyDefaults(connection);
    }

    private static void AddColumnIfMissing(SqliteConnection connection, string tableName, string columnName, string columnDefinition)
    {
        using var checkCommand = connection.CreateCommand();
        checkCommand.CommandText = $"PRAGMA table_info({tableName});";

        using var reader = checkCommand.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        using var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};";
        alterCommand.ExecuteNonQuery();
    }

    private static void UpdateLegacyDefaults(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Playlists
            SET DefaultVolume = 100
            WHERE DefaultVolume = 80;

            UPDATE Playlists
            SET DefaultCrossfadeSeconds = 2
            WHERE DefaultCrossfadeSeconds = 4;
            """;
        command.ExecuteNonQuery();
    }
}
