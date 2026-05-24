using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows.Input;
using StreamMusicPlayer.Models;
using StreamMusicPlayer.Obs;
using StreamMusicPlayer.Repositories;
using StreamMusicPlayer.Rules;
using StreamMusicPlayer.Services;

namespace StreamMusicPlayer.ViewModels;

public sealed class EventRulesViewModel : ObservableObject
{
    private readonly AutomationRuleRepository automationRuleRepository;
    private readonly EventRuleRepository legacyEventRuleRepository;
    private readonly JsonSerializerOptions jsonOptions = new() { PropertyNameCaseInsensitive = true };
    private AutomationRule? selectedRule;
    private AutomationEvent? selectedEvent;
    private AutomationAction? selectedAction;
    private string statusMessage = LocalizationService.T("Ready");
    private bool isLoading;
    private AutomationTargetType selectedActionTarget = AutomationTargetType.Player;
    private string selectedActionType = AutomationActionTypes.Player.PlayPlaylist;
    private string selectedActionSceneName = string.Empty;
    private string selectedActionPlaylistId = string.Empty;
    private string selectedActionTrackId = string.Empty;
    private string selectedActionSourceName = string.Empty;
    private string selectedActionFilterName = string.Empty;
    private bool selectedActionFilterEnabled = true;
    private int selectedActionVolume = 100;
    private double selectedActionCrossfadeSeconds = 2;

    public EventRulesViewModel(
        AutomationRuleRepository automationRuleRepository,
        EventRuleRepository legacyEventRuleRepository,
        IEnumerable<Playlist> playlists,
        IEnumerable<string> scenes,
        IEnumerable<ObsSceneItemInfo> sceneItems,
        IEnumerable<ObsSourceFilterInfo> sourceFilters)
    {
        this.automationRuleRepository = automationRuleRepository;
        this.legacyEventRuleRepository = legacyEventRuleRepository;
        Playlists = new ObservableCollection<PlaylistOption>(
            playlists.Select(item => new PlaylistOption(
                item.Id,
                item.Name,
                item.Tracks.Select(track => new TrackOption(track.Id, track.DisplayTitle)))).OrderBy(item => item.DisplayName));
        Scenes = new ObservableCollection<string>(scenes.OrderBy(item => item));
        ActionTracks = [];
        ObsEventMenu = new ObservableCollection<AutomationEventMenuOption>(BuildObsEventMenu(scenes, sceneItems, sourceFilters));
        PlayerEventMenu = new ObservableCollection<AutomationEventMenuOption>(BuildPlayerEventMenu());
        ObsActionMenu = new ObservableCollection<AutomationActionMenuOption>(BuildObsActionMenu(scenes, sceneItems, sourceFilters));
        PlayerActionMenu = new ObservableCollection<AutomationActionMenuOption>(BuildPlayerActionMenu());
        var storedRules = automationRuleRepository.LoadAll();
        if (storedRules.Count == 0)
        {
            storedRules = MigrateLegacyRules();
        }

        Rules = new ObservableCollection<AutomationRule>(storedRules);

        AddRuleCommand = new RelayCommand(_ => AddRule());
        RemoveRuleCommand = new RelayCommand(_ => RemoveRule(), _ => SelectedRule is not null);
        AddEventOptionCommand = new RelayCommand(parameter => AddEvent(parameter as AutomationEventMenuOption), _ => SelectedRule is not null);
        RemoveEventCommand = new RelayCommand(_ => RemoveEvent(), _ => SelectedEvent is not null);
        AddActionOptionCommand = new RelayCommand(parameter => AddAction(parameter as AutomationActionMenuOption), _ => SelectedRule is not null);
        RemoveActionCommand = new RelayCommand(_ => RemoveAction(), _ => SelectedAction is not null);
        SaveRulesCommand = new RelayCommand(_ => SaveRules());

        SelectedRule = Rules.FirstOrDefault();
    }

    public ObservableCollection<AutomationRule> Rules { get; }
    public ObservableCollection<AutomationEvent> CurrentEvents => SelectedRule?.Events ?? [];
    public ObservableCollection<AutomationAction> CurrentActions => SelectedRule?.Actions ?? [];
    public ObservableCollection<string> Scenes { get; }
    public ObservableCollection<PlaylistOption> Playlists { get; }
    public ObservableCollection<TrackOption> ActionTracks { get; }
    public ObservableCollection<AutomationEventMenuOption> ObsEventMenu { get; }
    public ObservableCollection<AutomationEventMenuOption> PlayerEventMenu { get; }
    public ObservableCollection<AutomationActionMenuOption> ObsActionMenu { get; }
    public ObservableCollection<AutomationActionMenuOption> PlayerActionMenu { get; }

    public ICommand AddRuleCommand { get; }
    public ICommand RemoveRuleCommand { get; }
    public ICommand AddEventOptionCommand { get; }
    public ICommand RemoveEventCommand { get; }
    public ICommand AddActionOptionCommand { get; }
    public ICommand RemoveActionCommand { get; }
    public ICommand SaveRulesCommand { get; }

