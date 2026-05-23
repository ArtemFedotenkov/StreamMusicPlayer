using System.Text.Json;
using StreamMusicPlayer.Models;

namespace StreamMusicPlayer.Rules;

public sealed class EventRulesEngine
{
    public EventRule? FindSceneRule(IEnumerable<EventRule> rules, string sceneName)
    {
        return FindRule(rules, "SceneChanged", sceneName);
    }

    public EventRule? FindRule(IEnumerable<EventRule> rules, string triggerType, string condition = "")
    {
        return rules
            .Where(rule => rule.Enabled)
            .Where(rule => string.Equals(rule.TriggerType, triggerType, StringComparison.OrdinalIgnoreCase))
            .Where(rule => string.IsNullOrWhiteSpace(rule.TriggerSceneName)
                || string.IsNullOrWhiteSpace(condition)
                || string.Equals(rule.TriggerSceneName, condition, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(rule => rule.Priority)
            .ThenBy(rule => rule.CreatedAt)
            .FirstOrDefault();
    }

    public EventRuleActionParameters ReadActionParameters(EventRule rule)
    {
        if (string.IsNullOrWhiteSpace(rule.ActionJson))
        {
            return new EventRuleActionParameters();
        }

        try
        {
            return JsonSerializer.Deserialize<EventRuleActionParameters>(
                rule.ActionJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new EventRuleActionParameters();
        }
        catch
        {
            return new EventRuleActionParameters();
        }
    }
}
