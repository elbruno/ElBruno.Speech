using System.Buffers.Binary;
using ElBruno.Speech.Audio;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ElBruno.Speech.Stt.Whisper.Tests;

public class WhisperTests
{
    [Fact]
    public async Task NormalizesPcmAndPreservesNativeSegmentsWithoutInventingWordAlignment()
    {
        var options = new WhisperOptions();
        using var client = new WhisperSpeechToTextClient(options, (samples, _) =>
        {
            Assert.Equal(-1, samples[0]);
            Assert.Equal(0.5f, samples[1]);
            Assert.Equal(16000, samples.Length);
            return [new(" Hola", TimeSpan.Zero, TimeSpan.FromSeconds(1.04), "es")];
        });
        options.InputFormat = Pcm16InputFormat.Wav;
        byte[] audio = new byte[32000];
        BinaryPrimitives.WriteInt16LittleEndian(audio, short.MinValue);
        BinaryPrimitives.WriteInt16LittleEndian(audio.AsSpan(2), 16384);
        using var input = new MemoryStream(audio);
        var response = await client.GetTextAsync(input);
        var transcript = Assert.IsType<WhisperTranscript>(response.RawRepresentation);
        Assert.Equal("Hola", response.Text);
        Assert.Equal(WhisperModel.ModelId, response.ModelId);
        Assert.Equal("es", transcript.DetectedLanguage);
        Assert.True(transcript.HasSegmentIntervals);
        Assert.False(transcript.HasWordIntervals);
        Assert.Contains("unreviewed", transcript.TimingProvenance);
        var segment = Assert.Single(transcript.Segments);
        Assert.Equal(TimeSpan.FromSeconds(1.04), segment.End);
        Assert.True(segment.ExtendsPastInput);
        Assert.Null(response.StartTime);
        Assert.Null(response.EndTime);
        Assert.True(input.CanRead);
    }

    [Fact]
    public async Task AcceptsStrictWavAndRejectsWrappedAudioWhenConfiguredRaw()
    {
        using var wav = new MemoryStream();
        WavWriter.Write(wav, AudioFormat.Pcm16KhzMono, new byte[32000]);
        using var wavClient = new WhisperSpeechToTextClient(new() { InputFormat = Pcm16InputFormat.Wav },
            (samples, _) => { Assert.Equal(16000, samples.Length); return []; });
        wav.Position = 0;
        await wavClient.GetTextAsync(wav);
        using var rawClient = new WhisperSpeechToTextClient(new(), (_, _) => []);
        wav.Position = 0;
        await Assert.ThrowsAsync<InvalidDataException>(() => rawClient.GetTextAsync(wav));
    }

