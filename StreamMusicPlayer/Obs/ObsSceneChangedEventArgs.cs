namespace StreamMusicPlayer.Obs;

public sealed class ObsSceneChangedEventArgs : EventArgs
{
    public ObsSceneChangedEventArgs(string sceneName)
    {
        SceneName = sceneName;
    }

    public string SceneName { get; }
}