    public AutomationRule? SelectedRule
    {
        get => selectedRule;
        set
        {
            if (SetProperty(ref selectedRule, value))
            {
                SelectedEvent = selectedRule?.Events.FirstOrDefault();
                SelectedAction = selectedRule?.Actions.FirstOrDefault();
                OnPropertyChanged(nameof(CurrentEvents));
                OnPropertyChanged(nameof(CurrentActions));
                RaiseCommandStates();
            }
        }
    }

    public AutomationEvent? SelectedEvent
    {
        get => selectedEvent;
        set
        {
            if (SetProperty(ref selectedEvent, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public AutomationAction? SelectedAction
    {
        get => selectedAction;
        set
        {
            if (SetProperty(ref selectedAction, value))
            {
                LoadActionInspector();
                RaiseCommandStates();
            }
        }
    }

    public string StatusMessage
    {
        get => statusMessage;
        private set => SetProperty(ref statusMessage, value);
    }

    public AutomationTargetType SelectedActionTarget
    {
        get => selectedActionTarget;
        private set => SetProperty(ref selectedActionTarget, value);
    }

    public string SelectedActionType
    {
        get => selectedActionType;
        private set => SetProperty(ref selectedActionType, value);
    }

    public string SelectedActionSceneName
    {
        get => selectedActionSceneName;
        set
        {
            if (SetProperty(ref selectedActionSceneName, value))
            {
                UpdateSelectedActionSettings();
            }
        }
    }

    public string SelectedActionPlaylistId
    {
        get => selectedActionPlaylistId;
        set
        {
            if (SetProperty(ref selectedActionPlaylistId, value))
            {
                RefreshActionTracks();
                UpdateSelectedActionSettings();
            }
        }
    }

    public string SelectedActionTrackId
    {
        get => selectedActionTrackId;
        set
        {
            if (SetProperty(ref selectedActionTrackId, value))
            {
                UpdateSelectedActionSettings();
            }
        }
    }

    public string SelectedActionSourceName
    {
        get => selectedActionSourceName;
        set
        {
            if (SetProperty(ref selectedActionSourceName, value))
            {
                UpdateSelectedActionSettings();
            }
        }
    }

    public string SelectedActionFilterName
    {
        get => selectedActionFilterName;
        set
        {
            if (SetProperty(ref selectedActionFilterName, value))
            {
                UpdateSelectedActionSettings();
            }
        }
    }

    public bool SelectedActionFilterEnabled
    {
        get => selectedActionFilterEnabled;
        set
        {
            if (SetProperty(ref selectedActionFilterEnabled, value))
            {
                UpdateSelectedActionSettings();
            }
        }
    }

    public int SelectedActionVolume
    {
        get => selectedActionVolume;
        set
        {
            if (SetProperty(ref selectedActionVolume, Math.Clamp(value, 0, 100)))
            {
                UpdateSelectedActionSettings();
            }
        }
    }

    public double SelectedActionCrossfadeSeconds
    {
        get => selectedActionCrossfadeSeconds;
        set
        {
            if (SetProperty(ref selectedActionCrossfadeSeconds, Math.Round(Math.Clamp(value, 0, 10), 2)))
            {
                UpdateSelectedActionSettings();
            }
        }
    }

    public string SelectedActionDisplayName => SelectedAction is null
        ? string.Empty
        : AutomationDisplayNameService.GetActionDisplayName(SelectedAction.ActionType);
    public bool HasSelectedAction => SelectedAction is not null;
    public bool CanEditActionScene => SelectedActionTarget == AutomationTargetType.Obs
        && SelectedActionType == AutomationActionTypes.Obs.ChangeScene;
    public bool CanEditActionPlaylist => SelectedActionTarget == AutomationTargetType.Player
        && SelectedActionType is AutomationActionTypes.Player.PlayPlaylist or AutomationActionTypes.Player.PlayTrack;
    public bool CanEditActionTrack => SelectedActionTarget == AutomationTargetType.Player
        && SelectedActionType == AutomationActionTypes.Player.PlayTrack;
    public bool CanEditActionFilter => SelectedActionTarget == AutomationTargetType.Obs
        && SelectedActionType is AutomationActionTypes.Obs.SetSourceFilterEnabled
            or AutomationActionTypes.Obs.SetSceneItemEnabled;
    public bool CanEditActionVolume => SelectedActionTarget == AutomationTargetType.Player
        && SelectedActionType == AutomationActionTypes.Player.SetVolume;
    public bool CanEditActionCrossfade => SelectedActionTarget == AutomationTargetType.Player
        && SelectedActionType == AutomationActionTypes.Player.SetCrossfade;

    private void AddRule()
    {
        var rule = new AutomationRule
        {
            Name = LocalizationService.F("SceneRuleFormat", Rules.Count + 1),
            Priority = 100
        };

        Rules.Add(rule);
        SelectedRule = rule;
        StatusMessage = LocalizationService.T("RuleAdded");
    }

    private void RemoveRule()
    {
        if (SelectedRule is null)
        {
            return;
        }

        var index = Rules.IndexOf(SelectedRule);
        Rules.Remove(SelectedRule);
        SelectedRule = Rules.Count == 0 ? null : Rules[Math.Clamp(index, 0, Rules.Count - 1)];
        StatusMessage = LocalizationService.T("RuleRemoved");
    }

    private void AddEvent(AutomationEventMenuOption? option)
    {
        if (SelectedRule is null || option is not { CanAdd: true })
        {
            return;
        }

        var automationEvent = new AutomationEvent
        {
            RuleId = SelectedRule.Id,
            SourceType = option.SourceType,
            EventType = option.EventType,
            SortOrder = SelectedRule.Events.Count,
            SettingsJson = JsonSerializer.Serialize(option.Settings)
        };
        SelectedRule.Events.Add(automationEvent);
        SelectedEvent = automationEvent;
        OnPropertyChanged(nameof(CurrentEvents));
    }

    private void RemoveEvent()
    {
        if (SelectedRule is null || SelectedEvent is null)
        {
            return;
        }

        var index = SelectedRule.Events.IndexOf(SelectedEvent);
        SelectedRule.Events.Remove(SelectedEvent);
        SelectedEvent = SelectedRule.Events.Count == 0 ? null : SelectedRule.Events[Math.Clamp(index, 0, SelectedRule.Events.Count - 1)];
        ReorderEvents();
    }

    private void AddAction(AutomationActionMenuOption? option)
    {
        if (SelectedRule is null || option is not { CanAdd: true })
        {
            return;
        }

        var action = new AutomationAction
        {
            RuleId = SelectedRule.Id,
            TargetType = option.TargetType,
            ActionType = option.ActionType,
            SortOrder = SelectedRule.Actions.Count,
            SettingsJson = JsonSerializer.Serialize(option.Settings)
        };

        SelectedRule.Actions.Add(action);
        SelectedAction = action;
        OnPropertyChanged(nameof(CurrentActions));
    }

    private void RemoveAction()
    {
        if (SelectedRule is null || SelectedAction is null)
        {
            return;
        }

        var index = SelectedRule.Actions.IndexOf(SelectedAction);
        SelectedRule.Actions.Remove(SelectedAction);
        SelectedAction = SelectedRule.Actions.Count == 0 ? null : SelectedRule.Actions[Math.Clamp(index, 0, SelectedRule.Actions.Count - 1)];
        ReorderActions();
    }

    public void MoveAction(AutomationAction action, int targetIndex)
    {
        if (SelectedRule is null || !SelectedRule.Actions.Contains(action))
        {
            return;
        }

        var oldIndex = SelectedRule.Actions.IndexOf(action);
        var newIndex = Math.Clamp(targetIndex, 0, SelectedRule.Actions.Count - 1);
        if (oldIndex == newIndex)
        {
            return;
        }

        SelectedRule.Actions.Move(oldIndex, newIndex);
        SelectedAction = action;
        ReorderActions();
        OnPropertyChanged(nameof(CurrentActions));
    }

    private void SaveRules()
    {
        UpdateSelectedActionSettings();
        automationRuleRepository.SaveAll(Rules);
        StatusMessage = LocalizationService.T("RulesSaved");
    }

    private IReadOnlyList<AutomationRule> MigrateLegacyRules()
    {
        var legacyRules = legacyEventRuleRepository.LoadAll();
        if (legacyRules.Count == 0)
        {
            return [];
        }

        var migratedRules = legacyRules.Select(CreateAutomationRuleFromLegacyRule).ToList();
        automationRuleRepository.SaveAll(migratedRules);
        return migratedRules;
    }

    private AutomationRule CreateAutomationRuleFromLegacyRule(EventRule legacyRule)
    {
        var rule = new AutomationRule
        {
            Name = legacyRule.Name,
            Enabled = legacyRule.Enabled,
            Priority = legacyRule.Priority,
            CreatedAt = legacyRule.CreatedAt,
            UpdatedAt = legacyRule.UpdatedAt
        };

        rule.Events.Add(new AutomationEvent
        {
            RuleId = rule.Id,
            SourceType = AutomationSourceType.Obs,
            EventType = legacyRule.TriggerType,
            SettingsJson = JsonSerializer.Serialize(new AutomationEventSettings
            {
                SceneName = legacyRule.TriggerSceneName
            })
        });

        var parameters = ReadLegacyActionParameters(legacyRule.ActionJson);
        rule.Actions.Add(new AutomationAction
        {
            RuleId = rule.Id,
            TargetType = AutomationTargetType.Player,
            ActionType = MapLegacyActionType(legacyRule.ActionType),
            SettingsJson = JsonSerializer.Serialize(new AutomationActionSettings
            {
                PlaylistId = ResolvePlaylistId(parameters.PlaylistName),
                Volume = parameters.Volume,
                CrossfadeSeconds = parameters.CrossfadeSeconds <= 0 ? 2 : parameters.CrossfadeSeconds
            })
        });

        return rule;
    }

    private void LoadActionInspector()
    {
        isLoading = true;
        selectedActionTarget = SelectedAction?.TargetType ?? AutomationTargetType.Player;
        selectedActionType = SelectedAction?.ActionType ?? AutomationActionTypes.Player.PlayPlaylist;

        var settings = ReadActionSettings(SelectedAction);
        selectedActionSceneName = settings.SceneName;
        selectedActionPlaylistId = settings.PlaylistId;
        selectedActionTrackId = settings.TrackId;
        selectedActionSourceName = settings.SourceName;
        selectedActionFilterName = settings.FilterName;
        selectedActionFilterEnabled = settings.FilterEnabled;
        selectedActionVolume = settings.Volume;
        selectedActionCrossfadeSeconds = settings.CrossfadeSeconds;
        RefreshActionTracks();

        OnPropertyChanged(nameof(SelectedActionTarget));
        OnPropertyChanged(nameof(SelectedActionType));
        OnPropertyChanged(nameof(SelectedActionDisplayName));
        OnPropertyChanged(nameof(HasSelectedAction));
        OnPropertyChanged(nameof(SelectedActionSceneName));
        OnPropertyChanged(nameof(SelectedActionPlaylistId));
        OnPropertyChanged(nameof(SelectedActionTrackId));
        OnPropertyChanged(nameof(SelectedActionSourceName));
        OnPropertyChanged(nameof(SelectedActionFilterName));
        OnPropertyChanged(nameof(SelectedActionFilterEnabled));
        OnPropertyChanged(nameof(SelectedActionVolume));
        OnPropertyChanged(nameof(SelectedActionCrossfadeSeconds));
        OnPropertyChanged(nameof(CanEditActionScene));
        OnPropertyChanged(nameof(CanEditActionPlaylist));
        OnPropertyChanged(nameof(CanEditActionTrack));
        OnPropertyChanged(nameof(CanEditActionFilter));
        OnPropertyChanged(nameof(CanEditActionVolume));
        OnPropertyChanged(nameof(CanEditActionCrossfade));
        isLoading = false;
    }

    private void UpdateSelectedActionSettings()
    {
        if (isLoading || SelectedAction is null)
        {
            return;
        }

        SelectedAction.SettingsJson = JsonSerializer.Serialize(new AutomationActionSettings
        {
            SceneName = SelectedActionSceneName,
            PlaylistId = SelectedActionPlaylistId,
            TrackId = SelectedActionTrackId,
            SourceName = SelectedActionSourceName,
            FilterName = SelectedActionFilterName,
            FilterEnabled = SelectedActionFilterEnabled,
            Volume = SelectedActionVolume,
            CrossfadeSeconds = SelectedActionCrossfadeSeconds
        });
    }

    private void ReorderEvents()
    {
        if (SelectedRule is null)
        {
            return;
        }

        for (var index = 0; index < SelectedRule.Events.Count; index++)
        {
            SelectedRule.Events[index].SortOrder = index;
        }
    }

    private void ReorderActions()
    {
        if (SelectedRule is null)
        {
            return;
        }

        for (var index = 0; index < SelectedRule.Actions.Count; index++)
        {
            SelectedRule.Actions[index].SortOrder = index;
        }
    }

    private AutomationEventSettings ReadEventSettings(AutomationEvent? automationEvent)
    {
        if (automationEvent is null)
        {
            return new AutomationEventSettings();
        }

        try
        {
            return JsonSerializer.Deserialize<AutomationEventSettings>(automationEvent.SettingsJson, jsonOptions) ?? new AutomationEventSettings();
        }
        catch
        {
            return new AutomationEventSettings();
        }
    }

    private AutomationActionSettings ReadActionSettings(AutomationAction? action)
    {
        if (action is null)
        {
            return new AutomationActionSettings();
        }

        try
        {
            return JsonSerializer.Deserialize<AutomationActionSettings>(action.SettingsJson, jsonOptions) ?? new AutomationActionSettings();
        }
        catch
        {
            return new AutomationActionSettings();
        }
    }

    private LegacyActionParameters ReadLegacyActionParameters(string actionJson)
    {
        if (string.IsNullOrWhiteSpace(actionJson))
        {
            return new LegacyActionParameters();
        }

        try
        {
            return JsonSerializer.Deserialize<LegacyActionParameters>(actionJson, jsonOptions) ?? new LegacyActionParameters();
        }
        catch
        {
            return new LegacyActionParameters();
        }
    }

    private string ResolvePlaylistId(string playlistName)
    {
        return Playlists.FirstOrDefault(item => string.Equals(item.DisplayName, playlistName, StringComparison.OrdinalIgnoreCase))?.Value
            ?? Playlists.FirstOrDefault()?.Value
            ?? string.Empty;
    }

    private static string MapLegacyActionType(string actionType)
    {
        return actionType switch
        {
            "PlayPlaylist" => AutomationActionTypes.Player.PlayPlaylist,
            "PlayTrack" => AutomationActionTypes.Player.PlayTrack,
            "Stop" => AutomationActionTypes.Player.Stop,
            "Pause" => AutomationActionTypes.Player.Pause,
            "Resume" => AutomationActionTypes.Player.Resume,
            "SetVolume" => AutomationActionTypes.Player.SetVolume,
            _ => AutomationActionTypes.Player.PlayPlaylist
        };
    }

    private void RaiseCommandStates()
    {
        ((RelayCommand)RemoveRuleCommand).RaiseCanExecuteChanged();
        ((RelayCommand)AddEventOptionCommand).RaiseCanExecuteChanged();
        ((RelayCommand)RemoveEventCommand).RaiseCanExecuteChanged();
        ((RelayCommand)AddActionOptionCommand).RaiseCanExecuteChanged();
        ((RelayCommand)RemoveActionCommand).RaiseCanExecuteChanged();
    }

    private void RefreshActionTracks()
    {
        ActionTracks.Clear();
        var playlist = Playlists.FirstOrDefault(item => item.Value == SelectedActionPlaylistId);
        if (playlist is null)
        {
            return;
        }

        foreach (var track in playlist.Tracks)
        {
            ActionTracks.Add(track);
        }

        if (!string.IsNullOrWhiteSpace(selectedActionTrackId)
            && ActionTracks.All(item => item.Value != selectedActionTrackId))
        {
            selectedActionTrackId = string.Empty;
            OnPropertyChanged(nameof(SelectedActionTrackId));
        }
    }

    public sealed class PlaylistOption
    {
        public PlaylistOption(string value, string displayName, IEnumerable<TrackOption> tracks)
        {
            Value = value;
            DisplayName = displayName;
            foreach (var track in tracks.OrderBy(item => item.DisplayName))
            {
                Tracks.Add(track);
            }
        }

        public string Value { get; }
        public string DisplayName { get; }
        public ObservableCollection<TrackOption> Tracks { get; } = [];

        public override string ToString()
        {
            return DisplayName;
        }
    }

    public sealed class TrackOption
    {
        public TrackOption(string value, string displayName)
        {
            Value = value;
            DisplayName = displayName;
        }

        public string Value { get; }
        public string DisplayName { get; }

        public override string ToString()
        {
            return DisplayName;
        }
    }

    private sealed class LegacyActionParameters
    {
        public string PlaylistName { get; set; } = string.Empty;
        public double CrossfadeSeconds { get; set; } = 2;
        public int Volume { get; set; } = 100;
    }

    private IEnumerable<AutomationEventMenuOption> BuildObsEventMenu(
        IEnumerable<string> scenes,
        IEnumerable<ObsSceneItemInfo> sceneItems,
        IEnumerable<ObsSourceFilterInfo> sourceFilters)
    {
        var sceneChanged = new AutomationEventMenuOption { DisplayName = LocalizationService.T("TriggerSceneChanged") };
        sceneChanged.Children.Add(new AutomationEventMenuOption
        {
            DisplayName = LocalizationService.T("Any"),
            SourceType = AutomationSourceType.Obs,
            EventType = AutomationEventTypes.Obs.SceneChanged
        });
        foreach (var sceneName in scenes.OrderBy(item => item))
        {
            sceneChanged.Children.Add(new AutomationEventMenuOption
            {
                DisplayName = sceneName,
                SourceType = AutomationSourceType.Obs,
                EventType = AutomationEventTypes.Obs.SceneChanged,
                Settings = new AutomationEventSettings { SceneName = sceneName }
            });
        }

        var filterEnabled = BuildFilterEventMenu(
            LocalizationService.T("TriggerSourceFilterEnabled"),
            AutomationEventTypes.Obs.SourceFilterEnabled,
            true,
            sourceFilters);
        var filterDisabled = BuildFilterEventMenu(
            LocalizationService.T("TriggerSourceFilterDisabled"),
            AutomationEventTypes.Obs.SourceFilterDisabled,
            false,
            sourceFilters);
        var sceneItemEnabled = BuildSceneItemEventMenu(
            LocalizationService.T("TriggerSceneItemEnabled"),
            AutomationEventTypes.Obs.SceneItemEnabled,
            true,
            sceneItems);
        var sceneItemDisabled = BuildSceneItemEventMenu(
            LocalizationService.T("TriggerSceneItemDisabled"),
            AutomationEventTypes.Obs.SceneItemDisabled,
            false,
            sceneItems);

        return
        [
            sceneChanged,
            CreateSimpleObsEvent(LocalizationService.T("TriggerStreamStarted"), AutomationEventTypes.Obs.StreamStarted),
            CreateSimpleObsEvent(LocalizationService.T("TriggerStreamStopped"), AutomationEventTypes.Obs.StreamStopped),
            CreateSimpleObsEvent(LocalizationService.T("TriggerRecordingStarted"), AutomationEventTypes.Obs.RecordingStarted),
            CreateSimpleObsEvent(LocalizationService.T("TriggerRecordingStopped"), AutomationEventTypes.Obs.RecordingStopped),
            CreateSimpleObsEvent(LocalizationService.T("TriggerRecordingPaused"), AutomationEventTypes.Obs.RecordingPaused),
            CreateSimpleObsEvent(LocalizationService.T("TriggerRecordingResumed"), AutomationEventTypes.Obs.RecordingResumed),
            sceneItemEnabled,
            sceneItemDisabled,
            filterEnabled,
            filterDisabled
        ];
    }

    private IEnumerable<AutomationEventMenuOption> BuildPlayerEventMenu()
    {
        var playlistFinished = new AutomationEventMenuOption { DisplayName = LocalizationService.T("TriggerPlaylistFinished") };
        playlistFinished.Children.Add(new AutomationEventMenuOption
        {
            DisplayName = LocalizationService.T("Any"),
            SourceType = AutomationSourceType.Player,
            EventType = AutomationEventTypes.Player.PlaylistFinished
        });
        foreach (var playlist in Playlists)
        {
            playlistFinished.Children.Add(new AutomationEventMenuOption
            {
                DisplayName = playlist.DisplayName,
                SourceType = AutomationSourceType.Player,
                EventType = AutomationEventTypes.Player.PlaylistFinished,
                Settings = new AutomationEventSettings
                {
                    PlaylistId = playlist.Value,
                    PlaylistName = playlist.DisplayName
                }
            });
        }

        var trackFinished = new AutomationEventMenuOption { DisplayName = LocalizationService.T("TriggerTrackFinished") };
        trackFinished.Children.Add(new AutomationEventMenuOption
        {
            DisplayName = LocalizationService.T("Any"),
            SourceType = AutomationSourceType.Player,
            EventType = AutomationEventTypes.Player.TrackFinished
        });
        foreach (var playlist in Playlists)
        {
            var playlistGroup = new AutomationEventMenuOption { DisplayName = playlist.DisplayName };
            playlistGroup.Children.Add(new AutomationEventMenuOption
            {
                DisplayName = LocalizationService.T("Any"),
                SourceType = AutomationSourceType.Player,
                EventType = AutomationEventTypes.Player.TrackFinished,
                Settings = new AutomationEventSettings
                {
                    PlaylistId = playlist.Value,
                    PlaylistName = playlist.DisplayName
                }
            });
            foreach (var track in playlist.Tracks)
            {
                playlistGroup.Children.Add(new AutomationEventMenuOption
                {
                    DisplayName = track.DisplayName,
                    SourceType = AutomationSourceType.Player,
                    EventType = AutomationEventTypes.Player.TrackFinished,
                    Settings = new AutomationEventSettings
                    {
                        PlaylistId = playlist.Value,
                        PlaylistName = playlist.DisplayName,
                        TrackId = track.Value,
                        TrackName = track.DisplayName
                    }
                });
            }

            trackFinished.Children.Add(playlistGroup);
        }

        return
        [
            playlistFinished,
            trackFinished,
            CreateSimplePlayerEvent(LocalizationService.T("TriggerPlaybackStopped"), AutomationEventTypes.Player.PlaybackStopped),
            CreateSimplePlayerEvent(LocalizationService.T("TriggerPlaybackPaused"), AutomationEventTypes.Player.PlaybackPaused),
            CreateSimplePlayerEvent(LocalizationService.T("TriggerPlaybackResumed"), AutomationEventTypes.Player.PlaybackResumed)
        ];
    }

    private static AutomationEventMenuOption CreateSimpleObsEvent(string displayName, string eventType)
    {
        return new AutomationEventMenuOption
        {
            DisplayName = displayName,
            SourceType = AutomationSourceType.Obs,
            EventType = eventType
        };
    }

    private static AutomationEventMenuOption CreateSimplePlayerEvent(string displayName, string eventType)
    {
        return new AutomationEventMenuOption
        {
            DisplayName = displayName,
            SourceType = AutomationSourceType.Player,
            EventType = eventType
        };
    }

    private static AutomationEventMenuOption BuildFilterEventMenu(
        string displayName,
        string eventType,
        bool filterEnabled,
        IEnumerable<ObsSourceFilterInfo> sourceFilters)
    {
        var root = new AutomationEventMenuOption { DisplayName = displayName };
        root.Children.Add(new AutomationEventMenuOption
        {
            DisplayName = LocalizationService.T("Any"),
            SourceType = AutomationSourceType.Obs,
            EventType = eventType,
            Settings = new AutomationEventSettings { FilterEnabled = filterEnabled }
        });

        foreach (var sourceGroup in sourceFilters.GroupBy(item => item.SourceName).OrderBy(item => item.Key))
        {
            var sourceOption = new AutomationEventMenuOption { DisplayName = sourceGroup.Key };
            sourceOption.Children.Add(new AutomationEventMenuOption
            {
                DisplayName = LocalizationService.T("Any"),
                SourceType = AutomationSourceType.Obs,
                EventType = eventType,
                Settings = new AutomationEventSettings
                {
                    SourceName = sourceGroup.Key,
                    FilterEnabled = filterEnabled
                }
            });

            foreach (var filter in sourceGroup.OrderBy(item => item.FilterName))
            {
                sourceOption.Children.Add(new AutomationEventMenuOption
                {
                    DisplayName = filter.FilterName,
                    SourceType = AutomationSourceType.Obs,
                    EventType = eventType,
                    Settings = new AutomationEventSettings
                    {
                        SourceName = filter.SourceName,
                        FilterName = filter.FilterName,
                        FilterEnabled = filterEnabled
                    }
                });
            }

            root.Children.Add(sourceOption);
        }

        return root;
    }

    private static AutomationEventMenuOption BuildSceneItemEventMenu(
        string displayName,
        string eventType,
        bool enabled,
        IEnumerable<ObsSceneItemInfo> sceneItems)
    {
        var root = new AutomationEventMenuOption { DisplayName = displayName };
        root.Children.Add(new AutomationEventMenuOption
        {
            DisplayName = LocalizationService.T("Any"),
            SourceType = AutomationSourceType.Obs,
            EventType = eventType,
            Settings = new AutomationEventSettings { FilterEnabled = enabled }
        });

        foreach (var sceneGroup in sceneItems.GroupBy(item => item.SceneName).OrderBy(item => item.Key))
        {
            var sceneOption = new AutomationEventMenuOption { DisplayName = sceneGroup.Key };
            sceneOption.Children.Add(new AutomationEventMenuOption
            {
                DisplayName = LocalizationService.T("Any"),
                SourceType = AutomationSourceType.Obs,
                EventType = eventType,
                Settings = new AutomationEventSettings
                {
                    SceneName = sceneGroup.Key,
                    FilterEnabled = enabled
                }
            });

            foreach (var sceneItem in sceneGroup.OrderBy(item => item.SourceName).ThenBy(item => item.SceneItemId))
            {
                sceneOption.Children.Add(new AutomationEventMenuOption
                {
                    DisplayName = sceneItem.SourceName,
                    SourceType = AutomationSourceType.Obs,
                    EventType = eventType,
                    Settings = new AutomationEventSettings
                    {
                        SceneName = sceneItem.SceneName,
                        SourceName = sceneItem.SourceName,
                        SceneItemId = sceneItem.SceneItemId,
                        FilterEnabled = enabled
                    }
                });
            }

            root.Children.Add(sceneOption);
        }

        return root;
    }

    private IEnumerable<AutomationActionMenuOption> BuildObsActionMenu(
        IEnumerable<string> scenes,
        IEnumerable<ObsSceneItemInfo> sceneItems,
        IEnumerable<ObsSourceFilterInfo> sourceFilters)
    {
        var changeScene = new AutomationActionMenuOption { DisplayName = LocalizationService.T("ActionChangeScene") };
        foreach (var sceneName in scenes.OrderBy(item => item))
        {
            changeScene.Children.Add(new AutomationActionMenuOption
            {
                DisplayName = sceneName,
                TargetType = AutomationTargetType.Obs,
                ActionType = AutomationActionTypes.Obs.ChangeScene,
                Settings = new AutomationActionSettings { SceneName = sceneName }
            });
        }

        var sceneItemEnabled = new AutomationActionMenuOption { DisplayName = LocalizationService.T("ActionSetSceneItemEnabled") };
        foreach (var sceneGroup in sceneItems.GroupBy(item => item.SceneName).OrderBy(item => item.Key))
        {
            var sceneOption = new AutomationActionMenuOption { DisplayName = sceneGroup.Key };
            foreach (var sceneItem in sceneGroup.OrderBy(item => item.SourceName).ThenBy(item => item.SceneItemId))
            {
                sceneOption.Children.Add(new AutomationActionMenuOption
                {
                    DisplayName = sceneItem.SourceName,
                    TargetType = AutomationTargetType.Obs,
                    ActionType = AutomationActionTypes.Obs.SetSceneItemEnabled,
                    Settings = new AutomationActionSettings
                    {
                        SceneName = sceneItem.SceneName,
                        SourceName = sceneItem.SourceName,
                        SceneItemId = sceneItem.SceneItemId,
                        FilterEnabled = true
                    }
                });
            }

            sceneItemEnabled.Children.Add(sceneOption);
        }

        var sourceFilter = new AutomationActionMenuOption { DisplayName = LocalizationService.T("ActionSetSourceFilterEnabled") };
        foreach (var sourceGroup in sourceFilters.GroupBy(item => item.SourceName).OrderBy(item => item.Key))
        {
            var sourceOption = new AutomationActionMenuOption { DisplayName = sourceGroup.Key };
            foreach (var filter in sourceGroup.OrderBy(item => item.FilterName))
            {
                sourceOption.Children.Add(new AutomationActionMenuOption
                {
                    DisplayName = filter.FilterName,
                    TargetType = AutomationTargetType.Obs,
                    ActionType = AutomationActionTypes.Obs.SetSourceFilterEnabled,
                    Settings = new AutomationActionSettings
                    {
                        SourceName = filter.SourceName,
                        FilterName = filter.FilterName,
                        FilterEnabled = true
                    }
                });
            }

            sourceFilter.Children.Add(sourceOption);
        }

        return
        [
            changeScene,
            CreateSimpleObsAction(LocalizationService.T("ActionStartStream"), AutomationActionTypes.Obs.StartStream),
            CreateSimpleObsAction(LocalizationService.T("ActionStopStream"), AutomationActionTypes.Obs.StopStream),
            CreateSimpleObsAction(LocalizationService.T("ActionStartRecording"), AutomationActionTypes.Obs.StartRecording),
            CreateSimpleObsAction(LocalizationService.T("ActionStopRecording"), AutomationActionTypes.Obs.StopRecording),
            CreateSimpleObsAction(LocalizationService.T("ActionPauseRecording"), AutomationActionTypes.Obs.PauseRecording),
            CreateSimpleObsAction(LocalizationService.T("ActionResumeRecording"), AutomationActionTypes.Obs.ResumeRecording),
            sceneItemEnabled,
            sourceFilter
        ];
    }

    private IEnumerable<AutomationActionMenuOption> BuildPlayerActionMenu()
    {
        var playPlaylist = new AutomationActionMenuOption { DisplayName = LocalizationService.T("ActionPlayPlaylist") };
        foreach (var playlist in Playlists)
        {
            playPlaylist.Children.Add(new AutomationActionMenuOption
            {
                DisplayName = playlist.DisplayName,
                TargetType = AutomationTargetType.Player,
                ActionType = AutomationActionTypes.Player.PlayPlaylist,
                Settings = new AutomationActionSettings { PlaylistId = playlist.Value }
            });
        }

        var playTrack = new AutomationActionMenuOption { DisplayName = LocalizationService.T("ActionPlayTrack") };
        foreach (var playlist in Playlists)
        {
            var playlistOption = new AutomationActionMenuOption { DisplayName = playlist.DisplayName };
            foreach (var track in playlist.Tracks)
            {
                playlistOption.Children.Add(new AutomationActionMenuOption
                {
                    DisplayName = track.DisplayName,
                    TargetType = AutomationTargetType.Player,
                    ActionType = AutomationActionTypes.Player.PlayTrack,
                    Settings = new AutomationActionSettings
                    {
                        PlaylistId = playlist.Value,
                        TrackId = track.Value
                    }
                });
            }

            playTrack.Children.Add(playlistOption);
        }

        return
        [
            playPlaylist,
            playTrack,
            CreateSimplePlayerAction(LocalizationService.T("ActionPlayNextPlaylist"), AutomationActionTypes.Player.PlayNextPlaylist),
            CreateSimplePlayerAction(LocalizationService.T("ActionPlayPreviousPlaylist"), AutomationActionTypes.Player.PlayPreviousPlaylist),
            CreateSimplePlayerAction(LocalizationService.T("ActionStop"), AutomationActionTypes.Player.Stop),
            CreateSimplePlayerAction(LocalizationService.T("ActionPause"), AutomationActionTypes.Player.Pause),
            CreateSimplePlayerAction(LocalizationService.T("ActionResume"), AutomationActionTypes.Player.Resume),
            CreateSimplePlayerAction(LocalizationService.T("ActionSetVolume"), AutomationActionTypes.Player.SetVolume, new AutomationActionSettings { Volume = 100 }),
            CreateSimplePlayerAction(LocalizationService.T("ActionSetCrossfade"), AutomationActionTypes.Player.SetCrossfade, new AutomationActionSettings { CrossfadeSeconds = 2 })
        ];
    }

    private static AutomationActionMenuOption CreateSimpleObsAction(string displayName, string actionType)
    {
        return new AutomationActionMenuOption
        {
            DisplayName = displayName,
            TargetType = AutomationTargetType.Obs,
            ActionType = actionType
        };
    }

    private static AutomationActionMenuOption CreateSimplePlayerAction(
        string displayName,
        string actionType,
        AutomationActionSettings? settings = null)
    {
        return new AutomationActionMenuOption
        {
            DisplayName = displayName,
            TargetType = AutomationTargetType.Player,
            ActionType = actionType,
            Settings = settings ?? new AutomationActionSettings()
        };
    }
}