    [Fact]
    public async Task EmptyInputDoesNotRunNativeInference()
    {
        using var client = new WhisperSpeechToTextClient(new(), (_, _) => throw new InvalidOperationException());
        var response = await client.GetTextAsync(new MemoryStream());
        Assert.Empty(response.Text);
        Assert.Empty(Assert.IsType<WhisperTranscript>(response.RawRepresentation).Segments);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(101)]
    public async Task BoundsInputAndRejectsIncompleteSamples(int bytes)
    {
        using var client = new WhisperSpeechToTextClient(new() { MaxInputBytes = 100 }, (_, _) => []);
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            client.GetTextAsync(new MemoryStream(new byte[bytes])));
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(2, 1)]
    public async Task RejectsInvalidNativeIntervals(double start, double end)
    {
        using var client = new WhisperSpeechToTextClient(new(), (_, _) =>
            [new("text", TimeSpan.FromSeconds(start), TimeSpan.FromSeconds(end), "en")]);
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            client.GetTextAsync(new MemoryStream(new byte[32000])));
    }

    [Fact]
    public async Task RejectsUnsupportedRequestOverrides()
    {
        using var client = new WhisperSpeechToTextClient(new(), (_, _) => []);
        foreach (var options in new SpeechToTextOptions[]
        {
            new() { ModelId = "tiny" }, new() { SpeechSampleRate = 48000 },
            new() { SpeechLanguage = "es" }, new() { TextLanguage = "en" }
        })
            await Assert.ThrowsAsync<NotSupportedException>(() => client.GetTextAsync(new MemoryStream(), options));
    }

    [Fact]
    public async Task StreamingIsFinalOnlyAndIncludesNativeTranscript()
    {
        using var client = new WhisperSpeechToTextClient(new(), (_, _) => [new("hello", TimeSpan.Zero, TimeSpan.Zero, "en")]);
        var updates = new List<SpeechToTextResponseUpdate>();
        await foreach (var update in client.GetStreamingTextAsync(new MemoryStream(new byte[32000]))) updates.Add(update);
        Assert.Equal("hello", string.Concat(updates.Select(u => u.Text)));
        Assert.Contains(updates, u => u.RawRepresentation is WhisperTranscript);
    }

    [Fact]
    public async Task NativeFailuresPropagateAndReleaseClientForReuse()
    {
        int calls = 0;
        using var client = new WhisperSpeechToTextClient(new(), (_, _) =>
            ++calls == 1 ? throw new InvalidOperationException("native failure") : []);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetTextAsync(new MemoryStream(new byte[32000])));
        Assert.Equal("native failure", error.Message);
        Assert.Empty((await client.GetTextAsync(new MemoryStream(new byte[32000]))).Text);
    }

    [Fact]
    public async Task CancellationDoesNotReleaseGateUntilNativeWorkFinishes()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        var backend = new TrackingBackend();
        var client = new WhisperSpeechToTextClient(new(), (_, _) =>
        {
            started.SetResult();
            finish.Task.Wait(TimeSpan.FromSeconds(10));
            return [];
        }, backend);
        var inference = client.GetTextAsync(new MemoryStream(new byte[32000]), cancellationToken: cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        using var queuedCancellation = new CancellationTokenSource();
        var queued = client.GetTextAsync(new MemoryStream(), cancellationToken: queuedCancellation.Token);
        queuedCancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
        Assert.False(inference.IsCompleted);
        var disposing = Task.Run(client.Dispose);
        try { Assert.False(backend.Disposed); }
        finally { finish.SetResult(); }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => inference);
        await disposing.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(backend.Disposed);
        client.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.GetTextAsync(new MemoryStream()));
    }

    [Fact]
    public void RegistrationAndMetadataDoNotDownloadOrLoadModels()
    {
        var services = new ServiceCollection();
        services.AddWhisperLargeV3SpeechToText(new() { ModelPath = "not-downloaded.bin" });
        Assert.Contains(services, s => s.ServiceType == typeof(ISpeechToTextClient));
        using var client = new WhisperSpeechToTextClient(new(), (_, _) => []);
        Assert.Same(client, client.GetService(typeof(ISpeechToTextClient)));
        Assert.Same(client.Metadata, client.GetService(typeof(SpeechToTextClientMetadata)));
        Assert.Null(client.GetService(typeof(ISpeechToTextClient), "other"));
        Assert.Null(client.GetService(typeof(Stream)));
        Assert.Throws<FileNotFoundException>(() => new WhisperSpeechToTextClient(new() { ModelPath = "not-downloaded.bin" }));
    }

    [Fact]
    public async Task DownloadRejectsCorruptionWithoutNetworkAndObservesCancellation()
    {
        string path = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(path, [1, 2]);
            Assert.Throws<InvalidDataException>(() => WhisperModel.Verify(path));
            await Assert.ThrowsAsync<InvalidDataException>(() => WhisperModel.DownloadAsync(path));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => WhisperModel.DownloadAsync(path, new(true)));
            Assert.Equal(new byte[] { 1, 2 }, await File.ReadAllBytesAsync(path));
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(129)]
    public void ValidatesThreadCount(int threads) => Assert.Throws<ArgumentOutOfRangeException>(() =>
        new WhisperSpeechToTextClient(new() { Threads = threads }, (_, _) => []));

    [Fact]
    public void ValidatesFormatSizeAndPath()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WhisperSpeechToTextClient(new() { MaxInputBytes = 0 }, (_, _) => []));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WhisperSpeechToTextClient(new() { InputFormat = (Pcm16InputFormat)99 }, (_, _) => []));
        Assert.Throws<ArgumentException>(() => new WhisperSpeechToTextClient(new() { ModelPath = "" }, (_, _) => []));
        Assert.Null(new WhisperTranscript("", [new("", TimeSpan.Zero, TimeSpan.Zero, "en"), new("", TimeSpan.Zero, TimeSpan.Zero, "es")]).DetectedLanguage);
    }

    private sealed class TrackingBackend : IDisposable
    {
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }
}
