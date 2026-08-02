# BlazorSpeechDemo Sample

Blazor Server application demonstrating all six ElBruno.Speech.BlazorComponents components.

## Running

```bash
cd src/samples/BlazorSpeechDemo
dotnet run
```

Navigate to `http://localhost:5000`.

## What It Shows

- **Pipeline Status** — click state buttons to cycle through Idle/Listening/Transcribing/Responding/Speaking
- **STT Transcript** — click "Add Sample" to append transcript segments
- **TTS Player** — Play/Pause/Stop with volume control
- **Microphone Selector** — device picker (demo uses default device)
- **VAD Visualizer** — drag slider to simulate probability
- **Pipeline Builder** — configure frame duration, capacity, and pre-roll
