using BenchmarkDotNet.Attributes;
using ElBruno.Speech.Audio;
using System.Buffers;

namespace ElBruno.Speech.Benchmarks;

[MemoryDiagnoser]
public class ResamplerBenchmark
{
    private byte[] _data48k = null!;

    [GlobalSetup]
    public void Setup()
    {
        // 1 second of 48 kHz mono Int16
        _data48k = new byte[48_000 * 2];
        new Random(0).NextBytes(_data48k);
    }

    [Benchmark]
    public int ResampleFrom48Khz()
    {
        var (buf, result) = AudioResampler.ResampleTo16Khz(_data48k, 48_000);
        int len = result.Length;
        if (buf is not null) ArrayPool<byte>.Shared.Return(buf);
        return len;
    }
}
