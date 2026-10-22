# Speech fixtures 1.0.0

Twenty complete FLEURS test recordings: ten English (`en_us`) and ten Spanish
(`es_419`), with supplied raw and normalized transcript references.
These are independent text references, not model-generated hypotheses.
Human-reviewed word timestamps are not provided.

The manifest pins the upstream revision, source metadata hashes, original WAV
hashes, converted WAV hashes, exact archive members, and conversion details.
Selection takes the first ten distinct sentence IDs in each pinned test archive's
order whose TSV sample count is between 32000 and 240000 inclusive (2-15 seconds).
No clips are trimmed, concatenated, selected by model performance, or resampled.
Float32 samples are rounded to PCM16 with ties to even, then clamped.

Schema version 1 is independent of bundle version 1.0.0. Published bundle contents
are immutable. The entire uncompressed bundle is limited to 20 MiB.
This small read-speech set supports regression testing, not representative accuracy
claims, unseen-training-data guarantees, or podcast-specific acceptance.

Use any WAV/JSON tooling to consume the bundle. Verify SHA-256 hashes before use.
The JSON `path` values use portable slash-separated relative paths.
Retain this README, ATTRIBUTION.md, LICENSE.txt, and provenance on redistribution.
See the repository's docs/speech-fixtures.md for test and distribution commands.
