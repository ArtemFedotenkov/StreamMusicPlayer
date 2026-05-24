using System.Collections.ObjectModel;
using System.Windows.Input;
using StreamMusicPlayer.Models;
using StreamMusicPlayer.Services;

namespace StreamMusicPlayer.ViewModels;

public sealed class PlaylistRulesViewModel : ObservableObject
{
    private readonly Action saveAction;
    private PlaylistCompletionAction completionAction;
    private Playlist? completionPlaylist;
    private PlayMode playMode;
    private bool shuffleEnabled;
    private string statusMessage = LocalizationService.T("Ready");

    public PlaylistRulesViewModel(Playlist playlist, IEnumerable<Playlist> playlists, Action saveAction)
    {
        Playlist = playlist;
        this.saveAction = saveAction;
        Playlists = new ObservableCollection<Playlist>(playlists);
        PlayModes = new ObservableCollection<LocalizedValueOption<PlayMode>>(
        [
            new(PlayMode.Sequential, "PlayModeSequential"),
            new(PlayMode.RepeatOne, "PlayModeRepeatOne"),
            new(PlayMode.RepeatAll, "PlayModeRepeatAll")
        ]);
        CompletionActions = new ObservableCollection<LocalizedValueOption<PlaylistCompletionAction>>(
        [
            new(PlaylistCompletionAction.Stop, "CompletionStop"),
            new(PlaylistCompletionAction.PlayPreviousPlaylist, "CompletionPlayPreviousPlaylist"),
            new(PlaylistCompletionAction.PlayNextPlaylist, "CompletionPlayNextPlaylist"),
            new(PlaylistCompletionAction.PlaySpecificPlaylist, "CompletionPlaySpecificPlaylist")
        ]);

        playMode = playlist.PlayMode == PlayMode.StopAfterPlaylist ? PlayMode.Sequential : playlist.PlayMode;
        shuffleEnabled = playlist.ShuffleEnabled;
        completionAction = playlist.PlayMode == PlayMode.StopAfterPlaylist
            ? PlaylistCompletionAction.Stop
            : playlist.CompletionAction;
        completionPlaylist = Playlists.FirstOrDefault(item => item.Id == playlist.CompletionPlaylistId);

        SaveCommand = new RelayCommand(_ => Save());
        LocalizationService.LanguageChanged += OnLanguageChanged;
    }

    public Playlist Playlist { get; }
    public ObservableCollection<Playlist> Playlists { get; }
    public ObservableCollection<LocalizedValueOption<PlayMode>> PlayModes { get; }
    public ObservableCollection<LocalizedValueOption<PlaylistCompletionAction>> CompletionActions { get; }

    public PlayMode PlayMode
    {
        get => playMode;
        set
        {
            if (SetProperty(ref playMode, value))
            {
                OnPropertyChanged(nameof(CanShuffle));
                OnPropertyChanged(nameof(ShowShuffle));
                OnPropertyChanged(nameof(ShowCompletionSettings));
                OnPropertyChanged(nameof(ShowSpecificPlaylist));
                if (!CanShuffle)
                {
                    ShuffleEnabled = false;
                }
            }
        }
    }

    public bool ShuffleEnabled
    {
        get => shuffleEnabled;
        set => SetProperty(ref shuffleEnabled, value);
    }

    public bool CanShuffle => PlayMode == PlayMode.RepeatAll;
    public bool ShowShuffle => PlayMode == PlayMode.RepeatAll;
    public bool ShowCompletionSettings => PlayMode == PlayMode.Sequential;

    public PlaylistCompletionAction CompletionAction
    {
        get => completionAction;
        set
        {
            if (SetProperty(ref completionAction, value))
            {
                OnPropertyChanged(nameof(NeedsSpecificPlaylist));
                OnPropertyChanged(nameof(ShowSpecificPlaylist));
            }
        }
    }

    public bool NeedsSpecificPlaylist => CompletionAction == PlaylistCompletionAction.PlaySpecificPlaylist;
    public bool ShowSpecificPlaylist => ShowCompletionSettings && NeedsSpecificPlaylist;

    public Playlist? CompletionPlaylist
    {
        get => completionPlaylist;
        set => SetProperty(ref completionPlaylist, value);
    }

    public string StatusMessage
    {
        get => statusMessage;
        private set => SetProperty(ref statusMessage, value);
    }

    public ICommand SaveCommand { get; }

    private void Save()
    {
        Playlist.PlayMode = PlayMode;
        Playlist.ShuffleEnabled = ShuffleEnabled && CanShuffle;
        Playlist.RepeatEnabled = PlayMode is PlayMode.RepeatOne or PlayMode.RepeatAll;
        Playlist.CompletionAction = ShowCompletionSettings ? CompletionAction : PlaylistCompletionAction.Stop;
        Playlist.CompletionPlaylistId = ShowSpecificPlaylist ? CompletionPlaylist?.Id ?? string.Empty : string.Empty;
        saveAction();
        StatusMessage = LocalizationService.T("PlaylistRulesSaved");
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        foreach (var playMode in PlayModes)
        {
            playMode.Refresh();
        }

        foreach (var completionAction in CompletionActions)
        {
            completionAction.Refresh();
        }

        if (StatusMessage is "Ready" or "Готово" or "Gotowe")
        {
            StatusMessage = LocalizationService.T("Ready");
        }
    }
}
