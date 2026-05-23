using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows.Input;
using StreamMusicPlayer.Models;
using StreamMusicPlayer.Repositories;
using StreamMusicPlayer.Services;

namespace StreamMusicPlayer.ViewModels;

public sealed class EventRulesViewModel : ObservableObject
{
    private readonly EventRuleRepository eventRuleRepository;
    private EventRule? selectedRule;
    private string statusMessage = LocalizationService.T("Ready");
    private bool isLoadingRule;
    private string selectedRuleName = string.Empty;
    private bool selectedRuleEnabled = true;
    private string selectedTriggerType = "SceneChanged";
    private string selectedTriggerCondition = string.Empty;
    private string selectedActionType = "PlayPlaylist";
    private int selectedPriority = 100;
    private double selectedDelaySeconds;
    private string selectedActionPlaylistName = string.Empty;
    private double selectedActionCrossfadeSeconds = 2;
    private int selectedActionVolume = 100;

    public EventRulesViewModel(EventRuleRepository eventRuleRepository, IEnumerable<Playlist> playlists, IEnumerable<string> scenes)
    {
        this.eventRuleRepository = eventRuleRepository;
        Rules = new ObservableCollection<EventRule>(eventRuleRepository.LoadAll());
        PlaylistNames = new ObservableCollection<string>(playlists.Select(item => item.Name).OrderBy(item => item));
        SceneNames = new ObservableCollection<string>(scenes.OrderBy(item => item));
        ActionTypes = new ObservableCollection<LocalizedValueOption<string>>(
        [
            new("PlayPlaylist", "ActionPlayPlaylist"),
            new("Stop", "ActionStop"),
            new("Pause", "ActionPause"),
            new("Resume", "ActionResume"),
            new("SetVolume", "ActionSetVolume")
        ]);
        TriggerTypes = new ObservableCollection<LocalizedValueOption<string>>(
        [
            new("SceneChanged", "TriggerSceneChanged"),
            new("StreamStarted", "TriggerStreamStarted"),
            new("StreamStopped", "TriggerStreamStopped"),
            new("RecordingStarted", "TriggerRecordingStarted"),
            new("RecordingStopped", "TriggerRecordingStopped"),
            new("RecordingPaused", "TriggerRecordingPaused"),
            new("RecordingResumed", "TriggerRecordingResumed")
        ]);

        AddRuleCommand = new RelayCommand(_ => AddRule());
        RemoveRuleCommand = new RelayCommand(_ => RemoveRule(), _ => SelectedRule is not null);
        SaveRulesCommand = new RelayCommand(_ => SaveRules());
        LocalizationService.LanguageChanged += OnLanguageChanged;

        SelectedRule = Rules.FirstOrDefault();
    }

    public ObservableCollection<EventRule> Rules { get; }
    public ObservableCollection<string> PlaylistNames { get; }
    public ObservableCollection<string> SceneNames { get; }
    public ObservableCollection<LocalizedValueOption<string>> ActionTypes { get; }
    public ObservableCollection<LocalizedValueOption<string>> TriggerTypes { get; }

    public EventRule? SelectedRule
    {
        get => selectedRule;
        set
        {
            if (SetProperty(ref selectedRule, value))
            {
                LoadSelectedRuleActionParameters();
                if (RemoveRuleCommand is RelayCommand removeRuleCommand)
                {
                    removeRuleCommand.RaiseCanExecuteChanged();
                }
            }
        }
    }

    public string StatusMessage
    {
        get => statusMessage;
        private set => SetProperty(ref statusMessage, value);
    }

    public ICommand AddRuleCommand { get; }
    public ICommand RemoveRuleCommand { get; }
    public ICommand SaveRulesCommand { get; }

    public string SelectedRuleName
    {
        get => selectedRuleName;
        set
        {
            if (SetProperty(ref selectedRuleName, value))
            {
                UpdateSelectedRule();
            }
        }
    }

    public bool SelectedRuleEnabled
    {
        get => selectedRuleEnabled;
        set
        {
            if (SetProperty(ref selectedRuleEnabled, value))
            {
                UpdateSelectedRule();
            }
        }
    }

