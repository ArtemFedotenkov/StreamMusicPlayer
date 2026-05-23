using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Threading;
using System.Windows.Input;
using Microsoft.Win32;
using StreamMusicPlayer.Audio;
using StreamMusicPlayer.Data;
using StreamMusicPlayer.Models;
using StreamMusicPlayer.Obs;
using StreamMusicPlayer.Repositories;
using StreamMusicPlayer.Rules;
using StreamMusicPlayer.Services;
using StreamMusicPlayer.Views;

namespace StreamMusicPlayer.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private Playlist? selectedPlaylist;
    private Track? selectedTrack;
    private Track? currentTrack;
    private PlaybackState playbackState;
    private int volume = 100;
    private double positionSeconds;
    private double crossfadeSeconds = 2;
    private string obsStatus = LocalizationService.T("Disconnected");
    private string activeScene = LocalizationService.T("None");
    private string playerMessage = LocalizationService.T("Ready");
    private bool automaticTransitionPending;
    private readonly PlaylistRepository playlistRepository;
    private readonly EventRuleRepository eventRuleRepository;
    private readonly AppSettingsRepository appSettingsRepository;
    private readonly AudioPlaybackService audioPlaybackService;
    private readonly ApplicationSettingsService applicationSettingsService;
    private readonly AudioOutputDeviceService audioOutputDeviceService;
    private readonly ApplicationDataResetService applicationDataResetService = new();
    private readonly ObsSettingsService obsSettingsService;
    private readonly ObsClientService obsClientService;
    private readonly EventRulesEngine eventRulesEngine = new();
    private readonly DispatcherTimer positionTimer;
    private readonly Random random = new();

    public MainViewModel()
    {
        AppDataPaths.EnsureCreated();
        new DatabaseInitializer(AppDataPaths.DatabasePath).Initialize();
        playlistRepository = new PlaylistRepository(AppDataPaths.DatabasePath);
        eventRuleRepository = new EventRuleRepository(AppDataPaths.DatabasePath);
        appSettingsRepository = new AppSettingsRepository(AppDataPaths.DatabasePath);
        applicationSettingsService = new ApplicationSettingsService(appSettingsRepository);
        audioOutputDeviceService = new AudioOutputDeviceService();
        obsSettingsService = new ObsSettingsService(appSettingsRepository);
        obsClientService = new ObsClientService();
        obsClientService.Connected += OnObsConnected;
        obsClientService.Disconnected += OnObsDisconnected;
        obsClientService.CurrentProgramSceneChanged += OnObsSceneChanged;
        obsClientService.ObsEventTriggered += OnObsEventTriggered;
        audioPlaybackService = new AudioPlaybackService();
        LocalizationService.LanguageChanged += OnLanguageChanged;
        ApplyApplicationAudioSettings();
        audioPlaybackService.PlaybackEnded += OnPlaybackEnded;

        var storedPlaylists = playlistRepository.LoadAll();
        Playlists = storedPlaylists.Count > 0
            ? new ObservableCollection<Playlist>(storedPlaylists)
            : CreateSamplePlaylists();

        if (storedPlaylists.Count == 0)
        {
            SavePlaylists();
        }

        selectedPlaylist = Playlists.FirstOrDefault();
        selectedTrack = selectedPlaylist?.Tracks.FirstOrDefault();

        AddPlaylistCommand = new RelayCommand(_ => AddPlaylist());
        RemovePlaylistCommand = new RelayCommand(_ => RemoveSelectedPlaylist(), _ => SelectedPlaylist is not null);
        AddTracksCommand = new RelayCommand(_ => AddTracks(), _ => SelectedPlaylist is not null);
        AddFolderCommand = new RelayCommand(_ => AddFolder(), _ => SelectedPlaylist is not null);
        RemoveTrackCommand = new RelayCommand(_ => RemoveSelectedTrack(), _ => SelectedPlaylist is not null && SelectedTrack is not null);
        ClearPlaylistCommand = new RelayCommand(_ => ClearSelectedPlaylist(), _ => SelectedPlaylist?.Tracks.Count > 0);
        OpenPlaylistRulesCommand = new RelayCommand(_ => OpenPlaylistRules(), _ => SelectedPlaylist is not null);
        MovePlaylistLeftCommand = new RelayCommand(_ => MoveSelectedPlaylist(-1), _ => CanMoveSelectedPlaylist(-1));
        MovePlaylistRightCommand = new RelayCommand(_ => MoveSelectedPlaylist(1), _ => CanMoveSelectedPlaylist(1));
        PlaySelectedTrackCommand = new RelayCommand(async _ => await PlaySelectedTrackAsync(), _ => SelectedTrack is not null);
        PlayPauseCommand = new RelayCommand(async _ => await TogglePlayPauseAsync(), _ => SelectedTrack is not null || CurrentTrack is not null);
        StopCommand = new RelayCommand(async _ => await StopAsync(), _ => PlaybackState != PlaybackState.Stopped);
        PreviousCommand = new RelayCommand(async _ => await SelectOffsetTrackAsync(-1), _ => SelectedPlaylist?.Tracks.Count > 0);
        NextCommand = new RelayCommand(async _ => await SelectOffsetTrackAsync(1), _ => SelectedPlaylist?.Tracks.Count > 0);
        ConnectObsCommand = new RelayCommand(async _ => await ConnectObsAsync(), _ => !obsClientService.IsConnected);
        OpenObsSettingsCommand = new RelayCommand(_ => OpenObsSettings());
        OpenEventRulesCommand = new RelayCommand(_ => OpenEventRules());
        OpenAppSettingsCommand = new RelayCommand(_ => OpenAppSettings());

        positionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        positionTimer.Tick += (_, _) => SyncPositionFromAudio();
        positionTimer.Start();

        var obsSettings = obsSettingsService.Load();
        if (obsSettings.AutoConnectOnStartup)
        {
            _ = ConnectToObsOnStartupAsync(obsSettings);
        }
    }

    public ObservableCollection<Playlist> Playlists { get; }

    public Playlist? SelectedPlaylist
    {
        get => selectedPlaylist;
        set
        {
            if (SetProperty(ref selectedPlaylist, value))
            {
                SelectedTrack = selectedPlaylist?.Tracks.FirstOrDefault();
                OnPropertyChanged(nameof(CurrentPlaylistName));
                OnPropertyChanged(nameof(SelectedPlaylistName));
                OnPropertyChanged(nameof(SelectedPlaylistTracksCountText));
                OnPropertyChanged(nameof(StatusLine));
                RaisePlaybackCommandStates();
            }
        }
    }

    public Track? SelectedTrack
    {
        get => selectedTrack;
        set
        {
            if (SetProperty(ref selectedTrack, value))
            {
                RaisePlaybackCommandStates();
            }
        }
    }

    public Track? CurrentTrack
    {
        get => currentTrack;
        private set
        {
            if (SetProperty(ref currentTrack, value))
            {
                OnPropertyChanged(nameof(CurrentTrackTitle));
                OnPropertyChanged(nameof(PositionText));
                OnPropertyChanged(nameof(StatusLine));
                OnPropertyChanged(nameof(PlaybackStateText));
                RaisePlaybackCommandStates();
            }
        }
    }

    public PlaybackState PlaybackState
    {
        get => playbackState;
        private set
        {
            if (SetProperty(ref playbackState, value))
            {
                OnPropertyChanged(nameof(StatusLine));
                RaisePlaybackCommandStates();
            }
        }
    }

    public int Volume
    {
        get => volume;
        set
        {
            if (SetProperty(ref volume, value))
            {
                _ = audioPlaybackService.FadeVolumeToAsync(VolumeToFloat(value), CrossfadeSeconds);
            }
        }
    }

    public double PositionSeconds
    {
        get => positionSeconds;
        set
        {
            if (SetProperty(ref positionSeconds, value))
            {
                OnPropertyChanged(nameof(PositionText));
                if (CurrentTrack is not null && Math.Abs(audioPlaybackService.PositionSeconds - value) > 1)
                {
                    audioPlaybackService.PositionSeconds = value;
                }
            }
        }
    }

    public double CrossfadeSeconds
    {
        get => crossfadeSeconds;
        set => SetProperty(ref crossfadeSeconds, value);
    }

    public string ObsStatus
    {
        get => obsStatus;
        set
        {
            if (SetProperty(ref obsStatus, value))
            {
                OnPropertyChanged(nameof(StatusLine));
                OnPropertyChanged(nameof(ObsStatusText));
            }
        }
    }

    public string ActiveScene
    {
        get => activeScene;
        set
        {
            if (SetProperty(ref activeScene, value))
            {
                OnPropertyChanged(nameof(StatusLine));
                OnPropertyChanged(nameof(ActiveSceneText));
            }
        }
    }

    public string PlayerMessage
    {
        get => playerMessage;
        private set => SetProperty(ref playerMessage, value);
    }

    public string CurrentPlaylistName => SelectedPlaylist?.Name ?? "None";
    public string SelectedPlaylistName
    {
        get => SelectedPlaylist?.Name ?? string.Empty;
        set
        {
            if (SelectedPlaylist is null)
            {
                return;
            }

            var normalizedName = string.IsNullOrWhiteSpace(value) ? "Untitled Playlist" : value.Trim();
            if (SelectedPlaylist.Name == normalizedName)
            {
                return;
            }

            SelectedPlaylist.Name = normalizedName;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CurrentPlaylistName));
            OnPropertyChanged(nameof(StatusLine));
            SavePlaylists();
        }
    }

    public string CurrentTrackTitle => CurrentTrack?.DisplayTitle ?? LocalizationService.T("NothingPlaying");
    public string ObsStatusText => LocalizationService.F("StatusObsFormat", ObsStatus);
    public string ActiveSceneText => LocalizationService.F("StatusSceneFormat", ActiveScene);
    public string CurrentPlaylistText => LocalizationService.F("StatusPlaylistFormat", CurrentPlaylistName);
    public string PlaybackStateText => LocalizationService.F("StatusStateFormat", LocalizePlaybackState(PlaybackState));
    public string SelectedPlaylistTracksCountText => LocalizationService.F("TracksCountFormat", SelectedPlaylist?.Tracks.Count ?? 0);
    public string PositionText => $"{TimeSpan.FromSeconds(PositionSeconds):mm\\:ss} / {CurrentTrack?.DurationText ?? "--:--"}";
    public string StatusLine => LocalizationService.F("StatusLineFormat", ObsStatus, ActiveScene, CurrentPlaylistName, CurrentTrackTitle, LocalizePlaybackState(PlaybackState));

    public ICommand AddPlaylistCommand { get; }
    public ICommand RemovePlaylistCommand { get; }
    public ICommand AddTracksCommand { get; }
    public ICommand AddFolderCommand { get; }
    public ICommand RemoveTrackCommand { get; }
    public ICommand ClearPlaylistCommand { get; }
    public ICommand OpenPlaylistRulesCommand { get; }
    public ICommand MovePlaylistLeftCommand { get; }
    public ICommand MovePlaylistRightCommand { get; }
    public ICommand PlaySelectedTrackCommand { get; }
    public ICommand PlayPauseCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand PreviousCommand { get; }
    public ICommand NextCommand { get; }
    public ICommand ConnectObsCommand { get; }
    public ICommand OpenObsSettingsCommand { get; }
    public ICommand OpenEventRulesCommand { get; }
    public ICommand OpenAppSettingsCommand { get; }

    private static ObservableCollection<Playlist> CreateSamplePlaylists()
    {
        var intro = new Playlist { Name = "Starting Soon", SortOrder = 1 };
        intro.Tracks.Add(new Track
        {
            PlaylistId = intro.Id,
            DisplayTitle = "ambient_loop.mp3",
            FilePath = @"D:\Music\ambient_loop.mp3",
            Duration = TimeSpan.FromMinutes(3) + TimeSpan.FromSeconds(12),
            SortOrder = 1
        });
        intro.Tracks.Add(new Track
        {
            PlaylistId = intro.Id,
            DisplayTitle = "intro_pad.wav",
            FilePath = @"D:\Music\intro_pad.wav",
            Duration = TimeSpan.FromMinutes(5),
            SortOrder = 2
        });

        var gameplay = new Playlist { Name = "Gameplay", SortOrder = 2 };
        gameplay.Tracks.Add(new Track
        {
            PlaylistId = gameplay.Id,
            DisplayTitle = "gameplay_energy.mp3",
            FilePath = @"D:\Music\gameplay_energy.mp3",
            Duration = TimeSpan.FromMinutes(4) + TimeSpan.FromSeconds(18),
            SortOrder = 1
        });

        return [intro, gameplay];
    }

    private void AddPlaylist()
    {
        var playlist = new Playlist
        {
            Name = $"Playlist {Playlists.Count + 1}",
            SortOrder = Playlists.Count + 1
        };

        Playlists.Add(playlist);
        SelectedPlaylist = playlist;
        SavePlaylists();
        OnPropertyChanged(nameof(SelectedPlaylistTracksCountText));
    }

    private void RemoveSelectedPlaylist()
    {
        if (SelectedPlaylist is null)
        {
            return;
        }

        var playlistIndex = Playlists.IndexOf(SelectedPlaylist);
        Playlists.Remove(SelectedPlaylist);
        SelectedPlaylist = Playlists.Count == 0 ? null : Playlists[Math.Clamp(playlistIndex, 0, Playlists.Count - 1)];
        SavePlaylists();
        OnPropertyChanged(nameof(SelectedPlaylistTracksCountText));
    }

    private void AddTracks()
    {
        if (SelectedPlaylist is null)
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = LocalizationService.T("AddAudioFilesTitle"),
            Filter = LocalizationService.T("AudioFilesFilter"),
            Multiselect = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        AddTrackFiles(dialog.FileNames);
    }

    private void AddFolder()
    {
        if (SelectedPlaylist is null)
        {
            return;
        }

        var dialog = new OpenFolderDialog
        {
            Title = LocalizationService.T("AddAudioFolderTitle"),
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var files = Directory
            .EnumerateFiles(dialog.FolderName, "*.*", SearchOption.AllDirectories)
            .Where(IsSupportedAudioFile)
            .OrderBy(filePath => filePath)
            .ToList();

        AddTrackFiles(files);
        PlayerMessage = files.Count == 0
            ? LocalizationService.T("NoSupportedAudioFiles")
            : LocalizationService.F("AddedAudioFilesFormat", files.Count);
    }

    private void RemoveSelectedTrack()
    {
        if (SelectedPlaylist is null || SelectedTrack is null)
        {
            return;
        }

        var trackIndex = SelectedPlaylist.Tracks.IndexOf(SelectedTrack);
        var wasCurrentTrack = CurrentTrack == SelectedTrack;
        SelectedPlaylist.Tracks.Remove(SelectedTrack);

        for (var index = 0; index < SelectedPlaylist.Tracks.Count; index++)
        {
            SelectedPlaylist.Tracks[index].SortOrder = index + 1;
        }

        SelectedTrack = SelectedPlaylist.Tracks.Count == 0
            ? null
            : SelectedPlaylist.Tracks[Math.Clamp(trackIndex, 0, SelectedPlaylist.Tracks.Count - 1)];

        if (wasCurrentTrack)
        {
            _ = StopAsync();
        }

        RaisePlaybackCommandStates();
        SavePlaylists();
        OnPropertyChanged(nameof(SelectedPlaylistTracksCountText));
    }

    private void ClearSelectedPlaylist()
    {
        if (SelectedPlaylist is null || SelectedPlaylist.Tracks.Count == 0)
        {
            return;
        }

        var result = System.Windows.MessageBox.Show(
            LocalizationService.F("ClearPlaylistQuestionFormat", SelectedPlaylist.Name),
            LocalizationService.T("ClearPlaylistTitle"),
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        if (result != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        var wasPlayingThisPlaylist = CurrentTrack is not null && SelectedPlaylist.Tracks.Contains(CurrentTrack);
        SelectedPlaylist.Tracks.Clear();
        SelectedTrack = null;
        if (wasPlayingThisPlaylist)
        {
            _ = StopAsync();
        }

        SavePlaylists();
        RaisePlaybackCommandStates();
        OnPropertyChanged(nameof(SelectedPlaylistTracksCountText));
        PlayerMessage = LocalizationService.F("ClearedPlaylistFormat", SelectedPlaylist.Name);
    }

    private bool CanMoveSelectedPlaylist(int offset)
    {
        if (SelectedPlaylist is null)
        {
            return false;
        }

        var index = Playlists.IndexOf(SelectedPlaylist);
        var newIndex = index + offset;
        return index >= 0 && newIndex >= 0 && newIndex < Playlists.Count;
    }

    private void MoveSelectedPlaylist(int offset)
    {
        if (!CanMoveSelectedPlaylist(offset) || SelectedPlaylist is null)
        {
            return;
        }

        var oldIndex = Playlists.IndexOf(SelectedPlaylist);
        var newIndex = oldIndex + offset;
        Playlists.Move(oldIndex, newIndex);

        for (var index = 0; index < Playlists.Count; index++)
        {
            Playlists[index].SortOrder = index + 1;
        }

        SavePlaylists();
        RaisePlaybackCommandStates();
    }

    public async Task PlaySelectedTrackAsync()
    {
        if (SelectedTrack is null)
        {
            return;
        }

        await PlayTrackAsync(SelectedTrack, CrossfadeSeconds);
    }

    private async Task TogglePlayPauseAsync()
    {
        if (PlaybackState == PlaybackState.Playing)
        {
            audioPlaybackService.Pause();
            PlaybackState = PlaybackState.Paused;
            return;
        }

        if (PlaybackState == PlaybackState.Paused && CurrentTrack is not null)
        {
            audioPlaybackService.Resume();
            PlaybackState = PlaybackState.Playing;
            return;
        }

        var trackToPlay = SelectedTrack ?? CurrentTrack;
        if (trackToPlay is not null)
        {
            await PlayTrackAsync(trackToPlay, 0);
        }
    }

    private async Task StopAsync()
    {
        await audioPlaybackService.StopAsync(1);
        PlaybackState = PlaybackState.Stopped;
        CurrentTrack = null;
        PositionSeconds = 0;
            PlayerMessage = LocalizationService.T("Stopped");
    }

    private async Task SelectOffsetTrackAsync(int offset)
    {
        if (SelectedPlaylist is null || SelectedPlaylist.Tracks.Count == 0)
        {
            return;
        }

        var sourceTrack = SelectedTrack ?? CurrentTrack ?? SelectedPlaylist.Tracks[0];
        var currentIndex = SelectedPlaylist.Tracks.IndexOf(sourceTrack);
        var nextIndex = currentIndex < 0 ? 0 : (currentIndex + offset + SelectedPlaylist.Tracks.Count) % SelectedPlaylist.Tracks.Count;

        SelectedTrack = SelectedPlaylist.Tracks[nextIndex];
        await PlayTrackAsync(SelectedTrack, CrossfadeSeconds);
    }

    private static void ShowPlaceholder(string screenName)
    {
        System.Windows.MessageBox.Show(
            $"{screenName} will be implemented in the next stages.",
            LocalizationService.T("AppTitle"),
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }

    private void OpenObsSettings()
    {
        var window = new ObsSettingsWindow
        {
            DataContext = new ObsSettingsViewModel(obsSettingsService, obsClientService),
            Owner = App.Current.MainWindow
        };
        window.ShowDialog();
    }

    private async Task ConnectObsAsync()
    {
        var settings = obsSettingsService.Load();
        PlayerMessage = LocalizationService.T("ConnectingObs");
        var info = await obsClientService.ConnectAsync(settings);
        ObsStatus = info.Connected ? LocalizationService.T("Connected") : LocalizationService.T("Disconnected");
        ActiveScene = string.IsNullOrWhiteSpace(info.CurrentScene) ? LocalizationService.T("None") : info.CurrentScene;
        PlayerMessage = info.StatusMessage;
        RaiseObsCommandStates();
    }

    private void OpenEventRules()
    {
        try
        {
            var scenes = obsClientService.IsConnected ? obsClientService.GetScenes() : [];
            var window = new EventRulesWindow
            {
                DataContext = new EventRulesViewModel(new EventRuleRepository(AppDataPaths.DatabasePath), Playlists, scenes),
                Owner = App.Current.MainWindow
            };
            window.ShowDialog();
        }
        catch (Exception exception)
        {
            PlayerMessage = LocalizationService.F("EventRulesErrorFormat", exception.Message);
            System.Windows.MessageBox.Show(
                LocalizationService.F("EventRulesOpenFailedFormat", exception.Message),
                LocalizationService.T("EventRulesErrorTitle"),
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    private void OpenPlaylistRules()
    {
        if (SelectedPlaylist is null)
        {
            return;
        }

        var window = new PlaylistRulesWindow
        {
            DataContext = new PlaylistRulesViewModel(SelectedPlaylist, Playlists, SavePlaylists),
            Owner = App.Current.MainWindow
        };
        window.ShowDialog();
        CrossfadeSeconds = SelectedPlaylist.DefaultCrossfadeSeconds;
        Volume = SelectedPlaylist.DefaultVolume;
    }

    private void OpenAppSettings()
    {
        var window = new AppSettingsWindow
        {
            DataContext = new AppSettingsViewModel(
                applicationSettingsService,
                audioOutputDeviceService,
                audioPlaybackService,
                applicationDataResetService),
            Owner = App.Current.MainWindow
        };
        window.ShowDialog();
    }

    private void ApplyApplicationAudioSettings()
    {
        var settings = applicationSettingsService.Load();
        var device = audioOutputDeviceService.ResolveDevice(settings.AudioOutputDeviceId);
        audioPlaybackService.SetOutputDevice(device.DeviceNumber);
    }

    private void RaisePlaybackCommandStates()
    {
        ((RelayCommand)RemovePlaylistCommand).RaiseCanExecuteChanged();
        ((RelayCommand)AddTracksCommand).RaiseCanExecuteChanged();
        ((RelayCommand)AddFolderCommand).RaiseCanExecuteChanged();
        ((RelayCommand)RemoveTrackCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ClearPlaylistCommand).RaiseCanExecuteChanged();
        ((RelayCommand)OpenPlaylistRulesCommand).RaiseCanExecuteChanged();
        ((RelayCommand)MovePlaylistLeftCommand).RaiseCanExecuteChanged();
        ((RelayCommand)MovePlaylistRightCommand).RaiseCanExecuteChanged();
        ((RelayCommand)PlaySelectedTrackCommand).RaiseCanExecuteChanged();
        ((RelayCommand)PlayPauseCommand).RaiseCanExecuteChanged();
        ((RelayCommand)StopCommand).RaiseCanExecuteChanged();
        ((RelayCommand)PreviousCommand).RaiseCanExecuteChanged();
        ((RelayCommand)NextCommand).RaiseCanExecuteChanged();
        RaiseObsCommandStates();
    }

    private void RaiseObsCommandStates()
    {
        ((RelayCommand)ConnectObsCommand).RaiseCanExecuteChanged();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        ObsStatus = obsClientService.IsConnected
            ? LocalizationService.T("Connected")
            : LocalizationService.T("Disconnected");
        if (string.IsNullOrWhiteSpace(ActiveScene) || ActiveScene is "None" or "Нет" or "Немає" or "Brak")
        {
            ActiveScene = LocalizationService.T("None");
        }

        if (CurrentTrack is null && PlaybackState == PlaybackState.Stopped)
        {
            PlayerMessage = LocalizationService.T("Ready");
        }

        OnPropertyChanged(nameof(CurrentTrackTitle));
        OnPropertyChanged(nameof(ObsStatusText));
        OnPropertyChanged(nameof(ActiveSceneText));
        OnPropertyChanged(nameof(CurrentPlaylistText));
        OnPropertyChanged(nameof(PlaybackStateText));
        OnPropertyChanged(nameof(SelectedPlaylistTracksCountText));
        OnPropertyChanged(nameof(StatusLine));
    }

    private void SavePlaylists()
    {
        playlistRepository.SaveAll(Playlists);
    }

    private void AddTrackFiles(IEnumerable<string> filePaths)
    {
        if (SelectedPlaylist is null)
        {
            return;
        }

        var added = 0;
        foreach (var filePath in filePaths.Where(IsSupportedAudioFile))
        {
            if (SelectedPlaylist.Tracks.Any(track => string.Equals(track.FilePath, filePath, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var nextOrder = SelectedPlaylist.Tracks.Count + 1;
            SelectedPlaylist.Tracks.Add(new Track
            {
                PlaylistId = SelectedPlaylist.Id,
                FilePath = filePath,
                DisplayTitle = Path.GetFileName(filePath),
                Duration = AudioMetadataReader.TryReadDuration(filePath),
                SortOrder = nextOrder,
                LastKnownExists = File.Exists(filePath)
            });
            added++;
        }

        SelectedTrack ??= SelectedPlaylist.Tracks.FirstOrDefault();
        SavePlaylists();
        RaisePlaybackCommandStates();
        if (added > 0)
        {
            OnPropertyChanged(nameof(SelectedPlaylistTracksCountText));
            PlayerMessage = LocalizationService.F("AddedAudioFilesFormat", added);
        }
    }

    private static bool IsSupportedAudioFile(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".wav", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".flac", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".ogg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".m4a", StringComparison.OrdinalIgnoreCase);
    }

    private async Task PlayTrackAsync(Track track, double crossfade)
    {
        if (!File.Exists(track.FilePath))
        {
            track.LastKnownExists = false;
            PlayerMessage = LocalizationService.F("MissingFileFormat", track.DisplayTitle);
            SavePlaylists();
            return;
        }

        try
        {
            track.LastKnownExists = true;
            CurrentTrack = track;
            PlaybackState = crossfade > 0 && audioPlaybackService.CurrentTrack is not null
                ? PlaybackState.Switching
                : PlaybackState.Playing;
            PlayerMessage = LocalizationService.F("PlayingTrackFormat", track.DisplayTitle);
            await audioPlaybackService.PlayAsync(track, VolumeToFloat(Volume), fadeInSeconds: 0.5, crossfadeSeconds: crossfade);
            PlaybackState = audioPlaybackService.State;
            PositionSeconds = audioPlaybackService.PositionSeconds;
            automaticTransitionPending = false;
            OnPropertyChanged(nameof(PositionText));
            SavePlaylists();
        }
        catch (Exception exception)
        {
            PlaybackState = PlaybackState.Stopped;
            PlayerMessage = exception.Message;
            System.Windows.MessageBox.Show(
                exception.Message,
                LocalizationService.T("PlaybackErrorTitle"),
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
    }

    private async Task PlayPlaylistByNameAsync(string playlistName, double crossfadeSeconds)
    {
        var playlist = Playlists.FirstOrDefault(item => string.Equals(item.Name, playlistName, StringComparison.OrdinalIgnoreCase));
        if (playlist is null)
        {
            PlayerMessage = LocalizationService.F("RulePlaylistNotFoundFormat", playlistName);
            return;
        }

        SelectedPlaylist = playlist;
        var track = playlist.Tracks.FirstOrDefault(item => item.Enabled && File.Exists(item.FilePath));
        if (track is null)
        {
            PlayerMessage = LocalizationService.F("PlaylistNoPlayableTracksFormat", playlist.Name);
            return;
        }

        SelectedTrack = track;
        var transitionSeconds = crossfadeSeconds > 0 ? crossfadeSeconds : playlist.DefaultCrossfadeSeconds;
        await PlayTrackAsync(track, transitionSeconds);
    }

    private void SyncPositionFromAudio()
    {
        if (CurrentTrack is null || PlaybackState is PlaybackState.Stopped)
        {
            return;
        }

        positionSeconds = audioPlaybackService.PositionSeconds;
        OnPropertyChanged(nameof(PositionSeconds));
        OnPropertyChanged(nameof(PositionText));
        PlaybackState = audioPlaybackService.State;
        TryStartAutomaticCrossfadeBeforeEnd();
    }

    private void TryStartAutomaticCrossfadeBeforeEnd()
    {
        if (automaticTransitionPending || CurrentTrack is null || PlaybackState != PlaybackState.Playing)
        {
            return;
        }

        var playlist = Playlists.FirstOrDefault(item => item.Tracks.Contains(CurrentTrack));
        if (playlist is null || playlist.DefaultCrossfadeSeconds <= 0)
        {
            return;
        }

        var durationSeconds = audioPlaybackService.DurationSeconds;
        if (durationSeconds <= 0)
        {
            return;
        }

        var remainingSeconds = durationSeconds - audioPlaybackService.PositionSeconds;
        var triggerSeconds = Math.Min(playlist.DefaultCrossfadeSeconds, Math.Max(0.25, durationSeconds / 2));
        if (remainingSeconds > triggerSeconds)
        {
            return;
        }

        var nextPlayback = ResolveNextPlaybackAfterEnd(playlist, CurrentTrack);
        if (nextPlayback is null)
        {
            return;
        }

        automaticTransitionPending = true;
        _ = PlayResolvedPlaybackAsync(nextPlayback.Value.Playlist, nextPlayback.Value.Track);
    }

    private void OnPlaybackEnded(object? sender, EventArgs eventArgs)
    {
        App.Current.Dispatcher.Invoke(async () => await HandlePlaybackEndedAsync());
    }

    private async Task HandlePlaybackEndedAsync()
    {
        if (CurrentTrack is null)
        {
            await StopAsync();
            return;
        }

        var playlist = Playlists.FirstOrDefault(item => item.Tracks.Contains(CurrentTrack));
        if (playlist is null)
        {
            await StopAsync();
            return;
        }

        var nextTrack = ResolveNextTrackAfterEnd(playlist, CurrentTrack);
        if (nextTrack is not null)
        {
            await PlayResolvedPlaybackAsync(playlist, nextTrack);
            return;
        }

        await HandlePlaylistCompletedAsync(playlist);
    }

    private (Playlist Playlist, Track Track)? ResolveNextPlaybackAfterEnd(Playlist playlist, Track currentTrack)
    {
        var nextTrack = ResolveNextTrackAfterEnd(playlist, currentTrack);
        if (nextTrack is not null)
        {
            return (playlist, nextTrack);
        }

        if (playlist.PlayMode == PlayMode.StopAfterPlaylist)
        {
            return null;
        }

        Playlist? targetPlaylist = playlist.CompletionAction switch
        {
            PlaylistCompletionAction.PlayNextPlaylist => GetAdjacentPlaylist(playlist, 1),
            PlaylistCompletionAction.PlaySpecificPlaylist => Playlists.FirstOrDefault(item => item.Id == playlist.CompletionPlaylistId),
            _ => null
        };

        var targetTrack = targetPlaylist?.Tracks
            .Where(item => item.Enabled && File.Exists(item.FilePath))
            .OrderBy(item => item.SortOrder)
            .FirstOrDefault();

        return targetPlaylist is not null && targetTrack is not null
            ? (targetPlaylist, targetTrack)
            : null;
    }

    private async Task PlayResolvedPlaybackAsync(Playlist playlist, Track track)
    {
        SelectedPlaylist = playlist;
        SelectedTrack = track;
        Volume = playlist.DefaultVolume;
        CrossfadeSeconds = playlist.DefaultCrossfadeSeconds;
        await PlayTrackAsync(track, playlist.DefaultCrossfadeSeconds);
    }

    private Track? ResolveNextTrackAfterEnd(Playlist playlist, Track currentTrack)
    {
        var playableTracks = playlist.Tracks
            .Where(item => item.Enabled && File.Exists(item.FilePath))
            .OrderBy(item => item.SortOrder)
            .ToList();
        if (playableTracks.Count == 0)
        {
            return null;
        }

        if (playlist.PlayMode == PlayMode.RepeatOne)
        {
            return currentTrack.Enabled && File.Exists(currentTrack.FilePath) ? currentTrack : playableTracks[0];
        }

        if (playlist.PlayMode == PlayMode.RepeatAll && playlist.ShuffleEnabled)
        {
            var candidates = playableTracks.Count > 1
                ? playableTracks.Where(item => item != currentTrack).ToList()
                : playableTracks;
            return candidates[random.Next(candidates.Count)];
        }

        var currentIndex = playableTracks.IndexOf(currentTrack);
        if (currentIndex >= 0 && currentIndex < playableTracks.Count - 1)
        {
            return playableTracks[currentIndex + 1];
        }

        if (playlist.PlayMode == PlayMode.RepeatAll)
        {
            return playableTracks[0];
        }

        return null;
    }

    private async Task HandlePlaylistCompletedAsync(Playlist playlist)
    {
        if (playlist.PlayMode == PlayMode.StopAfterPlaylist)
        {
            await StopAsync();
            PlayerMessage = LocalizationService.F("PlaylistFinishedFormat", playlist.Name);
            return;
        }

        switch (playlist.CompletionAction)
        {
            case PlaylistCompletionAction.PlayNextPlaylist:
                await PlayAdjacentPlaylistAsync(playlist, 1);
                break;
            case PlaylistCompletionAction.PlaySpecificPlaylist:
                var targetPlaylist = Playlists.FirstOrDefault(item => item.Id == playlist.CompletionPlaylistId);
                if (targetPlaylist is null)
                {
                    await StopAsync();
                    PlayerMessage = LocalizationService.T("TargetPlaylistNotFound");
                    return;
                }

                await PlayPlaylistAsync(targetPlaylist);
                break;
            default:
                await StopAsync();
            PlayerMessage = LocalizationService.F("PlaylistFinishedFormat", playlist.Name);
                break;
        }
    }

    private async Task PlayAdjacentPlaylistAsync(Playlist playlist, int offset)
    {
        var targetPlaylist = GetAdjacentPlaylist(playlist, offset);
        if (targetPlaylist is null)
        {
            await StopAsync();
            PlayerMessage = LocalizationService.T("NoNextPlaylist");
            return;
        }

        await PlayPlaylistAsync(targetPlaylist);
    }

    private Playlist? GetAdjacentPlaylist(Playlist playlist, int offset)
    {
        var index = Playlists.IndexOf(playlist);
        var targetIndex = index + offset;
        return index < 0 || targetIndex < 0 || targetIndex >= Playlists.Count
            ? null
            : Playlists[targetIndex];
    }

    private async Task PlayPlaylistAsync(Playlist playlist)
    {
        var track = playlist.Tracks
            .Where(item => item.Enabled && File.Exists(item.FilePath))
            .OrderBy(item => item.SortOrder)
            .FirstOrDefault();
        if (track is null)
        {
            await StopAsync();
            PlayerMessage = LocalizationService.F("PlaylistNoPlayableTracksFormat", playlist.Name);
            return;
        }

        SelectedPlaylist = playlist;
        SelectedTrack = track;
        Volume = playlist.DefaultVolume;
        CrossfadeSeconds = playlist.DefaultCrossfadeSeconds;
        await PlayTrackAsync(track, playlist.DefaultCrossfadeSeconds);
    }

    private async Task ConnectToObsOnStartupAsync(ObsConnectionSettings settings)
    {
        var info = await obsClientService.ConnectAsync(settings);
        App.Current.Dispatcher.Invoke(() =>
        {
            ObsStatus = info.Connected ? LocalizationService.T("Connected") : LocalizationService.T("Disconnected");
            ActiveScene = string.IsNullOrWhiteSpace(info.CurrentScene) ? LocalizationService.T("None") : info.CurrentScene;
            PlayerMessage = info.StatusMessage;
        });
    }

    private void OnObsConnected(object? sender, EventArgs e)
    {
        App.Current.Dispatcher.Invoke(() =>
        {
            ObsStatus = LocalizationService.T("Connected");
            ActiveScene = obsClientService.GetCurrentScene();
            PlayerMessage = LocalizationService.T("ObsConnected");
            RaiseObsCommandStates();
        });
    }

    private void OnObsDisconnected(object? sender, EventArgs e)
    {
        App.Current.Dispatcher.Invoke(() =>
        {
            ObsStatus = LocalizationService.T("Disconnected");
            ActiveScene = LocalizationService.T("None");
            PlayerMessage = LocalizationService.T("ObsDisconnected");
            RaiseObsCommandStates();
        });
    }

    private void OnObsSceneChanged(object? sender, ObsSceneChangedEventArgs e)
    {
        App.Current.Dispatcher.Invoke(async () =>
        {
            ActiveScene = e.SceneName;
        });
    }

    private void OnObsEventTriggered(object? sender, ObsEventTriggeredEventArgs e)
    {
        App.Current.Dispatcher.Invoke(async () =>
        {
            if (e.TriggerType == "SceneChanged")
            {
                ActiveScene = e.Condition;
            }

            await ExecuteObsRuleAsync(e.TriggerType, e.Condition);
        });
    }

    private async Task ExecuteObsRuleAsync(string triggerType, string condition)
    {
        var rules = eventRuleRepository.LoadAll();
        var rule = eventRulesEngine.FindRule(rules, triggerType, condition);
        if (rule is null)
        {
            PlayerMessage = string.IsNullOrWhiteSpace(condition)
                ? LocalizationService.F("ObsEventFormat", triggerType)
                : LocalizationService.F("ObsEventConditionFormat", triggerType, condition);
            return;
        }

        if (rule.DelaySeconds > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(rule.DelaySeconds));
        }

        var actionParameters = eventRulesEngine.ReadActionParameters(rule);
        switch (rule.ActionType)
        {
            case "PlayPlaylist":
                await PlayPlaylistByNameAsync(actionParameters.PlaylistName, actionParameters.CrossfadeSeconds);
                PlayerMessage = LocalizationService.F("RuleExecutedFormat", rule.Name);
                break;
            case "Stop":
                await StopAsync();
                PlayerMessage = LocalizationService.F("RuleExecutedFormat", rule.Name);
                break;
            case "Pause":
                audioPlaybackService.Pause();
                PlaybackState = PlaybackState.Paused;
                PlayerMessage = LocalizationService.F("RuleExecutedFormat", rule.Name);
                break;
            case "Resume":
                audioPlaybackService.Resume();
                PlaybackState = PlaybackState.Playing;
                PlayerMessage = LocalizationService.F("RuleExecutedFormat", rule.Name);
                break;
            case "SetVolume":
                Volume = Math.Clamp(actionParameters.Volume, 0, 100);
                PlayerMessage = LocalizationService.F("RuleExecutedFormat", rule.Name);
                break;
            default:
                PlayerMessage = LocalizationService.F("UnsupportedRuleActionFormat", rule.ActionType);
                break;
        }
    }

    private static float VolumeToFloat(int value)
    {
        return Math.Clamp(value, 0, 100) / 100f;
    }

    private static string LocalizePlaybackState(PlaybackState state)
    {
        return state switch
        {
            PlaybackState.Stopped => LocalizationService.T("Stopped"),
            _ => state.ToString()
        };
    }

    public void Dispose()
    {
        positionTimer.Stop();
        LocalizationService.LanguageChanged -= OnLanguageChanged;
        audioPlaybackService.PlaybackEnded -= OnPlaybackEnded;
        obsClientService.Connected -= OnObsConnected;
        obsClientService.Disconnected -= OnObsDisconnected;
        obsClientService.CurrentProgramSceneChanged -= OnObsSceneChanged;
        obsClientService.ObsEventTriggered -= OnObsEventTriggered;
        audioPlaybackService.Dispose();
        obsClientService.Dispose();
    }
}
