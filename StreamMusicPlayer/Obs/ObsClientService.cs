using Newtonsoft.Json.Linq;
using StreamMusicPlayer.Models;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Communication;
using OBSWebsocketDotNet.Types;
using OBSWebsocketDotNet.Types.Events;

namespace StreamMusicPlayer.Obs;

public sealed class ObsClientService : IDisposable
{
    private readonly OBSWebsocket client = new();
    private readonly SemaphoreSlim connectionLock = new(1, 1);
    private ObsConnectionSettings? currentSettings;
    private CancellationTokenSource reconnectCancellation = new();
    private bool manualDisconnectRequested;
    private bool disposed;

    public ObsClientService()
    {
        client.Connected += OnConnected;
        client.Disconnected += OnDisconnected;
        client.CurrentProgramSceneChanged += OnCurrentProgramSceneChanged;
        client.StreamStateChanged += OnStreamStateChanged;
        client.RecordStateChanged += OnRecordStateChanged;
        client.SceneItemEnableStateChanged += OnSceneItemEnableStateChanged;
        client.SourceFilterEnableStateChanged += OnSourceFilterEnableStateChanged;
    }

    public event EventHandler? Connected;
    public event EventHandler? Disconnected;
    public event EventHandler<ObsSceneChangedEventArgs>? CurrentProgramSceneChanged;
    public event EventHandler<ObsEventTriggeredEventArgs>? ObsEventTriggered;

    public bool IsConnected => client.IsConnected;

    public async Task<ObsConnectionInfo> ConnectAsync(ObsConnectionSettings settings)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        currentSettings = settings;
        manualDisconnectRequested = false;
        CancelReconnect();

