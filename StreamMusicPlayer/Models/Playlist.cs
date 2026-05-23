using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace StreamMusicPlayer.Models;

public sealed class Playlist : INotifyPropertyChanged
{
    private string name = "New Playlist";

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public string Name
    {
        get => name;
        set
        {
            if (name == value)
            {
                return;
            }

            name = value;
            OnPropertyChanged();
        }
    }

    public int SortOrder { get; set; }
    public PlayMode PlayMode { get; set; } = PlayMode.Sequential;
    public int DefaultVolume { get; set; } = 100;
    public double DefaultCrossfadeSeconds { get; set; } = 2;
    public bool RepeatEnabled { get; set; } = true;
    public bool ShuffleEnabled { get; set; }
    public PlaylistCompletionAction CompletionAction { get; set; } = PlaylistCompletionAction.Stop;
    public string CompletionPlaylistId { get; set; } = string.Empty;
    public ObservableCollection<Track> Tracks { get; } = [];

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public override string ToString()
    {
        return Name;
    }
}
