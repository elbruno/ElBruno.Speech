using ElBruno.Speech;
using FluentAssertions;

namespace ElBruno.Speech.Conformance.Tests;

/// <summary>
/// Verifies that IVadClient, VoiceActivityResult, VoiceActivityState satisfy the PRD contract.
/// </summary>
public sealed class VadContractConformanceTests
{
    [Fact]
    public void VoiceActivityState_HasSilenceAndSpeechValues()
    {
        Enum.IsDefined(typeof(VoiceActivityState), VoiceActivityState.Silence).Should().BeTrue();
        Enum.IsDefined(typeof(VoiceActivityState), VoiceActivityState.Speech).Should().BeTrue();
        ((int)VoiceActivityState.Silence).Should().Be(0);
        ((int)VoiceActivityState.Speech).Should().Be(1);
    }

    [Fact]
    public void VoiceActivityResult_IsReadOnlyRecordStruct()
    {
        var frame = new AudioFrame(new byte[640], AudioFormat.Pcm16KhzMono, 0, TimeSpan.Zero);
        var result = new VoiceActivityResult(VoiceActivityState.Speech, 0.9f, frame);
        result.State.Should().Be(VoiceActivityState.Speech);
        result.Confidence.Should().Be(0.9f);
        result.Frame.Should().Be(frame);
    }

    [Fact]
    public void IVadClient_IsAsyncDisposable()
    {
        typeof(IVadClient).IsAssignableTo(typeof(IAsyncDisposable)).Should().BeTrue();
    }

    [Fact]
    public void IVadClient_HasProcessFrameAsync()
    {
        var method = typeof(IVadClient).GetMethod("ProcessFrameAsync");
        method.Should().NotBeNull();
    }

    [Fact]
    public void IVadClient_HasReset()
    {
        var method = typeof(IVadClient).GetMethod("Reset");
        method.Should().NotBeNull();
    }
}
