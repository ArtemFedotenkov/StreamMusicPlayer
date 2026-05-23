using StreamMusicPlayer.Services;

namespace StreamMusicPlayer.ViewModels;

public sealed class LocalizedValueOption<T> : ObservableObject
{
    public LocalizedValueOption(T value, string key)
    {
        Value = value;
        Key = key;
    }

    public T Value { get; }

    public string Key { get; }

    public string DisplayName => LocalizationService.T(Key);

    public void Refresh()
    {
        OnPropertyChanged(nameof(DisplayName));
    }

    public override string ToString()
    {
        return DisplayName;
    }
}
