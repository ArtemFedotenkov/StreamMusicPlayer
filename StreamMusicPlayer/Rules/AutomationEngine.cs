using System.Text.Json;
using StreamMusicPlayer.Models;
using StreamMusicPlayer.Repositories;

namespace StreamMusicPlayer.Rules;

public sealed class AutomationEngine
{
    private readonly AutomationRuleRepository automationRuleRepository;
    private readonly JsonSerializerOptions jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public AutomationEngine(AutomationRuleRepository automationRuleRepository)
    {
        this.automationRuleRepository = automationRuleRepository;
    }

    public async Task<int> ExecuteAsync(AutomationEventContext context, Func<AutomationRule, AutomationAction, Task> executeActionAsync)
    {
        var matchingRules = automationRuleRepository
            .LoadAll()
            .Where(rule => rule.Enabled && rule.Events.Any(automationEvent => Matches(automationEvent, context)))
            .OrderByDescending(rule => rule.Priority)
            .ThenBy(rule => rule.CreatedAt)
            .ToList();

        var executedActions = 0;
        foreach (var rule in matchingRules)
        {
            var actions = rule.Actions
                .Where(action => action.Enabled)
                .OrderBy(action => action.SortOrder)
                .ToList();
            var actionTasks = actions.Select(action => executeActionAsync(rule, action)).ToList();
            await Task.WhenAll(actionTasks);
            executedActions += actions.Count;
        }

        return executedActions;
    }

    public AutomationEventSettings ReadEventSettings(AutomationEvent automationEvent)
    {
        return ReadJson<AutomationEventSettings>(automationEvent.SettingsJson);
    }

    public AutomationActionSettings ReadActionSettings(AutomationAction action)
    {
        return ReadJson<AutomationActionSettings>(action.SettingsJson);
    }

    private bool Matches(AutomationEvent automationEvent, AutomationEventContext context)
    {
        if (automationEvent.SourceType != context.SourceType
            || !string.Equals(automationEvent.EventType, context.EventType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var settings = ReadEventSettings(automationEvent);
        return automationEvent.SourceType switch
        {
            AutomationSourceType.Obs => MatchesObsEvent(automationEvent.EventType, settings, context),
            AutomationSourceType.Player => MatchesPlayerEvent(automationEvent.EventType, settings, context),
            _ => false
        };
    }

    private static bool MatchesObsEvent(string eventType, AutomationEventSettings settings, AutomationEventContext context)
    {
        if (!string.Equals(eventType, AutomationEventTypes.Obs.SceneChanged, StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(eventType, AutomationEventTypes.Obs.SourceFilterEnabled, StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventType, AutomationEventTypes.Obs.SourceFilterDisabled, StringComparison.OrdinalIgnoreCase))
            {
                var sourceMatches = string.IsNullOrWhiteSpace(settings.SourceName)
                    || string.Equals(settings.SourceName, context.SourceName, StringComparison.OrdinalIgnoreCase);
                var filterMatches = string.IsNullOrWhiteSpace(settings.FilterName)
                    || string.Equals(settings.FilterName, context.FilterName, StringComparison.OrdinalIgnoreCase);
                var stateMatches = settings.FilterEnabled is null || settings.FilterEnabled == context.FilterEnabled;
                return sourceMatches && filterMatches && stateMatches;
            }

            if (string.Equals(eventType, AutomationEventTypes.Obs.SceneItemEnabled, StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventType, AutomationEventTypes.Obs.SceneItemDisabled, StringComparison.OrdinalIgnoreCase))
            {
                var sceneMatches = string.IsNullOrWhiteSpace(settings.SceneName)
                    || string.Equals(settings.SceneName, context.SceneName, StringComparison.OrdinalIgnoreCase);
                var sourceMatches = string.IsNullOrWhiteSpace(settings.SourceName)
                    || string.Equals(settings.SourceName, context.SourceName, StringComparison.OrdinalIgnoreCase);
                var sceneItemMatches = settings.SceneItemId <= 0 || settings.SceneItemId == context.SceneItemId;
                var stateMatches = settings.FilterEnabled is null || settings.FilterEnabled == context.FilterEnabled;
                return sceneMatches && sourceMatches && sceneItemMatches && stateMatches;
            }

            return true;
        }

        return string.IsNullOrWhiteSpace(settings.SceneName)
            || string.Equals(settings.SceneName, context.SceneName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesPlayerEvent(string eventType, AutomationEventSettings settings, AutomationEventContext context)
    {
        if (string.Equals(eventType, AutomationEventTypes.Player.PlaylistFinished, StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(settings.PlaylistId)
                || string.Equals(settings.PlaylistId, context.PlaylistId, StringComparison.OrdinalIgnoreCase);
        }

        if (string.Equals(eventType, AutomationEventTypes.Player.TrackFinished, StringComparison.OrdinalIgnoreCase))
        {
            var playlistMatches = string.IsNullOrWhiteSpace(settings.PlaylistId)
                || string.Equals(settings.PlaylistId, context.PlaylistId, StringComparison.OrdinalIgnoreCase);
            var trackMatches = string.IsNullOrWhiteSpace(settings.TrackId)
                || string.Equals(settings.TrackId, context.TrackId, StringComparison.OrdinalIgnoreCase);
            return playlistMatches && trackMatches;
        }

        return true;
    }

    private TSettings ReadJson<TSettings>(string settingsJson)
        where TSettings : new()
    {
        if (string.IsNullOrWhiteSpace(settingsJson))
        {
            return new TSettings();
        }

        try
        {
            return JsonSerializer.Deserialize<TSettings>(settingsJson, jsonOptions) ?? new TSettings();
        }
        catch
        {
            return new TSettings();
        }
    }
}
