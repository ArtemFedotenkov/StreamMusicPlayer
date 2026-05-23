# Stream Music Player

Stream Music Player is a free Windows desktop application for personal use. It is designed to help streamers and content creators manage background music playlists and automate music behavior during live streams, recordings, and scene-based workflows.

The application can connect to OBS Studio through obs-websocket integration, but it is an independent tool and is not affiliated with OBS Project.

## Platform

- Windows only
- Built with .NET and WPF
- Requires OBS Studio with obs-websocket enabled if you want to use OBS event rules

## Installation

1. Open the latest release on the GitHub Releases page.
2. Download the `StreamMusicPlayer-vX.X.X.zip` archive.
3. Extract the archive to any folder on your Windows PC.
4. Run `StreamMusicPlayer.exe`.

Do not run the application directly from inside the zip archive. Extract it first so all required files and folders are available next to the executable.

## Purpose

Stream Music Player was created to make stream music control simpler and more flexible. Instead of manually switching tracks or playlists during a stream, you can prepare playlists in advance and create rules that react to scene changes, stream state, or recording state.

It is especially useful for:

- streamers who use different music for starting, ending, intermission, or gameplay scenes;
- creators who record videos and want separate music behavior while recording;
- users who want a dedicated music player with playlist rules, crossfade, and output device selection;
- anyone who wants lightweight music automation without a complex media setup.

## Main Features

- Create, rename, remove, and reorder playlists
- Add individual audio files or entire folders
- Clear playlists or remove selected tracks
- Play, pause, stop, previous, and next controls
- Track seek bar
- Volume control
- Crossfade between tracks
- Smooth volume fading
- Select an audio output device
- Playlist behavior rules:
  - stop after playlist
  - play sequentially
  - repeat one track
  - repeat the whole playlist
  - shuffle during repeat-all
  - play next playlist after completion
  - play a specific playlist after completion
- OBS integration through obs-websocket:
  - auto-connect on startup
  - auto-reconnect
  - manual connect/disconnect
  - refresh scene list
- OBS event rules:
  - scene changed
  - stream started
  - stream stopped
  - recording started
  - recording stopped
  - recording paused
  - recording resumed
- Event actions:
  - play playlist
  - stop playback
  - pause playback
  - resume playback
  - set volume
- Themes:
  - standard light
  - standard dark
  - cyberpunk
  - olive
  - midnight blue
  - dark red
- Interface languages:
  - English
  - Ukrainian
  - Russian
  - Polish

## Intended Use

This project is shared freely with the community and is intended for personal use. You can use it as a helper tool for your own streams, recordings, and local content creation workflow.

## Notes About OBS

Stream Music Player uses obs-websocket integration to receive events from OBS Studio. You need OBS Studio and obs-websocket configured if you want scene, stream, or recording event rules to work.

The application name does not use the OBS name because this is an independent project.

## AI Assistance

This project was created with the help of AI. The implementation, interface, localization, and feature iterations were developed collaboratively with AI assistance.

## Security and Privacy

Stream Music Player does not collect telemetry, analytics, personal data, OBS credentials, or streaming account information.

All OBS communication is performed locally through obs-websocket on the user's machine.

The application does not upload files or stream data to external servers.

## Author

Created by FEDOT.

- Donation: https://destream.net/live/FEDOT/donate
- YouTube channel: https://www.youtube.com/channel/UCAPMkkZzlYhVX4Rn5hRCW9g

## License

This project is published under the MIT License.
