using System.Windows;
using StreamMusicPlayer.Data;
using StreamMusicPlayer.Repositories;
using StreamMusicPlayer.Services;

namespace StreamMusicPlayer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        ApplySavedTheme();
        EventManager.RegisterClassHandler(
            typeof(Window),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) =>
            {
                if (sender is Window window)
                {
                    ThemeService.ApplyToWindow(window);
                }
            }));

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(
                args.Exception.Message,
                "Stream Music Player Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        base.OnStartup(e);
    }

    private static void ApplySavedTheme()
    {
        try
        {
            AppDataPaths.EnsureCreated();
            new DatabaseInitializer(AppDataPaths.DatabasePath).Initialize();
            var repository = new AppSettingsRepository(AppDataPaths.DatabasePath);
            var settings = new ApplicationSettingsService(repository).Load();
            ThemeService.Apply(settings.Theme);
            LocalizationService.Apply(settings.Language);
        }
        catch
        {
            ThemeService.Apply(Models.AppTheme.Light);
            LocalizationService.Apply(Models.AppLanguage.English);
        }
    }
}
