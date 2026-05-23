using System.Text.Json;
using StreamMusicPlayer.Models;
using StreamMusicPlayer.Repositories;

namespace StreamMusicPlayer.Services;

public sealed class ApplicationSettingsService
{
    private const string SettingsKey = "ApplicationSettings";
    private readonly AppSettingsRepository appSettingsRepository;

    public ApplicationSettingsService(AppSettingsRepository appSettingsRepository)
    {
        this.appSettingsRepository = appSettingsRepository;
    }

    public ApplicationSettings Load()
    {
        var json = appSettingsRepository.Get(SettingsKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ApplicationSettings();
        }

        try
        {
            return JsonSerializer.Deserialize<ApplicationSettings>(json) ?? new ApplicationSettings();
        }
        catch
        {
            return new ApplicationSettings();
        }
    }

    public void Save(ApplicationSettings settings)
    {
        appSettingsRepository.Set(SettingsKey, JsonSerializer.Serialize(settings));
    }
}
