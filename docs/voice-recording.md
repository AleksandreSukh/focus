# Console voice recording

The console `voice` command records through ffmpeg and saves the result as an audio attachment on the current node.

By default, Focus looks for a bundled ffmpeg executable next to the app, then falls back to `ffmpeg` on `PATH`. The optional `voiceRecorder` config block is only needed for a custom recorder command or explicit ffmpeg device selection. If ffmpeg lives outside the app and is not on `PATH`, set only `voiceRecorder.command` and let Focus choose the input arguments automatically.

Minimal custom ffmpeg path:

```json
{
  "voiceRecorder": {
    "command": "C:\\ffmpeg\\ffmpeg.exe"
  }
}
```

On Windows, the default recorder probes the selected ffmpeg before recording:

- Uses WASAPI when that ffmpeg build supports the `wasapi` input.
- Falls back to DirectShow when WASAPI is unavailable.
- Selects the first DirectShow audio device reported by `ffmpeg -hide_banner -list_devices true -f dshow -i dummy`.

Example Windows DirectShow override:

```json
{
  "voiceRecorder": {
    "command": "C:\\ffmpeg\\ffmpeg.exe",
    "arguments": [
      "-hide_banner",
      "-loglevel",
      "error",
      "-f",
      "dshow",
      "-i",
      "audio=Microphone Array (Realtek(R) Audio)",
      "-t",
      "{seconds}",
      "-vn",
      "-c:a",
      "libopus",
      "-b:a",
      "64k",
      "-y",
      "{output}"
    ],
    "fileExtension": ".webm",
    "mediaType": "audio/webm; codecs=opus"
  }
}
```

If a custom `voiceRecorder` block uses `-f wasapi` with an ffmpeg build that lacks WASAPI support, remove the block to use the app default or switch the arguments to DirectShow.
