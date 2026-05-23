using System.Text.Json;
using StreamMusicPlayer.Models;
using StreamMusicPlayer.Repositories;

namespace StreamMusicPlayer.Services;

public sealed class ObsSettingsService
{
    private const string SettingsKey = "obs.connection";
    private readonly AppSettingsRepository appSettingsRepository;

    public ObsSettingsService(AppSettingsRepository appSettingsRepository)
    {
        this.appSettingsRepository = appSettingsRepository;
    }

    public ObsConnectionSettings Load()
    {
        var json = appSettingsRepository.Get(SettingsKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ObsConnectionSettings();
        }

        try
        {
            return JsonSerializer.Deserialize<ObsConnectionSettings>(json) ?? new ObsConnectionSettings();
        }
        catch
        {
            return new ObsConnectionSettings();
        }
    }

    public void Save(ObsConnectionSettings settings)
    {
        appSettingsRepository.Set(SettingsKey, JsonSerializer.Serialize(settings));
    }
}
