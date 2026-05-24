namespace StreamMusicPlayer.Models;

public static class AutomationEventTypes
{
    public static class Obs
    {
        public const string SceneChanged = "SceneChanged";
        public const string StreamStarted = "StreamStarted";
        public const string StreamStopped = "StreamStopped";
        public const string RecordingStarted = "RecordingStarted";
        public const string RecordingStopped = "RecordingStopped";
        public const string RecordingPaused = "RecordingPaused";
        public const string RecordingResumed = "RecordingResumed";
        public const string SceneItemEnabled = "SceneItemEnabled";
        public const string SceneItemDisabled = "SceneItemDisabled";
        public const string SourceFilterEnabled = "SourceFilterEnabled";
        public const string SourceFilterDisabled = "SourceFilterDisabled";
    }

    public static class Player
    {
        public const string PlaylistFinished = "PlaylistFinished";
        public const string TrackFinished = "TrackFinished";
        public const string PlaybackStopped = "PlaybackStopped";
        public const string PlaybackPaused = "PlaybackPaused";
        public const string PlaybackResumed = "PlaybackResumed";
    }
}
