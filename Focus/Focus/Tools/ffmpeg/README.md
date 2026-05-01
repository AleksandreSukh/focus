# Bundled ffmpeg

Place platform ffmpeg builds here so release publishing copies them beside the app.

Expected default locations:

- `Tools/ffmpeg/win-x64/ffmpeg.exe`
- `Tools/ffmpeg/linux-x64/ffmpeg`
- `Tools/ffmpeg/osx-arm64/ffmpeg`

The app looks for a bundled executable first, then falls back to `ffmpeg` on `PATH`. If a platform build ships supporting libraries next to the executable, keep those files in the same platform folder.

Windows builds should include at least one microphone capture input. Focus prefers WASAPI when available and falls back to DirectShow when the bundled ffmpeg reports `dshow` support.
