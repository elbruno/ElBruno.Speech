# BlazorSpeechDemo Sample

Blazor Server application demonstrating all six `ElBruno.Speech.BlazorComponents` components with the current validated demo wiring. The sample uses fake STT, chat, and TTS providers, so it demonstrates component behavior without microphone access, model downloads, or real audio playback.

## Running

```bash
cd src/samples/BlazorSpeechDemo
dotnet run
```

Open the HTTP URL printed by `dotnet run` (or start with `dotnet run --urls http://localhost:5000` and open `http://localhost:5000`). The app uses interactive server rendering.

## What It Shows

- **Pipeline Status** — click state buttons to display Idle/Listening/Transcribing/Responding/Speaking
- **STT Transcript** — click **Add Sample** to append a timestamped final segment
- **TTS Player** — exercise Play/Pause/Stop state transitions and volume control; no audio is played
- **Microphone Selector** — enumerate browser audio-input devices when the browser grants access; the list can be empty during prerendering or when device enumeration is unavailable
- **VAD Visualizer** — drag the slider to display a clamped probability and speech/silence styling
- **Pipeline Builder** — apply frame duration, channel capacity, and pre-roll options and display the resulting values

The sample registers `AddSpeechBlazorComponents()`, which uses the browser-backed device provider and scoped per-circuit state. Production applications can replace `IAudioDeviceProvider` and connect component events to their own audio and pipeline services.
