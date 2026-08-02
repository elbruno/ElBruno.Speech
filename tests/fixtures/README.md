# Test Fixtures

This directory contains golden WAV fixtures for deterministic, reproducible audio tests.

## Purpose

Golden fixtures are synthetic or curated audio files with **known, expected properties**. Tests use them to verify that audio processing components (WAV I/O, resampling, framing, conversion) produce bit-exact or numerically bounded results.

## Contents

| File / Helper | Description |
|---|---|
| `ElBruno.Speech.Audio.Tests/Fixtures/WavFixtures.cs` | Generates synthetic PCM data (440 Hz sine, silence) in-memory — no static files needed |

## Adding New Fixtures

- Prefer **programmatic generation** (in `WavFixtures.cs`) over committing binary `.wav` files to keep the repo size small.
- If you must commit a binary fixture, add it here with a note in this README describing its origin and expected properties.
- Never commit fixture files that contain personal audio recordings.
