using ElBruno.Speech;
using ElBruno.Speech.Audio;
using ElBruno.Speech.Vad.Silero;
using FluentAssertions;
using Xunit;

namespace ElBruno.Speech.Vad.Silero.Tests;

/// <summary>
/// Integration tests that require the Silero VAD model to be present at the default cache path.
/// Run manually by removing the <c>Skip</c> parameter when the model is available locally.
/// </summary>
public sealed class SileroVadClientIntegrationTests
{
    private const string SkipReason =
        "Integration test — requires Silero model at default cache path (~/.cache/elbruno-speech/models/silero_vad_v4.onnx). Remove Skip to run locally.";

    [Fact(Skip = SkipReason)]
    public async Task ProcessFrameAsync_SilenceInput_ReturnsSilence()
    {
        await using var factory = new SileroVadClientFactory();
        await using var client = await factory.CreateAsync();

        // 32 ms of silence at 16 kHz = 512 samples = 1024 bytes (all zeros)
        var silenceFrame = new AudioFrame(
            Data: new byte[1024],
            Format: AudioFormat.Pcm16KhzMono,
            SequenceNumber: 0,
            Timestamp: TimeSpan.Zero);

        // Feed enough silence frames to trip MinSilenceFrames (6 by default)
        VoiceActivityResult result = default;
        for (int i = 0; i < 10; i++)
            result = await client.ProcessFrameAsync(silenceFrame);

        result.State.Should().Be(VoiceActivityState.Silence);
    }
}
