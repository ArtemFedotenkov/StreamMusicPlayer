using Microsoft.Data.Sqlite;
using StreamMusicPlayer.Models;

namespace StreamMusicPlayer.Repositories;

public sealed class EventRuleRepository
{
    private readonly string connectionString;

    public EventRuleRepository(string databasePath)
    {
        connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();
    }

    public IReadOnlyList<EventRule> LoadAll()
    {
        var rules = new List<EventRule>();

        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Name, Enabled, TriggerType, TriggerSceneName, ActionType, ActionJson,
                   Priority, DelaySeconds, CreatedAt, UpdatedAt
            FROM EventRules
            ORDER BY Priority DESC, CreatedAt;
            """;

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            try
            {
                rules.Add(new EventRule
                {
                    Id = ReadString(reader, 0, Guid.NewGuid().ToString("N")),
                    Name = ReadString(reader, 1, "New Rule"),
                    Enabled = ReadBoolean(reader, 2, true),
                    TriggerType = ReadString(reader, 3, "SceneChanged"),
                    TriggerSceneName = ReadString(reader, 4, string.Empty),
                    ActionType = ReadString(reader, 5, "PlayPlaylist"),
                    ActionJson = NormalizeActionJson(ReadString(reader, 6, "{}")),
                    Priority = ReadInt32(reader, 7, 100),
                    DelaySeconds = ReadDouble(reader, 8, 0),
                    CreatedAt = ReadDateTimeOffset(reader, 9, DateTimeOffset.UtcNow),
                    UpdatedAt = ReadDateTimeOffset(reader, 10, DateTimeOffset.UtcNow)
                });
            }
            catch
            {
                // Skip corrupted legacy rows instead of crashing the editor.
            }
        }

        return rules;
    }

    public void SaveAll(IEnumerable<EventRule> rules)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var transaction = connection.BeginTransaction();
        using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = "DELETE FROM EventRules;";
            deleteCommand.ExecuteNonQuery();
        }

        foreach (var rule in rules)
        {
            rule.UpdatedAt = DateTimeOffset.UtcNow;

            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO EventRules (
                    Id, Name, Enabled, TriggerType, TriggerSceneName, ActionType, ActionJson,
                    Priority, DelaySeconds, CreatedAt, UpdatedAt
                )
                VALUES (
                    $id, $name, $enabled, $triggerType, $triggerSceneName, $actionType, $actionJson,
                    $priority, $delaySeconds, $createdAt, $updatedAt
                );
                """;
            command.Parameters.AddWithValue("$id", rule.Id);
            command.Parameters.AddWithValue("$name", rule.Name);
            command.Parameters.AddWithValue("$enabled", rule.Enabled ? 1 : 0);
            command.Parameters.AddWithValue("$triggerType", rule.TriggerType);
            command.Parameters.AddWithValue("$triggerSceneName", rule.TriggerSceneName);
            command.Parameters.AddWithValue("$actionType", rule.ActionType);
            command.Parameters.AddWithValue("$actionJson", rule.ActionJson);
            command.Parameters.AddWithValue("$priority", rule.Priority);
            command.Parameters.AddWithValue("$delaySeconds", rule.DelaySeconds);
            command.Parameters.AddWithValue("$createdAt", rule.CreatedAt.ToString("O"));
            command.Parameters.AddWithValue("$updatedAt", rule.UpdatedAt.ToString("O"));
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    private static string ReadString(SqliteDataReader reader, int ordinal, string fallback)
    {
        if (reader.IsDBNull(ordinal))
        {
            return fallback;
        }

        var value = reader.GetValue(ordinal)?.ToString();
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static bool ReadBoolean(SqliteDataReader reader, int ordinal, bool fallback)
    {
        if (reader.IsDBNull(ordinal))
        {
            return fallback;
        }

        var value = reader.GetValue(ordinal);
        return value switch
        {
            bool boolValue => boolValue,
            long longValue => longValue != 0,
            int intValue => intValue != 0,
            string stringValue when bool.TryParse(stringValue, out var parsed) => parsed,
            string stringValue when int.TryParse(stringValue, out var parsed) => parsed != 0,
            _ => fallback
        };
    }

    private static int ReadInt32(SqliteDataReader reader, int ordinal, int fallback)
    {
        if (reader.IsDBNull(ordinal))
        {
            return fallback;
        }

        var value = reader.GetValue(ordinal);
        return value switch
        {
            int intValue => intValue,
            long longValue => (int)longValue,
            double doubleValue => (int)doubleValue,
            string stringValue when int.TryParse(stringValue, out var parsed) => parsed,
            _ => fallback
        };
    }

    private static double ReadDouble(SqliteDataReader reader, int ordinal, double fallback)
    {
        if (reader.IsDBNull(ordinal))
        {
            return fallback;
        }

        var value = reader.GetValue(ordinal);
        return value switch
        {
            double doubleValue => doubleValue,
            float floatValue => floatValue,
            int intValue => intValue,
            long longValue => longValue,
            string stringValue when double.TryParse(stringValue, out var parsed) => parsed,
            _ => fallback
        };
    }

    private static DateTimeOffset ReadDateTimeOffset(SqliteDataReader reader, int ordinal, DateTimeOffset fallback)
    {
        var value = ReadString(reader, ordinal, string.Empty);
        return DateTimeOffset.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static string NormalizeActionJson(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "{}" : value;
    }
}
