using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using StreamMusicPlayer.Audio;
using StreamMusicPlayer.Models;
using StreamMusicPlayer.Services;

namespace StreamMusicPlayer.ViewModels;

public sealed class AppSettingsViewModel : ObservableObject
{
    private readonly ApplicationSettingsService applicationSettingsService;
    private readonly AudioOutputDeviceService audioOutputDeviceService;
    private readonly AudioPlaybackService audioPlaybackService;
    private readonly ApplicationDataResetService applicationDataResetService;
    private ThemeOption selectedTheme;
    private LanguageOption selectedLanguage;
    private AudioOutputDevice selectedAudioDevice;
    private string statusMessage = LocalizationService.T("Ready");

    public AppSettingsViewModel(
        ApplicationSettingsService applicationSettingsService,
        AudioOutputDeviceService audioOutputDeviceService,
        AudioPlaybackService audioPlaybackService,
        ApplicationDataResetService applicationDataResetService)
    {
        this.applicationSettingsService = applicationSettingsService;
        this.audioOutputDeviceService = audioOutputDeviceService;
        this.audioPlaybackService = audioPlaybackService;
        this.applicationDataResetService = applicationDataResetService;

        Themes =
        [
            new ThemeOption(AppTheme.Light, "ThemeStandardLight"),
            new ThemeOption(AppTheme.Dark, "ThemeStandardDark"),
            new ThemeOption(AppTheme.Cyberpunk, "ThemeCyberpunk"),
            new ThemeOption(AppTheme.Olive, "ThemeOlive"),
            new ThemeOption(AppTheme.MidnightBlue, "ThemeMidnightBlue"),
            new ThemeOption(AppTheme.DarkRed, "ThemeDarkRed")
        ];

        Languages =
        [
            new LanguageOption(AppLanguage.English, "LanguageEnglish"),
            new LanguageOption(AppLanguage.Ukrainian, "LanguageUkrainian"),
            new LanguageOption(AppLanguage.Russian, "LanguageRussian"),
            new LanguageOption(AppLanguage.Polish, "LanguagePolish")
        ];

        foreach (var device in audioOutputDeviceService.GetOutputDevices())
        {
            AudioDevices.Add(device);
        }

        var settings = applicationSettingsService.Load();
        selectedTheme = Themes.FirstOrDefault(theme => theme.Value == settings.Theme) ?? Themes[0];
        selectedLanguage = Languages.FirstOrDefault(language => language.Value == settings.Language) ?? Languages[0];
        selectedAudioDevice = AudioDevices.FirstOrDefault(device => device.Id == settings.AudioOutputDeviceId)
            ?? AudioDevices.First(device => device.IsDefault);

        SaveCommand = new RelayCommand(_ => Save());
        ResetDataCommand = new RelayCommand(_ => ResetData());
        ExportCommand = new RelayCommand(_ => ShowNotImplemented(LocalizationService.T("ButtonExport")));
        ImportCommand = new RelayCommand(_ => ShowNotImplemented(LocalizationService.T("ButtonImport")));
    }

    public IReadOnlyList<ThemeOption> Themes { get; }

    public IReadOnlyList<LanguageOption> Languages { get; }

    public ObservableCollection<AudioOutputDevice> AudioDevices { get; } = [];

    public ThemeOption SelectedTheme
    {
        get => selectedTheme;
        set => SetProperty(ref selectedTheme, value);
    }

    public LanguageOption SelectedLanguage
    {
        get => selectedLanguage;
        set => SetProperty(ref selectedLanguage, value);
    }

    public AudioOutputDevice SelectedAudioDevice
    {
        get => selectedAudioDevice;
        set => SetProperty(ref selectedAudioDevice, value);
    }

    public string StatusMessage
    {
        get => statusMessage;
        private set => SetProperty(ref statusMessage, value);
    }

    public ICommand SaveCommand { get; }

    public ICommand ResetDataCommand { get; }

    public ICommand ExportCommand { get; }

    public ICommand ImportCommand { get; }

    private void Save()
    {
        var settings = new ApplicationSettings
        {
            Theme = SelectedTheme.Value,
            Language = SelectedLanguage.Value,
            AudioOutputDeviceId = SelectedAudioDevice.Id
        };

        applicationSettingsService.Save(settings);
        ThemeService.Apply(settings.Theme);
        LocalizationService.Apply(settings.Language);
        RefreshLocalizedOptions();
        audioPlaybackService.SetOutputDevice(SelectedAudioDevice.DeviceNumber);
        StatusMessage = LocalizationService.T("SettingsSaved");
    }

    private void ResetData()
    {
        var result = MessageBox.Show(
            LocalizationService.T("ResetDataQuestion"),
            LocalizationService.T("ResetDataTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            applicationDataResetService.Reset();
            MessageBox.Show(
                LocalizationService.T("ResetDataDone"),
                LocalizationService.T("ResetDataTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Application.Current.Shutdown();
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
            MessageBox.Show(
                exception.Message,
                LocalizationService.T("ResetDataErrorTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static void ShowNotImplemented(string actionName)
    {
            MessageBox.Show(
                LocalizationService.F("NotImplementedFormat", actionName),
            LocalizationService.T("AppTitle"),
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void RefreshLocalizedOptions()
    {
        foreach (var theme in Themes)
        {
            theme.Refresh();
        }

        foreach (var language in Languages)
        {
            language.Refresh();
        }
    }
}

public sealed class ThemeOption : ObservableObject
{
    public ThemeOption(AppTheme value, string key)
    {
        Value = value;
        Key = key;
    }

    public AppTheme Value { get; }

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

public sealed class LanguageOption : ObservableObject
{
    public LanguageOption(AppLanguage value, string key)
    {
        Value = value;
        Key = key;
    }

    public AppLanguage Value { get; }

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
