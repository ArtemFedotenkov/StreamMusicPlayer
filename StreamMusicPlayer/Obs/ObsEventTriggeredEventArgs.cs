namespace StreamMusicPlayer.Obs;

public sealed class ObsEventTriggeredEventArgs : EventArgs
{
    public ObsEventTriggeredEventArgs(string triggerType, string condition = "")
    {
        TriggerType = triggerType;
        Condition = condition;
    }

    public string TriggerType { get; }
    public string Condition { get; }
}
