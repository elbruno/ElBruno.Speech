# Project Context

- **Owner:** Bruno Capuano
- **Project:** ElBruno.Speech — a reusable, local-first speech runtime for .NET 8/10
- **Stack:** C#, .NET 8/10, ONNX Runtime, Silero VAD, ArrayPool/MemoryPool, Span<T>/Memory<T>
- **Created:** 2026-07-03

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->
- AudioFrame is an immutable view; long-lived consumers must copy memory explicitly
- Silero VAD: recurrent h/c tensors are per IVoiceActivitySession; ONNX session is shared
- Resampling target: 16 kHz mono — all upstream formats must normalize to this before VAD/STT
- Pre-roll default: 200 ms; preserved through resampling
- No per-frame heap allocations after warmup — ArrayPool<byte> is mandatory for frame buffers
- Silero model downloaded via ElBruno.HuggingFace.Downloader, never bundled