    public string SelectedTriggerType
    {
        get => selectedTriggerType;
        set
        {
            if (SetProperty(ref selectedTriggerType, value))
            {
                if (selectedTriggerType != "SceneChanged")
                {
                    SelectedTriggerCondition = string.Empty;
                }

                OnPropertyChanged(nameof(CanSelectTriggerCondition));
                UpdateSelectedRule();
            }
        }
    }

    public string SelectedTriggerCondition
    {
        get => selectedTriggerCondition;
        set
        {
            if (SetProperty(ref selectedTriggerCondition, value))
            {
                UpdateSelectedRule();
            }
        }
    }

    public string SelectedActionType
    {
        get => selectedActionType;
        set
        {
            if (SetProperty(ref selectedActionType, value))
            {
                OnPropertyChanged(nameof(CanSelectActionPlaylist));
                OnPropertyChanged(nameof(CanEditActionCrossfade));
                OnPropertyChanged(nameof(CanEditActionVolume));
                UpdateSelectedRule();
            }
        }
    }

    public bool CanSelectTriggerCondition => SelectedTriggerType == "SceneChanged";

    public bool CanSelectActionPlaylist => SelectedActionType == "PlayPlaylist";

    public bool CanEditActionCrossfade => SelectedActionType == "PlayPlaylist";

    public bool CanEditActionVolume => SelectedActionType is "PlayPlaylist" or "SetVolume";

    public int SelectedPriority
    {
        get => selectedPriority;
        set
        {
            if (SetProperty(ref selectedPriority, value))
            {
                UpdateSelectedRule();
            }
        }
    }

    public double SelectedDelaySeconds
    {
        get => selectedDelaySeconds;
        set
        {
            if (SetProperty(ref selectedDelaySeconds, Math.Max(0, value)))
            {
                UpdateSelectedRule();
            }
        }
    }

    public string SelectedActionPlaylistName
    {
        get => selectedActionPlaylistName;
        set
        {
            if (SetProperty(ref selectedActionPlaylistName, value))
            {
                UpdateSelectedRuleActionJson();
            }
        }
    }

    public double SelectedActionCrossfadeSeconds
    {
        get => selectedActionCrossfadeSeconds;
        set
        {
            if (SetProperty(ref selectedActionCrossfadeSeconds, Math.Max(0, value)))
            {
                UpdateSelectedRuleActionJson();
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
                UpdateSelectedRuleActionJson();
            }
        }
    }

