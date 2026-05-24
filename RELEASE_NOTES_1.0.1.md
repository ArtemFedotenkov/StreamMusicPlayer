# Stream Music Player 1.0.1

Version 1.0.1 is a feature and stability update after the initial 1.0.0 release.

## New Features

- Added a modular event rules editor with separate rule, event, action, and inspector areas.
- Added support for multiple events and multiple actions in one rule.
- Added drag-and-drop ordering for actions inside rules.
- Added drag-and-drop ordering for tracks inside playlists.
- Added expanded OBS triggers:
  - stream started/stopped;
  - recording started/stopped/paused/resumed;
  - scene source enabled/disabled;
  - source filter enabled/disabled.
- Added expanded OBS actions:
  - start/stop stream;
  - start/stop/pause/resume recording;
  - enable/disable scene sources;
  - enable/disable source filters.
- Added player triggers:
  - playlist finished;
  - track finished;
  - playback stopped/paused/resumed.
- Added player actions:
  - play track;
  - play next playlist;
  - play previous playlist;
  - stop, pause, resume;
  - change global volume;
  - change global crossfade.
- Added playlist rules for sequential playback, repeat one, repeat all, completion actions, and shuffle repeat.
- Added shuffle repeat behavior that reshuffles the playlist order between repeat cycles and avoids immediately repeating the last track when possible.
- Added manual OBS connect button and automatic reconnect behavior.
- Added application settings page.
- Added themes: light, dark, cyberpunk, olive, midnight blue, and dark red.
- Added interface languages: English, Ukrainian, Russian, and Polish.
- Added audio output device selection.
- Added saved global volume and crossfade settings.
- Added program version display in settings.
- Added local application data reset.

## Playback Improvements

- Improved crossfade behavior between tracks.
- Improved automatic crossfade when a track ends and the next track starts.
- Added smooth crossfade-style seeking inside the current track.
- Changed track seeking so the audio position is applied only after releasing the seek slider.
- Limited crossfade to 0-10 seconds in the main player.
- Added finer crossfade control:
  - 0.1 second steps in the main player;
  - 0.01 second precision in rule actions.
- Increased audio output buffering to reduce small playback drops during short CPU or disk load spikes.

## UI Improvements

- Redesigned the main layout with playlists on the left and playlist tracks on the right.
- Improved dark theme colors for fields, buttons, and drop-down menus.
- Applied themes immediately after saving application settings.
- Added themed context menus.
- Improved playlist controls and compact rule editor controls.
- Added centered author and version information in application settings.
- Updated application icon.
- Renamed the project to Stream Music Player.

## Bug Fixes

- Fixed crashes when opening the event rules editor with saved rules.
- Fixed incorrect display names in drop-down lists.
- Fixed scene selection not being saved in event rules.
- Fixed event settings visibility so irrelevant fields are hidden.
- Fixed playlist-specific rule UI behavior.
- Fixed player logic when a rule tries to play a playlist or track that is already playing.
- Fixed volume and crossfade actions so they affect global player settings.
- Fixed crossfade and volume changes so they can run independently.
- Fixed automatic playlist/track ending events after rule-driven playlist switches.
- Added fallback detection for track completion if the audio end event is missed.
- Fixed reset application data when the database file is in use.
- Fixed scrollbar and control theme consistency.
- Fixed source/filter action selection so OBS sources and filters are selected from menus instead of typed manually.

## Data and Privacy

- User data is stored locally in `%APPDATA%\StreamMusicPlayer\app.db`.
- Release archives do not include personal playlists, rules, settings, or OBS connection data.
- The program does not collect telemetry, analytics, personal data, or streaming account information.

