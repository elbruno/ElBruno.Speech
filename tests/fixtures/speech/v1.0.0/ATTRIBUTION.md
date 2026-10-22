# Attribution and license

FLEURS: Few-shot Learning Evaluation of Universal Representations of Speech
(2022), by Alexis Conneau, Min Ma, Simran Khanuja, Yu Zhang, Vera Axelrod,
Siddharth Dalmia, Jason Riesa, Clara Rivera, and Ankur Bapna.

- Paper: https://arxiv.org/abs/2205.12446
- Dataset: https://huggingface.co/datasets/google/fleurs
- Source revision: 70bb2e84b976b7e960aa89f1c648e09c59f894dd
- Source card: https://huggingface.co/datasets/google/fleurs/blob/70bb2e84b976b7e960aa89f1c648e09c59f894dd/README.md
- License: Creative Commons Attribution 4.0 International (CC BY 4.0)
- License URL: https://creativecommons.org/licenses/by/4.0/
- Full license: LICENSE.txt in this bundle

FLEURS is the speech version of the FLoRes translation benchmark. Dataset
contributors are credited in the source card. The manifest identifies the exact
test recordings and references used.

Changes by ElBruno.Speech contributors: selected a small subset, converted complete
float32 WAV recordings to canonical mono 16 kHz PCM16 WAV (without trimming or
resampling), and packaged source references/provenance in a JSON manifest.
Original raw and normalized transcript strings are retained unchanged.

Audio and transcript references remain CC BY 4.0, not the surrounding code's MIT
license. Preserve attribution, link the license, and indicate further changes.
No endorsement by the dataset authors or speakers is implied. The license provides
no warranties and does not remove other applicable rights.