    private void AddRule()
    {
        var playlistName = PlaylistNames.FirstOrDefault() ?? string.Empty;
        var rule = new EventRule
        {
            Name = LocalizationService.F("SceneRuleFormat", Rules.Count + 1),
            TriggerSceneName = SceneNames.FirstOrDefault() ?? "Starting Soon",
            ActionType = "PlayPlaylist",
            ActionJson = JsonSerializer.Serialize(new
            {
                playlistName,
                crossfadeSeconds = 2,
                volume = 100
            })
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

    private void SaveRules()
    {
        UpdateSelectedRule();
        UpdateSelectedRuleActionJson();
        eventRuleRepository.SaveAll(Rules);
        StatusMessage = LocalizationService.T("RulesSaved");
    }

    private void LoadSelectedRuleActionParameters()
    {
        if (SelectedRule is null)
        {
            isLoadingRule = true;
            SelectedRuleName = string.Empty;
            SelectedRuleEnabled = true;
            SelectedTriggerType = "SceneChanged";
            SelectedTriggerCondition = string.Empty;
            SelectedActionType = "PlayPlaylist";
            SelectedPriority = 100;
            SelectedDelaySeconds = 0;
            SelectedActionPlaylistName = string.Empty;
            SelectedActionCrossfadeSeconds = 2;
            SelectedActionVolume = 100;
            isLoadingRule = false;
            return;
        }

        isLoadingRule = true;
        selectedRuleName = SelectedRule.Name;
        selectedRuleEnabled = SelectedRule.Enabled;
        selectedTriggerType = string.IsNullOrWhiteSpace(SelectedRule.TriggerType) ? "SceneChanged" : SelectedRule.TriggerType;
        selectedTriggerCondition = SelectedRule.TriggerSceneName;
        selectedActionType = string.IsNullOrWhiteSpace(SelectedRule.ActionType) ? "PlayPlaylist" : SelectedRule.ActionType;
        selectedPriority = SelectedRule.Priority;
        selectedDelaySeconds = SelectedRule.DelaySeconds;
        OnPropertyChanged(nameof(SelectedRuleName));
        OnPropertyChanged(nameof(SelectedRuleEnabled));
        OnPropertyChanged(nameof(SelectedTriggerType));
        OnPropertyChanged(nameof(SelectedTriggerCondition));
        OnPropertyChanged(nameof(SelectedActionType));
        OnPropertyChanged(nameof(CanSelectTriggerCondition));
        OnPropertyChanged(nameof(CanSelectActionPlaylist));
        OnPropertyChanged(nameof(CanEditActionCrossfade));
        OnPropertyChanged(nameof(CanEditActionVolume));
        OnPropertyChanged(nameof(SelectedPriority));
        OnPropertyChanged(nameof(SelectedDelaySeconds));

        try
        {
            var parameters = JsonSerializer.Deserialize<RuleEditorActionParameters>(
                SelectedRule.ActionJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new RuleEditorActionParameters();
            selectedActionPlaylistName = parameters.PlaylistName;
            selectedActionCrossfadeSeconds = parameters.CrossfadeSeconds;
            selectedActionVolume = parameters.Volume;
            OnPropertyChanged(nameof(SelectedActionPlaylistName));
            OnPropertyChanged(nameof(SelectedActionCrossfadeSeconds));
            OnPropertyChanged(nameof(SelectedActionVolume));
        }
        catch
        {
            selectedActionPlaylistName = PlaylistNames.FirstOrDefault() ?? string.Empty;
            selectedActionCrossfadeSeconds = 2;
            selectedActionVolume = 100;
        }

        isLoadingRule = false;
    }

    private void UpdateSelectedRuleActionJson()
    {
        if (SelectedRule is null)
        {
            return;
        }

        SelectedRule.ActionJson = JsonSerializer.Serialize(new RuleEditorActionParameters
        {
            PlaylistName = SelectedActionPlaylistName,
            CrossfadeSeconds = SelectedActionCrossfadeSeconds,
            Volume = SelectedActionVolume
        });
    }

    private void UpdateSelectedRule()
    {
        if (isLoadingRule || SelectedRule is null)
        {
            return;
        }

        SelectedRule.Name = string.IsNullOrWhiteSpace(SelectedRuleName) ? LocalizationService.T("NewRule") : SelectedRuleName.Trim();
        SelectedRule.Enabled = SelectedRuleEnabled;
        SelectedRule.TriggerType = string.IsNullOrWhiteSpace(SelectedTriggerType) ? "SceneChanged" : SelectedTriggerType;
        SelectedRule.TriggerSceneName = SelectedTriggerCondition?.Trim() ?? string.Empty;
        SelectedRule.ActionType = string.IsNullOrWhiteSpace(SelectedActionType) ? "PlayPlaylist" : SelectedActionType;
        SelectedRule.Priority = SelectedPriority;
        SelectedRule.DelaySeconds = SelectedDelaySeconds;
    }

    private sealed class RuleEditorActionParameters
    {
        public string PlaylistName { get; set; } = string.Empty;
        public double CrossfadeSeconds { get; set; }
        public int Volume { get; set; } = 100;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        foreach (var triggerType in TriggerTypes)
        {
            triggerType.Refresh();
        }

        foreach (var actionType in ActionTypes)
        {
            actionType.Refresh();
        }

        if (StatusMessage is "Ready" or "Готово" or "Готово" or "Gotowe")
        {
            StatusMessage = LocalizationService.T("Ready");
        }
    }
}