        await connectionLock.WaitAsync();
        try
        {
            var info = await ConnectCoreAsync(settings);
            if (!info.Connected && settings.ReconnectAutomatically && !manualDisconnectRequested)
            {
                StartReconnectLoop(settings);
            }

            return info;
        }
        finally
        {
            connectionLock.Release();
        }
    }

    private async Task<ObsConnectionInfo> ConnectCoreAsync(ObsConnectionSettings settings)
    {
        var connectedCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler? connectedHandler = null;
        connectedHandler = (_, _) =>
        {
            client.Connected -= connectedHandler;
            connectedCompletion.TrySetResult();
        };
        client.Connected += connectedHandler;

        try
        {
            var url = $"ws://{settings.Host}:{settings.Port}";
            client.ConnectAsync(url, settings.Password);

            var completedTask = await Task.WhenAny(connectedCompletion.Task, Task.Delay(TimeSpan.FromSeconds(8)));
            if (completedTask != connectedCompletion.Task)
            {
                client.Connected -= connectedHandler;
                return new ObsConnectionInfo { Connected = false, StatusMessage = "Connection timed out" };
            }

            return GetConnectionInfo("Connected");
        }
        catch (Exception exception)
        {
            client.Connected -= connectedHandler;
            return new ObsConnectionInfo { Connected = false, StatusMessage = exception.Message };
        }
    }

    public ObsConnectionInfo TestConnection(ObsConnectionSettings settings)
    {
        if (!IsConnected)
        {
            return new ObsConnectionInfo
            {
                Connected = false,
                StatusMessage = $"Ready to connect to ws://{settings.Host}:{settings.Port}"
            };
        }

        return GetConnectionInfo("Connected");
    }

    public void Disconnect()
    {
        manualDisconnectRequested = true;
        CancelReconnect();
        if (client.IsConnected)
        {
            client.Disconnect();
        }
    }

    public IReadOnlyList<string> GetScenes()
    {
        if (!client.IsConnected)
        {
            return [];
        }

        var response = client.SendRequest("GetSceneList");
        return response["scenes"]?
            .OfType<JObject>()
            .Select(scene => scene["sceneName"]?.ToString())
            .Where(sceneName => !string.IsNullOrWhiteSpace(sceneName))
            .Select(sceneName => sceneName!)
            .ToList() ?? [];
    }

    public IReadOnlyList<string> GetInputs()
    {
        if (!client.IsConnected)
        {
            return [];
        }

        var response = client.SendRequest("GetInputList");
        return response["inputs"]?
            .OfType<JObject>()
            .Select(input => input["inputName"]?.ToString())
            .Where(inputName => !string.IsNullOrWhiteSpace(inputName))
            .Select(inputName => inputName!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(inputName => inputName)
            .ToList() ?? [];
    }

    public IReadOnlyList<ObsSceneItemInfo> GetSceneItems()
    {
        var sceneItems = new List<ObsSceneItemInfo>();
        foreach (var sceneName in GetScenes())
        {
            try
            {
                var response = client.SendRequest("GetSceneItemList", new JObject
                {
                    ["sceneName"] = sceneName
                });

                var items = response["sceneItems"]?
                    .OfType<JObject>()
                    .Select(item => new ObsSceneItemInfo
                    {
                        SceneName = sceneName,
                        SourceName = item["sourceName"]?.ToString() ?? string.Empty,
                        SceneItemId = item["sceneItemId"]?.Value<int>() ?? 0
                    })
                    .Where(item => !string.IsNullOrWhiteSpace(item.SourceName) && item.SceneItemId > 0) ?? [];
                sceneItems.AddRange(items);
            }
            catch
            {
                // Skip scenes that cannot expose scene items through websocket.
            }
        }

        return sceneItems
            .OrderBy(item => item.SceneName)
            .ThenBy(item => item.SourceName)
            .ThenBy(item => item.SceneItemId)
            .ToList();
    }

    public IReadOnlyList<ObsSourceFilterInfo> GetSourceFilters()
    {
        var filters = new List<ObsSourceFilterInfo>();
        var sourceNames = GetScenes()
            .Concat(GetInputs())
            .Where(sourceName => !string.IsNullOrWhiteSpace(sourceName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(sourceName => sourceName);

        foreach (var sourceName in sourceNames)
        {
            try
            {
                var response = client.SendRequest("GetSourceFilterList", new JObject
                {
                    ["sourceName"] = sourceName
                });

                var sourceFilters = response["filters"]?
                    .OfType<JObject>()
                    .Select(filter => filter["filterName"]?.ToString())
                    .Where(filterName => !string.IsNullOrWhiteSpace(filterName))
                    .Select(filterName => new ObsSourceFilterInfo
                    {
                        SourceName = sourceName,
                        FilterName = filterName!
                    }) ?? [];
                filters.AddRange(sourceFilters);
            }
            catch
            {
                // Some OBS sources may not expose filters through websocket; skip them.
            }
        }

        return filters
            .OrderBy(filter => filter.SourceName)
            .ThenBy(filter => filter.FilterName)
            .ToList();
    }

    public string GetCurrentScene()
    {
        if (!client.IsConnected)
        {
            return string.Empty;
        }

        var response = client.SendRequest("GetCurrentProgramScene");
        return response["currentProgramSceneName"]?.ToString() ?? string.Empty;
    }

    public void SetCurrentScene(string sceneName)
    {
        if (!client.IsConnected || string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        client.SendRequest("SetCurrentProgramScene", new JObject
        {
            ["sceneName"] = sceneName
        });
    }

    public void StartStream()
    {
        SendObsCommand("StartStream");
    }

    public void StopStream()
    {
        SendObsCommand("StopStream");
    }

    public void StartRecording()
    {
        SendObsCommand("StartRecord");
    }

    public void StopRecording()
    {
        SendObsCommand("StopRecord");
    }

    public void PauseRecording()
    {
        SendObsCommand("PauseRecord");
    }

    public void ResumeRecording()
    {
        SendObsCommand("ResumeRecord");
    }

    public void SetSourceFilterEnabled(string sourceName, string filterName, bool enabled)
    {
        if (!client.IsConnected || string.IsNullOrWhiteSpace(sourceName) || string.IsNullOrWhiteSpace(filterName))
        {
            return;
        }

        client.SendRequest("SetSourceFilterEnabled", new JObject
        {
            ["sourceName"] = sourceName,
            ["filterName"] = filterName,
            ["filterEnabled"] = enabled
        });
    }

    public void SetSceneItemEnabled(string sceneName, int sceneItemId, bool enabled)
    {
        if (!client.IsConnected || string.IsNullOrWhiteSpace(sceneName) || sceneItemId <= 0)
        {
            return;
        }

        client.SendRequest("SetSceneItemEnabled", new JObject
        {
            ["sceneName"] = sceneName,
            ["sceneItemId"] = sceneItemId,
            ["sceneItemEnabled"] = enabled
        });
    }

    public ObsConnectionInfo GetConnectionInfo(string statusMessage)
    {
        if (!client.IsConnected)
        {
            return new ObsConnectionInfo { Connected = false, StatusMessage = "Disconnected" };
        }

        var version = client.SendRequest("GetVersion");
        var scenes = GetScenes();
        var currentScene = GetCurrentScene();

        return new ObsConnectionInfo
        {
            Connected = true,
            StatusMessage = statusMessage,
            ObsVersion = version["obsVersion"]?.ToString() ?? string.Empty,
            WebSocketVersion = version["obsWebSocketVersion"]?.ToString() ?? string.Empty,
            Scenes = scenes,
            CurrentScene = currentScene
        };
    }

    public async Task<ObsConnectionInfo> ReconnectAsync()
    {
        if (currentSettings is null)
        {
            return new ObsConnectionInfo { Connected = false, StatusMessage = "No OBS settings saved" };
        }

        if (client.IsConnected)
        {
            client.Disconnect();
        }

        await Task.Delay(250);
        return await ConnectAsync(currentSettings);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        manualDisconnectRequested = true;
        CancelReconnect();
        client.Connected -= OnConnected;
        client.Disconnected -= OnDisconnected;
        client.CurrentProgramSceneChanged -= OnCurrentProgramSceneChanged;
        client.StreamStateChanged -= OnStreamStateChanged;
        client.RecordStateChanged -= OnRecordStateChanged;
        client.SceneItemEnableStateChanged -= OnSceneItemEnableStateChanged;
        client.SourceFilterEnableStateChanged -= OnSourceFilterEnableStateChanged;
        Disconnect();
        reconnectCancellation.Dispose();
        connectionLock.Dispose();
    }

    private void OnConnected(object? sender, EventArgs e)
    {
        manualDisconnectRequested = false;
        CancelReconnect();
        Connected?.Invoke(this, EventArgs.Empty);
    }

    private void OnDisconnected(object? sender, ObsDisconnectionInfo e)
    {
        Disconnected?.Invoke(this, EventArgs.Empty);
        if (disposed || manualDisconnectRequested || currentSettings is not { ReconnectAutomatically: true } settings)
        {
            return;
        }

        StartReconnectLoop(settings);
    }

    private void StartReconnectLoop(ObsConnectionSettings settings)
    {
        CancelReconnect();
        reconnectCancellation = new CancellationTokenSource();
        var cancellationToken = reconnectCancellation.Token;

        _ = Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested && !disposed && !client.IsConnected)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, settings.ReconnectIntervalSeconds)), cancellationToken);
                    if (cancellationToken.IsCancellationRequested || disposed || manualDisconnectRequested || client.IsConnected)
                    {
                        return;
                    }

                    await connectionLock.WaitAsync(cancellationToken);
                    try
                    {
                        if (!client.IsConnected)
                        {
                            await ConnectCoreAsync(settings);
                        }
                    }
                    finally
                    {
                        connectionLock.Release();
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch
                {
                    // Keep trying until OBS becomes available or the user disconnects manually.
                }
            }
        }, cancellationToken);
    }

    private void CancelReconnect()
    {
        reconnectCancellation.Cancel();
        reconnectCancellation.Dispose();
        reconnectCancellation = new CancellationTokenSource();
    }

    private void SendObsCommand(string requestType)
    {
        if (!client.IsConnected)
        {
            return;
        }

        client.SendRequest(requestType);
    }

    private void OnCurrentProgramSceneChanged(object? sender, EventArgs e)
    {
        var sceneName = e.GetType().GetProperty("SceneName")?.GetValue(e)?.ToString() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(sceneName))
        {
            CurrentProgramSceneChanged?.Invoke(this, new ObsSceneChangedEventArgs(sceneName));
            ObsEventTriggered?.Invoke(this, new ObsEventTriggeredEventArgs("SceneChanged", sceneName));
        }
    }

    private void OnStreamStateChanged(object? sender, StreamStateChangedEventArgs e)
    {
        var triggerType = e.OutputState.State switch
        {
            OutputState.OBS_WEBSOCKET_OUTPUT_STARTED => "StreamStarted",
            OutputState.OBS_WEBSOCKET_OUTPUT_STOPPED => "StreamStopped",
            _ => string.Empty
        };

        if (!string.IsNullOrWhiteSpace(triggerType))
        {
            ObsEventTriggered?.Invoke(this, new ObsEventTriggeredEventArgs(triggerType));
        }
    }

    private void OnRecordStateChanged(object? sender, RecordStateChangedEventArgs e)
    {
        var triggerType = e.OutputState.State switch
        {
            OutputState.OBS_WEBSOCKET_OUTPUT_STARTED => "RecordingStarted",
            OutputState.OBS_WEBSOCKET_OUTPUT_STOPPED => "RecordingStopped",
            OutputState.OBS_WEBSOCKET_OUTPUT_PAUSED => "RecordingPaused",
            OutputState.OBS_WEBSOCKET_OUTPUT_RESUMED => "RecordingResumed",
            _ => string.Empty
        };

        if (!string.IsNullOrWhiteSpace(triggerType))
        {
            ObsEventTriggered?.Invoke(this, new ObsEventTriggeredEventArgs(triggerType));
        }
    }

    private void OnSceneItemEnableStateChanged(object? sender, SceneItemEnableStateChangedEventArgs e)
    {
        var triggerType = e.SceneItemEnabled
            ? AutomationEventTypes.Obs.SceneItemEnabled
            : AutomationEventTypes.Obs.SceneItemDisabled;
        var sourceName = ResolveSceneItemSourceName(e.SceneName, e.SceneItemId);
        ObsEventTriggered?.Invoke(
            this,
            new ObsEventTriggeredEventArgs(
                triggerType,
                condition: e.SceneName,
                sourceName: sourceName,
                sceneItemId: e.SceneItemId,
                filterEnabled: e.SceneItemEnabled));
    }

    private void OnSourceFilterEnableStateChanged(object? sender, SourceFilterEnableStateChangedEventArgs e)
    {
        var triggerType = e.FilterEnabled
            ? AutomationEventTypes.Obs.SourceFilterEnabled
            : AutomationEventTypes.Obs.SourceFilterDisabled;
        ObsEventTriggered?.Invoke(
            this,
            new ObsEventTriggeredEventArgs(
                triggerType,
                sourceName: e.SourceName,
                filterName: e.FilterName,
                filterEnabled: e.FilterEnabled));
    }

    private string ResolveSceneItemSourceName(string sceneName, int sceneItemId)
    {
        if (string.IsNullOrWhiteSpace(sceneName) || sceneItemId <= 0)
        {
            return string.Empty;
        }

        try
        {
            var response = client.SendRequest("GetSceneItemList", new JObject
            {
                ["sceneName"] = sceneName
            });

            return response["sceneItems"]?
                .OfType<JObject>()
                .FirstOrDefault(item => item["sceneItemId"]?.Value<int>() == sceneItemId)?["sourceName"]
                ?.ToString() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
