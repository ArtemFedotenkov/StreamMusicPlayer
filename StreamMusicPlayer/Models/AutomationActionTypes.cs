namespace StreamMusicPlayer.Models;

public static class AutomationActionTypes
{
    public static class Obs
    {
        public const string ChangeScene = "ChangeScene";
        public const string StartStream = "StartStream";
        public const string StopStream = "StopStream";
        public const string StartRecording = "StartRecording";
        public const string StopRecording = "StopRecording";
        public const string PauseRecording = "PauseRecording";
        public const string ResumeRecording = "ResumeRecording";
        public const string SetSceneItemEnabled = "SetSceneItemEnabled";
        public const string SetSourceFilterEnabled = "SetSourceFilterEnabled";
    }

    public static class Player
    {
        public const string PlayPlaylist = "PlayPlaylist";
        public const string PlayTrack = "PlayTrack";
        public const string PlayNextPlaylist = "PlayNextPlaylist";
        public const string PlayPreviousPlaylist = "PlayPreviousPlaylist";
        public const string Stop = "Stop";
        public const string Pause = "Pause";
        public const string Resume = "Resume";
        public const string SetVolume = "SetVolume";
        public const string SetCrossfade = "SetCrossfade";
    }
}
