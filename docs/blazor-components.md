# ElBruno.Speech.BlazorComponents

Razor Class Library providing ready-to-use Blazor Server components for speech pipeline UIs.

## Installation

```bash
dotnet add package ElBruno.Speech.BlazorComponents
```

## Setup

In `Program.cs`:
```csharp
builder.Services.AddSpeechBlazorComponents();
```

## Components

### SpeechPipelineStatus
Shows the current pipeline state as a colored badge.
```razor
<SpeechPipelineStatus CurrentState="PipelineState.Listening" ShowStateLabel="true" />
```

### SttTranscriptBox
Rolling live transcript panel.
```razor
<SttTranscriptBox @ref="_box" MaxSegments="20" ShowTimestamps="true" />
@code { void AddText() => _box?.AddSegment("Hello", isFinal: true); }
```

### TtsPlayer
Play/Pause/Stop controls with volume slider.
```razor
<TtsPlayer AutoPlay="false" OnPlaybackCompleted="OnDone" />
```

### MicrophoneSelector
Enumerates available audio devices.
```razor
<MicrophoneSelector OnDeviceChanged="(id) => _deviceId = id" />
```

### VadVisualizer
Real-time VAD probability bar.
```razor
<VadVisualizer @ref="_vad" ShowProbabilityLabel="true" />
@code { void Update(float p) => _vad?.UpdateProbability(p); }
```

### PipelineBuilder
Interactive pipeline configuration.
```razor
<PipelineBuilder OnConfigured="(opts) => _options = opts" />
```

## Custom Device Provider

Implement `IAudioDeviceProvider` for real device enumeration:
```csharp
public class MyDeviceProvider : IAudioDeviceProvider {
    public async Task<IReadOnlyList<AudioDeviceInfo>> GetInputDevicesAsync(CancellationToken ct = default) {
        // enumerate real devices
        return [ new AudioDeviceInfo("mic-1", "USB Microphone") ];
    }
}
// Register:
services.AddSingleton<IAudioDeviceProvider, MyDeviceProvider>();
```
