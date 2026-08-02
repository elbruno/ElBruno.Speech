# Audio Formats

## Canonical Format

All pipeline stages operate on **16 kHz, mono, 16-bit signed PCM** (little-endian):

```csharp
AudioFormat.Pcm16KhzMono
// = new AudioFormat(SampleRate: 16_000, Channels: 1, SampleFormat: AudioSampleFormat.Int16)
```

| Property | Value |
|----------|-------|
| `SampleRate` | 16,000 Hz |
| `Channels` | 1 (mono) |
| `SampleFormat` | `AudioSampleFormat.Int16` |
| `BytesPerSample` | 2 |
| `BytesPerSecond` | 32,000 |
| Frame (20 ms) | 640 bytes / 320 samples |

---

## `AudioFormat`

`AudioFormat` is a `sealed record` — value equality, `with`-expressions supported.

```csharp
public sealed record AudioFormat(int SampleRate, int Channels, AudioSampleFormat SampleFormat)
{
    public int BytesPerSample { get; }   // 2 for Int16, 4 for Float32
    public int BytesPerSecond { get; }   // SampleRate × Channels × BytesPerSample
    public static AudioFormat Pcm16KhzMono { get; }
}
```

---

## `AudioFrame`

`AudioFrame` is a `readonly record struct` (stack-allocated):

```csharp
public readonly record struct AudioFrame(
    ReadOnlyMemory<byte> Data,   // caller-owned — copy if storing beyond the call site
    AudioFormat Format,
    long SequenceNumber,         // monotonically increasing per session
    TimeSpan Timestamp,          // position of first sample from session start
    bool IsFinal = false)        // true on last frame of the stream/turn
{
    public TimeSpan Duration { get; }   // Data.Length / BytesPerSecond
}
```

### Buffer Ownership

`AudioFrame.Data` is a view into caller-owned memory. Consumers that store frames beyond the immediate call must copy `Data`:

```csharp
utteranceBytes.Add(frame.Data.ToArray()); // copies — safe to store
```

---

## Supported Resampler Input Rates

`AudioResampler.ResampleTo16Khz()` accepts input at:

| Input Rate | Notes |
|-----------|-------|
| 8,000 Hz | Telephone quality |
| 16,000 Hz | Pass-through (no resampling) |
| 22,050 Hz | CD-quality downscale |
| 24,000 Hz | Common TTS output |
| 44,100 Hz | CD audio |
| 48,000 Hz | Professional/broadcast |

Algorithm: **linear interpolation** (sufficient for VAD/STT input). Higher-quality algorithms (polyphase FIR) can be swapped in without changing the public API.

> **Buffer ownership:** `AudioResampler.ResampleTo16Khz()` returns a `byte[]` rented from `ArrayPool<byte>.Shared`. Callers **must** return it after use. A `null` return means the input was already 16 kHz — no pool return needed.

---

## AudioSampleFormat

```csharp
public enum AudioSampleFormat
{
    Int16 = 0,    // 16-bit signed PCM — canonical format
    Float32 = 1,  // 32-bit float — used internally by ONNX VAD
}
```

---

## Pool-Rented Buffer Helpers

| Helper | Return type | Must return to pool? |
|--------|-------------|---------------------|
| `PcmConverter.Int16ToFloat32()` | `float[]` | ✅ `ArrayPool<float>.Shared` |
| `PcmConverter.Float32ToInt16()` | `byte[]` | ✅ `ArrayPool<byte>.Shared` |
| `MonoConverter.StereoToMono()` | `byte[]` | ✅ `ArrayPool<byte>.Shared` |
| `AudioResampler.ResampleTo16Khz()` | `byte[]?` | ✅ if non-null |
| `AudioFramer.Frame()` | `IEnumerable<AudioFrame>` (owned copies) | ❌ frames own their data |

See [Phase 1 decision](../.squad/decisions/inbox/tank-phase1-audio-primitives.md) for full rationale.
