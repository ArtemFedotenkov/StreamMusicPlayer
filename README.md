# Stream Music Player

Stream Music Player is a free Windows desktop music player for personal use by streamers and creators. It is designed to manage local music playlists and automate playback based on events from OBS Studio through obs-websocket.

The project was created with the help of AI and is shared with the community as a personal-use tool.

## Installation

1. Download the latest release archive from the GitHub Releases page.
2. Extract the archive to any folder on your Windows PC.
3. Run `StreamMusicPlayer.exe`.
4. If you want OBS integration, open OBS Studio and make sure the WebSocket server is enabled.
5. In Stream Music Player, open OBS settings and enter your local WebSocket connection settings.

The application is portable in the sense that the program files can be placed anywhere, but user data is stored in the Windows user profile.

## Main Features

- Local playlist management for music files.
- Add audio files or whole folders.
- Rename, add, remove, clear, and reorder playlists.
- Reorder tracks by dragging them in the playlist table.
- Play, pause, stop, previous, next, and seek inside a track.
- Smooth crossfade between tracks.
- Smooth crossfade-style seeking inside the current track.
- Global volume and crossfade settings saved between launches.
- Configurable playlist behavior:
  - play in order;
  - repeat one track;
  - repeat the whole playlist;
  - shuffle repeat by reshuffling the playlist order between repeat cycles;
  - stop after the playlist;
  - switch to previous, next, or a selected playlist after completion.
- Modular event rules editor:
  - create multiple rules;
  - add one or more events per rule;
  - add one or more actions per rule;
  - reorder actions by dragging them.
- OBS event triggers:
  - scene changed;
  - stream started/stopped;
  - recording started/stopped/paused/resumed;
  - scene source enabled/disabled;
  - source filter enabled/disabled.
- Player event triggers:
  - playlist finished;
  - track finished;
  - playback stopped;
  - playback paused;
  - playback resumed.
- OBS actions:
  - change scene;
  - start/stop stream;
  - start/stop/pause/resume recording;
  - enable or disable scene sources;
  - enable or disable source filters.
- Player actions:
  - play playlist;
  - play track;
  - play next or previous playlist;
  - stop, pause, resume;
  - change global volume;
  - change global crossfade.
- Application themes:
  - standard light;
  - standard dark;
  - cyberpunk;
  - olive;
  - midnight blue;
  - dark red.
- Interface languages:
  - English;
  - Ukrainian;
  - Russian;
  - Polish.
- Audio output device selection.
- Local reset of all application data.

## Data Storage

Stream Music Player stores user data locally in the Windows roaming application data folder:

```text
%APPDATA%\StreamMusicPlayer\
```

For a typical Windows user this looks like:

```text
C:\Users\<UserName>\AppData\Roaming\StreamMusicPlayer\
```

The main database file is:

```text
%APPDATA%\StreamMusicPlayer\app.db
```

This database stores playlists, tracks, rules, application settings, OBS connection settings, and other user configuration.

Program releases do not include your personal `app.db` file. Your playlists and settings are not stored inside the release folder.

## Security and Privacy

Stream Music Player does not collect telemetry, analytics, personal data, OBS credentials, or streaming account information.

All OBS communication is performed locally through obs-websocket on the user's machine.

The application does not upload files or stream data to external servers.

## Requirements

- Windows.
- .NET runtime compatible with the published build.
- OBS Studio with obs-websocket enabled if OBS automation is needed.

## Author

Author: FEDOT.

Donation: https://destream.net/live/FEDOT/donate

YouTube channel: https://www.youtube.com/channel/UCAPMkkZzlYhVX4Rn5hRCW9g

