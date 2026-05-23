using System.Collections.ObjectModel;
using System.Windows.Input;
using StreamMusicPlayer.Models;
using StreamMusicPlayer.Obs;
using StreamMusicPlayer.Services;

namespace StreamMusicPlayer.ViewModels;

public sealed class ObsSettingsViewModel : ObservableObject, IDisposable
{
    private readonly ObsSettingsService obsSettingsService;
    private readonly ObsClientService obsClientService;
    private string host;
    private int port;
    private string password;
    private bool autoConnectOnStartup;
    private bool reconnectAutomatically;
    private int reconnectIntervalSeconds;
    private string statusMessage = LocalizationService.T("NotConnected");
    private string obsVersion = string.Empty;
    private string webSocketVersion = string.Empty;
    private string currentScene = string.Empty;

    public ObsSettingsViewModel(ObsSettingsService obsSettingsService, ObsClientService obsClientService)
    {
        this.obsSettingsService = obsSettingsService;
        this.obsClientService = obsClientService;
        var settings = obsSettingsService.Load();
        host = settings.Host;
        port = settings.Port;
        password = settings.Password;
        autoConnectOnStartup = settings.AutoConnectOnStartup;
        reconnectAutomatically = settings.ReconnectAutomatically;
        reconnectIntervalSeconds = settings.ReconnectIntervalSeconds;
        obsClientService.Connected += ObsClientService_Connected;
        obsClientService.Disconnected += ObsClientService_Disconnected;
        obsClientService.CurrentProgramSceneChanged += ObsClientService_CurrentProgramSceneChanged;

        SaveCommand = new RelayCommand(_ => Save());
        ConnectCommand = new RelayCommand(async _ => ApplyConnectionInfo(await obsClientService.ConnectAsync(BuildSettings())));
        DisconnectCommand = new RelayCommand(_ => Disconnect());
        RefreshScenesCommand = new RelayCommand(_ => RefreshScenes(), _ => obsClientService.IsConnected);

        if (obsClientService.IsConnected)
        {
            ApplyConnectionInfo(obsClientService.GetConnectionInfo(LocalizationService.T("Connected")));
        }
    }

    public string Host
    {
        get => host;
        set => SetProperty(ref host, value);
    }

    public int Port
    {
        get => port;
        set => SetProperty(ref port, value);
    }

    public string Password
    {
        get => password;
        set => SetProperty(ref password, value);
    }

    public bool AutoConnectOnStartup
    {
        get => autoConnectOnStartup;
        set => SetProperty(ref autoConnectOnStartup, value);
    }

    public bool ReconnectAutomatically
    {
        get => reconnectAutomatically;
        set => SetProperty(ref reconnectAutomatically, value);
    }

    public int ReconnectIntervalSeconds
    {
        get => reconnectIntervalSeconds;
        set => SetProperty(ref reconnectIntervalSeconds, Math.Max(1, value));
    }

    public string StatusMessage
    {
        get => statusMessage;
        private set => SetProperty(ref statusMessage, value);
    }

    public string ObsVersion
    {
        get => obsVersion;
        private set => SetProperty(ref obsVersion, value);
    }

    public string WebSocketVersion
    {
        get => webSocketVersion;
        private set => SetProperty(ref webSocketVersion, value);
    }

    public string CurrentScene
    {
        get => currentScene;
        private set => SetProperty(ref currentScene, value);
    }

    public ObservableCollection<string> Scenes { get; } = [];

    public ICommand SaveCommand { get; }
    public ICommand ConnectCommand { get; }
    public ICommand DisconnectCommand { get; }
    public ICommand RefreshScenesCommand { get; }

    private void Save()
    {
        obsSettingsService.Save(BuildSettings());

        StatusMessage = LocalizationService.T("SettingsSaved");
    }

    private ObsConnectionSettings BuildSettings()
    {
        return new ObsConnectionSettings
        {
            Host = string.IsNullOrWhiteSpace(Host) ? "localhost" : Host.Trim(),
            Port = Math.Clamp(Port, 1, 65535),
            Password = Password,
            AutoConnectOnStartup = AutoConnectOnStartup,
            ReconnectAutomatically = ReconnectAutomatically,
            ReconnectIntervalSeconds = Math.Max(1, ReconnectIntervalSeconds)
        };
    }

    private void Disconnect()
    {
        obsClientService.Disconnect();
        ApplyConnectionInfo(new ObsConnectionInfo { Connected = false, StatusMessage = LocalizationService.T("Disconnected") });
        RaiseConnectionCommandStates();
    }

    private void RefreshScenes()
    {
        if (!obsClientService.IsConnected)
        {
            StatusMessage = LocalizationService.T("ConnectObsBeforeRefreshing");
            return;
        }

        ApplyConnectionInfo(obsClientService.GetConnectionInfo(LocalizationService.T("ScenesRefreshed")));
    }

    private void ApplyConnectionInfo(ObsConnectionInfo info)
    {
        StatusMessage = info.StatusMessage;
        ObsVersion = info.ObsVersion;
        WebSocketVersion = info.WebSocketVersion;
        CurrentScene = info.CurrentScene;
        Scenes.Clear();
        foreach (var scene in info.Scenes)
        {
            Scenes.Add(scene);
        }

        RaiseConnectionCommandStates();
    }

    private void ObsClientService_Connected(object? sender, EventArgs e)
    {
        ApplyConnectionInfo(obsClientService.GetConnectionInfo(LocalizationService.T("Connected")));
    }

    private void ObsClientService_Disconnected(object? sender, EventArgs e)
    {
        ApplyConnectionInfo(new ObsConnectionInfo { Connected = false, StatusMessage = LocalizationService.T("Disconnected") });
    }

    private void ObsClientService_CurrentProgramSceneChanged(object? sender, ObsSceneChangedEventArgs e)
    {
        CurrentScene = e.SceneName;
    }

    private void RaiseConnectionCommandStates()
    {
        if (RefreshScenesCommand is RelayCommand refreshScenesCommand)
        {
            refreshScenesCommand.RaiseCanExecuteChanged();
        }
    }

    public void Dispose()
    {
        obsClientService.Connected -= ObsClientService_Connected;
        obsClientService.Disconnected -= ObsClientService_Disconnected;
        obsClientService.CurrentProgramSceneChanged -= ObsClientService_CurrentProgramSceneChanged;
    }
}
