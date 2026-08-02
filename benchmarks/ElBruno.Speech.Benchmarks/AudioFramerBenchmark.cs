using BenchmarkDotNet.Attributes;
using ElBruno.Speech;
using ElBruno.Speech.Audio;

namespace ElBruno.Speech.Benchmarks;

[MemoryDiagnoser]
public class AudioFramerBenchmark
{
    private byte[] _data = null!;
    private AudioFramer _framer = null!;

    [GlobalSetup]
    public void Setup()
    {
        var format = AudioFormat.Pcm16KhzMono;
        _framer = new AudioFramer(format, frameDurationMs: 20);
        // 10 seconds of audio
        _data = new byte[format.BytesPerSecond * 10];
    }

    [Benchmark]
    public int FrameAndCount()
    {
        _framer.Reset();
        int count = 0;
        foreach (var _ in _framer.Frame(_data))
            count++;
        return count;
    }
}
