namespace StreamMusicPlayer.Obs;

public sealed class ObsEventTriggeredEventArgs : EventArgs
{
    public ObsEventTriggeredEventArgs(
        string triggerType,
        string condition = "",
        string sourceName = "",
        int? sceneItemId = null,
        string filterName = "",
        bool? filterEnabled = null)
    {
        TriggerType = triggerType;
        Condition = condition;
        SourceName = sourceName;
        SceneItemId = sceneItemId;
        FilterName = filterName;
        FilterEnabled = filterEnabled;
    }

    public string TriggerType { get; }
    public string Condition { get; }
    public string SourceName { get; }
    public int? SceneItemId { get; }
    public string FilterName { get; }
    public bool? FilterEnabled { get; }
}
