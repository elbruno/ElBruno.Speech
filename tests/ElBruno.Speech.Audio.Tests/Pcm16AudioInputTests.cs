using System.Buffers.Binary;
using Xunit;

namespace ElBruno.Speech.Audio.Tests;

public class Pcm16AudioInputTests
{
    [Fact]
    public async Task BoundedReadKeepsCallerStreamOpenAndUnwrapsWav()
    {
        using var wav = new MemoryStream();
        WavWriter.Write(wav, AudioFormat.Pcm16KhzMono, new byte[] { 1, 2 });
        wav.Position = 0;
        Assert.Equal(new byte[] { 1, 2 }, await Pcm16AudioInput.ReadAsync(wav, Pcm16InputFormat.Wav, 100));
        Assert.True(wav.CanRead);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(101)]
    public async Task RejectsOddSamplesAndOversizedStreams(int count) =>
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            Pcm16AudioInput.ReadAsync(new MemoryStream(new byte[count]), Pcm16InputFormat.RawPcm16, 100));

    [Fact]
    public async Task ObservesCancellationAndRejectsInvalidOptions()
    {
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Pcm16AudioInput.ReadAsync(new MemoryStream(), Pcm16InputFormat.RawPcm16, 100, new(true)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            Pcm16AudioInput.ReadAsync(new MemoryStream(), Pcm16InputFormat.RawPcm16, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Pcm16AudioInput.Parse([], (Pcm16InputFormat)99));
    }

    [Fact]
    public void StrictWavParsingRejectsWrongRateDuplicateDataAndTrailingBytes()
    {
        using var source = new MemoryStream();
        WavWriter.Write(source, AudioFormat.Pcm16KhzMono, new byte[] { 1, 2 });
        byte[] wav = source.ToArray();
        Assert.Throws<InvalidDataException>(() => Pcm16AudioInput.Parse(wav, Pcm16InputFormat.RawPcm16));
        var wrongRate = wav.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(wrongRate.AsSpan(24), 48000);
        Assert.Throws<InvalidDataException>(() => Pcm16AudioInput.Parse(wrongRate, Pcm16InputFormat.Wav));
        var duplicate = wav.Concat("data"u8.ToArray()).Concat(new byte[4]).ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(duplicate.AsSpan(4), duplicate.Length - 8);
        Assert.Throws<InvalidDataException>(() => Pcm16AudioInput.Parse(duplicate, Pcm16InputFormat.Wav));
        var trailing = wav.Append((byte)1).ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(trailing.AsSpan(4), trailing.Length - 8);
        Assert.Throws<InvalidDataException>(() => Pcm16AudioInput.Parse(trailing, Pcm16InputFormat.Wav));
    }
}
