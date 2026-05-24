using System.Globalization;
using System.Text.Json;
using System.Windows.Data;
using StreamMusicPlayer.Rules;
using StreamMusicPlayer.Services;

namespace StreamMusicPlayer.Views;

public sealed class AutomationEventDisplayNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is string eventType
            ? AutomationDisplayNameService.GetEventDisplayName(eventType)
            : string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

public sealed class AutomationActionDisplayNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is string actionType
            ? AutomationDisplayNameService.GetActionDisplayName(actionType)
            : string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

public sealed class AutomationEventConditionConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string settingsJson || string.IsNullOrWhiteSpace(settingsJson))
        {
            return LocalizationService.T("Any");
        }

        try
        {
            var settings = JsonSerializer.Deserialize<AutomationEventSettings>(
                settingsJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new AutomationEventSettings();
            if (!string.IsNullOrWhiteSpace(settings.SceneName)
                && string.IsNullOrWhiteSpace(settings.SourceName)
                && string.IsNullOrWhiteSpace(settings.FilterName))
            {
                return settings.SceneName;
            }

            if (!string.IsNullOrWhiteSpace(settings.FilterName))
            {
                var sourceName = string.IsNullOrWhiteSpace(settings.SourceName) ? LocalizationService.T("Any") : settings.SourceName;
                var filterName = string.IsNullOrWhiteSpace(settings.FilterName) ? LocalizationService.T("Any") : settings.FilterName;
                return $"{sourceName} / {filterName}";
            }

            if (!string.IsNullOrWhiteSpace(settings.SceneName) || !string.IsNullOrWhiteSpace(settings.SourceName))
            {
                var sceneName = string.IsNullOrWhiteSpace(settings.SceneName) ? LocalizationService.T("Any") : settings.SceneName;
                var sourceName = string.IsNullOrWhiteSpace(settings.SourceName) ? LocalizationService.T("Any") : settings.SourceName;
                return $"{sceneName} / {sourceName}";
            }

            if (!string.IsNullOrWhiteSpace(settings.PlaylistName) || !string.IsNullOrWhiteSpace(settings.TrackName))
            {
                var playlistName = string.IsNullOrWhiteSpace(settings.PlaylistName) ? LocalizationService.T("Any") : settings.PlaylistName;
                var trackName = string.IsNullOrWhiteSpace(settings.TrackName) ? LocalizationService.T("Any") : settings.TrackName;
                return string.IsNullOrWhiteSpace(settings.TrackName) ? playlistName : $"{playlistName} / {trackName}";
            }
        }
        catch
        {
            return LocalizationService.T("Any");
        }

        return LocalizationService.T("Any");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
