# ElBruno.Speech.BlazorComponents

Razor Class Library targeting .NET 8 and providing six ready-to-use Blazor Server components for speech pipeline UIs. The components are UI primitives; they do not start a speech pipeline or provide browser audio capture/playback by themselves.

## Installation

```bash
dotnet add package ElBruno.Speech.BlazorComponents
```

## Setup

In `Program.cs`:
```csharp
builder.Services.AddSpeechBlazorComponents();
```

The registration adds a scoped `SpeechStateService` and a browser-backed `IAudioDeviceProvider`. State is scoped to the Blazor circuit so status, transcript, VAD, and device components can share updates without depending on an `ISpeechPipeline` event API. Device enumeration is deferred until the component is interactive; replace `IAudioDeviceProvider` with an application-specific provider for non-browser hosts or tests.

## Components

### SpeechPipelineStatus
Shows the supplied pipeline state as a colored badge. If `CurrentState` is omitted, it observes the scoped `SpeechStateService`; shared state changes raise `OnStateChanged`.
```razor
<SpeechPipelineStatus CurrentState="PipelineState.Listening" ShowStateLabel="true" />
```

### SttTranscriptBox
Rolling live transcript panel backed by the scoped `SpeechStateService`.
```razor
<SttTranscriptBox @ref="_box" MaxSegments="20" ShowTimestamps="true" />
@code { void AddText() => _box?.AddSegment("Hello", isFinal: true); }
```

### TtsPlayer
Play/Pause/Stop state controls with a volume slider. The component does not play audio or connect to a TTS provider. `AutoPlay` and request callbacks provide an application-owned boundary; call `CompletePlayback` when the host's audio output finishes.
```razor
<TtsPlayer AutoPlay="false" OnPlaybackCompleted="OnDone" />
```

### MicrophoneSelector
Displays the devices returned by `IAudioDeviceProvider` and raises `OnDeviceChanged` when the selection changes.
```razor
<MicrophoneSelector OnDeviceChanged="(id) => _deviceId = id" />
```

### VadVisualizer
Displays a clamped VAD probability from 0 to 1. Values at or above 0.5 are styled as speech. When `Probability` is omitted, it observes the scoped `SpeechStateService`.
```razor
<VadVisualizer @ref="_vad" ShowProbabilityLabel="true" />
@code { void Update(float p) => _vad?.UpdateProbability(p); }
```

### PipelineBuilder
Edits frame duration, channel capacity, and pre-roll values, then returns a `SpeechPipelineOptions` instance when Apply is clicked. The validated input ranges are 10–100 ms, 16–256, and 0–1000 ms respectively.
```razor
<PipelineBuilder OnConfigured="(opts) => _options = opts" />
```

## Custom Device Provider

Implement `IAudioDeviceProvider` for real device enumeration:
```csharp
public class MyDeviceProvider : IAudioDeviceProvider {
    public Task<IReadOnlyList<AudioDeviceInfo>> GetInputDevicesAsync(CancellationToken ct = default) {
        // enumerate real devices
        IReadOnlyList<AudioDeviceInfo> devices = [new("mic-1", "USB Microphone")];
        return Task.FromResult(devices);
    }
}
// Register:
services.AddScoped<IAudioDeviceProvider, MyDeviceProvider>();
```
