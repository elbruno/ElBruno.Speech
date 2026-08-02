using ElBruno.Speech;
using ElBruno.Speech.Audio;
using FluentAssertions;

namespace ElBruno.Speech.Audio.Tests;

public sealed class WavReaderWriterTests : IDisposable
{
    private readonly string _tempPath = Path.GetTempFileName() + ".wav";

    [Fact]
    public void RoundTrip_Int16_16Khz_Mono()
    {
        var format = AudioFormat.Pcm16KhzMono;
        // 100 ms of silence: 16000 Hz * 1 ch * 2 bytes * 0.1 s = 3200 bytes
        var samples = new byte[3200];
        new Random(42).NextBytes(samples); // deterministic non-silent data

        WavWriter.Write(_tempPath, format, samples);
        var (readFormat, readSamples) = WavReader.Read(_tempPath);

        readFormat.Should().Be(format);
        readSamples.ToArray().Should().Equal(samples);
    }

    [Fact]
    public void ParseWav_InvalidRiff_ThrowsSpeechPipelineException()
    {
        var badBytes = new byte[44]; // all zeros
        var act = () => WavReader.ParseWav(badBytes);
        act.Should().Throw<SpeechPipelineException>();
    }

    [Fact]
    public void ParseWav_TooShort_ThrowsSpeechPipelineException()
    {
        var act = () => WavReader.ParseWav(new byte[10]);
        act.Should().Throw<SpeechPipelineException>();
    }

    public void Dispose()
    {
        if (File.Exists(_tempPath)) File.Delete(_tempPath);
    }
}
