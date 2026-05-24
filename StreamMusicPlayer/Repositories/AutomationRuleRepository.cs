using Microsoft.Data.Sqlite;
using StreamMusicPlayer.Models;

namespace StreamMusicPlayer.Repositories;

public sealed class AutomationRuleRepository
{
    private readonly string connectionString;

    public AutomationRuleRepository(string databasePath)
    {
        connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();
    }

    public IReadOnlyList<AutomationRule> LoadAll()
    {
        var rules = LoadRules();
        LoadEvents(rules);
        LoadActions(rules);
        return rules;
    }

    public void SaveAll(IEnumerable<AutomationRule> rules)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var transaction = connection.BeginTransaction();
        ExecuteNonQuery(connection, transaction, "DELETE FROM AutomationActions;");
        ExecuteNonQuery(connection, transaction, "DELETE FROM AutomationEvents;");
        ExecuteNonQuery(connection, transaction, "DELETE FROM AutomationRules;");

        foreach (var rule in rules)
        {
            rule.UpdatedAt = DateTimeOffset.UtcNow;
            SaveRule(connection, transaction, rule);

            var eventOrder = 0;
            foreach (var automationEvent in rule.Events)
            {
                automationEvent.RuleId = rule.Id;
                automationEvent.SortOrder = eventOrder++;
                SaveEvent(connection, transaction, automationEvent);
            }

            var actionOrder = 0;
            foreach (var action in rule.Actions)
            {
                action.RuleId = rule.Id;
                action.SortOrder = actionOrder++;
                SaveAction(connection, transaction, action);
            }
        }

        transaction.Commit();
    }

    private List<AutomationRule> LoadRules()
    {
        var rules = new List<AutomationRule>();

        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Name, Enabled, Priority, CreatedAt, UpdatedAt
            FROM AutomationRules
            ORDER BY Priority DESC, CreatedAt;
            """;

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rules.Add(new AutomationRule
            {
                Id = ReadString(reader, 0, Guid.NewGuid().ToString("N")),
                Name = ReadString(reader, 1, "New Rule"),
                Enabled = ReadBoolean(reader, 2, true),
                Priority = ReadInt32(reader, 3, 100),
                CreatedAt = ReadDateTimeOffset(reader, 4, DateTimeOffset.UtcNow),
                UpdatedAt = ReadDateTimeOffset(reader, 5, DateTimeOffset.UtcNow)
            });
        }

        return rules;
    }

    private void LoadEvents(IReadOnlyList<AutomationRule> rules)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, RuleId, SourceType, EventType, SettingsJson, SortOrder
            FROM AutomationEvents
            ORDER BY RuleId, SortOrder;
            """;

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var ruleId = ReadString(reader, 1, string.Empty);
            var rule = rules.FirstOrDefault(item => item.Id == ruleId);
            if (rule is null)
            {
                continue;
            }

            rule.Events.Add(new AutomationEvent
            {
                Id = ReadString(reader, 0, Guid.NewGuid().ToString("N")),
                RuleId = ruleId,
                SourceType = ReadEnum(reader, 2, AutomationSourceType.Obs),
                EventType = ReadString(reader, 3, AutomationEventTypes.Obs.SceneChanged),
                SettingsJson = NormalizeJson(ReadString(reader, 4, "{}")),
                SortOrder = ReadInt32(reader, 5, 0)
            });
        }
    }

    private void LoadActions(IReadOnlyList<AutomationRule> rules)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, RuleId, TargetType, ActionType, SettingsJson, Enabled, SortOrder
            FROM AutomationActions
            ORDER BY RuleId, SortOrder;
            """;

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var ruleId = ReadString(reader, 1, string.Empty);
            var rule = rules.FirstOrDefault(item => item.Id == ruleId);
            if (rule is null)
            {
                continue;
            }

            rule.Actions.Add(new AutomationAction
            {
                Id = ReadString(reader, 0, Guid.NewGuid().ToString("N")),
                RuleId = ruleId,
                TargetType = ReadEnum(reader, 2, AutomationTargetType.Player),
                ActionType = ReadString(reader, 3, AutomationActionTypes.Player.PlayPlaylist),
                SettingsJson = NormalizeJson(ReadString(reader, 4, "{}")),
                Enabled = ReadBoolean(reader, 5, true),
                SortOrder = ReadInt32(reader, 6, 0)
            });
        }
    }

    private static void SaveRule(SqliteConnection connection, SqliteTransaction transaction, AutomationRule rule)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO AutomationRules (Id, Name, Enabled, Priority, CreatedAt, UpdatedAt)
            VALUES ($id, $name, $enabled, $priority, $createdAt, $updatedAt);
            """;
        command.Parameters.AddWithValue("$id", rule.Id);
        command.Parameters.AddWithValue("$name", rule.Name);
        command.Parameters.AddWithValue("$enabled", rule.Enabled ? 1 : 0);
        command.Parameters.AddWithValue("$priority", rule.Priority);
        command.Parameters.AddWithValue("$createdAt", rule.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$updatedAt", rule.UpdatedAt.ToString("O"));
        command.ExecuteNonQuery();
    }

    private static void SaveEvent(SqliteConnection connection, SqliteTransaction transaction, AutomationEvent automationEvent)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO AutomationEvents (Id, RuleId, SourceType, EventType, SettingsJson, SortOrder)
            VALUES ($id, $ruleId, $sourceType, $eventType, $settingsJson, $sortOrder);
            """;
        command.Parameters.AddWithValue("$id", automationEvent.Id);
        command.Parameters.AddWithValue("$ruleId", automationEvent.RuleId);
        command.Parameters.AddWithValue("$sourceType", automationEvent.SourceType.ToString());
        command.Parameters.AddWithValue("$eventType", automationEvent.EventType);
        command.Parameters.AddWithValue("$settingsJson", NormalizeJson(automationEvent.SettingsJson));
        command.Parameters.AddWithValue("$sortOrder", automationEvent.SortOrder);
        command.ExecuteNonQuery();
    }

    private static void SaveAction(SqliteConnection connection, SqliteTransaction transaction, AutomationAction action)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO AutomationActions (Id, RuleId, TargetType, ActionType, SettingsJson, Enabled, SortOrder)
            VALUES ($id, $ruleId, $targetType, $actionType, $settingsJson, $enabled, $sortOrder);
            """;
        command.Parameters.AddWithValue("$id", action.Id);
        command.Parameters.AddWithValue("$ruleId", action.RuleId);
        command.Parameters.AddWithValue("$targetType", action.TargetType.ToString());
        command.Parameters.AddWithValue("$actionType", action.ActionType);
        command.Parameters.AddWithValue("$settingsJson", NormalizeJson(action.SettingsJson));
        command.Parameters.AddWithValue("$enabled", action.Enabled ? 1 : 0);
        command.Parameters.AddWithValue("$sortOrder", action.SortOrder);
        command.ExecuteNonQuery();
    }

    private static void ExecuteNonQuery(SqliteConnection connection, SqliteTransaction transaction, string commandText)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        command.ExecuteNonQuery();
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

    private static TEnum ReadEnum<TEnum>(SqliteDataReader reader, int ordinal, TEnum fallback)
        where TEnum : struct
    {
        var value = ReadString(reader, ordinal, string.Empty);
        return Enum.TryParse<TEnum>(value, true, out var parsed) ? parsed : fallback;
    }

    private static DateTimeOffset ReadDateTimeOffset(SqliteDataReader reader, int ordinal, DateTimeOffset fallback)
    {
        var value = ReadString(reader, ordinal, string.Empty);
        return DateTimeOffset.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static string NormalizeJson(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "{}" : value;
    }
}
