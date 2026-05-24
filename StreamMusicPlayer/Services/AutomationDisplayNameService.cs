using StreamMusicPlayer.Models;

namespace StreamMusicPlayer.Services;

public static class AutomationDisplayNameService
{
    public static string GetEventKey(string eventType)
    {
        return eventType switch
        {
            AutomationEventTypes.Obs.SceneChanged => "TriggerSceneChanged",
            AutomationEventTypes.Obs.StreamStarted => "TriggerStreamStarted",
            AutomationEventTypes.Obs.StreamStopped => "TriggerStreamStopped",
            AutomationEventTypes.Obs.RecordingStarted => "TriggerRecordingStarted",
            AutomationEventTypes.Obs.RecordingStopped => "TriggerRecordingStopped",
            AutomationEventTypes.Obs.RecordingPaused => "TriggerRecordingPaused",
            AutomationEventTypes.Obs.RecordingResumed => "TriggerRecordingResumed",
            AutomationEventTypes.Obs.SceneItemEnabled => "TriggerSceneItemEnabled",
            AutomationEventTypes.Obs.SceneItemDisabled => "TriggerSceneItemDisabled",
            AutomationEventTypes.Obs.SourceFilterEnabled => "TriggerSourceFilterEnabled",
            AutomationEventTypes.Obs.SourceFilterDisabled => "TriggerSourceFilterDisabled",
            AutomationEventTypes.Player.PlaylistFinished => "TriggerPlaylistFinished",
            AutomationEventTypes.Player.TrackFinished => "TriggerTrackFinished",
            AutomationEventTypes.Player.PlaybackStopped => "TriggerPlaybackStopped",
            AutomationEventTypes.Player.PlaybackPaused => "TriggerPlaybackPaused",
            AutomationEventTypes.Player.PlaybackResumed => "TriggerPlaybackResumed",
            _ => eventType
        };
    }

    public static string GetActionKey(string actionType)
    {
        return actionType switch
        {
            AutomationActionTypes.Obs.ChangeScene => "ActionChangeScene",
            AutomationActionTypes.Obs.StartStream => "ActionStartStream",
            AutomationActionTypes.Obs.StopStream => "ActionStopStream",
            AutomationActionTypes.Obs.StartRecording => "ActionStartRecording",
            AutomationActionTypes.Obs.StopRecording => "ActionStopRecording",
            AutomationActionTypes.Obs.PauseRecording => "ActionPauseRecording",
            AutomationActionTypes.Obs.ResumeRecording => "ActionResumeRecording",
            AutomationActionTypes.Obs.SetSceneItemEnabled => "ActionSetSceneItemEnabled",
            AutomationActionTypes.Obs.SetSourceFilterEnabled => "ActionSetSourceFilterEnabled",
            AutomationActionTypes.Player.PlayPlaylist => "ActionPlayPlaylist",
            AutomationActionTypes.Player.PlayTrack => "ActionPlayTrack",
            AutomationActionTypes.Player.PlayNextPlaylist => "ActionPlayNextPlaylist",
            AutomationActionTypes.Player.PlayPreviousPlaylist => "ActionPlayPreviousPlaylist",
            AutomationActionTypes.Player.Stop => "ActionStop",
            AutomationActionTypes.Player.Pause => "ActionPause",
            AutomationActionTypes.Player.Resume => "ActionResume",
            AutomationActionTypes.Player.SetVolume => "ActionSetVolume",
            AutomationActionTypes.Player.SetCrossfade => "ActionSetCrossfade",
            _ => actionType
        };
    }

    public static string GetEventDisplayName(string eventType)
    {
        return LocalizationService.T(GetEventKey(eventType));
    }

    public static string GetActionDisplayName(string actionType)
    {
        return LocalizationService.T(GetActionKey(actionType));
    }
}
