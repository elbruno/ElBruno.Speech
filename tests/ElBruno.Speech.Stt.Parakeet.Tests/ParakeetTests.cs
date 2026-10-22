using System.Buffers.Binary;
using System.Security.Cryptography;
using ElBruno.Speech.Stt.Parakeet;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ElBruno.Speech.Stt.Parakeet.Tests;

public class ParakeetTests
{
    [Fact]
    public async Task RawPcmIsNormalizedAndTokenStartsKeepChunkOffsets()
    {
        int calls = 0;
        using var client = new ParakeetSpeechToTextClient(new() { ChunkSeconds = 1 }, (samples, _) =>
        {
            Assert.Equal(16000, samples.Length);
            Assert.Equal(-1f, samples[0]);
            calls++;
            return [new("\u2581Hola", TimeSpan.FromSeconds(0.16))];
        });
        byte[] audio = new byte[64000];
        BinaryPrimitives.WriteInt16LittleEndian(audio, short.MinValue);
        BinaryPrimitives.WriteInt16LittleEndian(audio.AsSpan(32000), short.MinValue);
        using var input = new MemoryStream(audio);
        var response = await client.GetTextAsync(input);
        var transcript = Assert.IsType<ParakeetTranscript>(response.RawRepresentation);
        Assert.Equal("Hola Hola", response.Text);
        Assert.Equal(2, calls);
        Assert.Equal(TimeSpan.FromSeconds(1.16), transcript.Tokens[1].Start);
        Assert.Null(transcript.DetectedLanguage);
        Assert.False(transcript.HasWordIntervals);
        Assert.False(transcript.HasSegmentIntervals);
        Assert.Null(response.StartTime);
        Assert.Null(response.EndTime);
        Assert.True(input.CanRead);
    }

    [Fact]
    public async Task EmptyInputReturnsEmptyTranscriptWithoutInference()
    {
        using var client = new ParakeetSpeechToTextClient(new(), (_, _) => throw new InvalidOperationException());
        var response = await client.GetTextAsync(new MemoryStream());
        Assert.Empty(response.Text);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(101)]
    public async Task RejectsIncompleteSamplesAndOversizedInput(int size)
    {
        using var client = new ParakeetSpeechToTextClient(new() { MaxInputBytes = 100 }, (_, _) => []);
        await Assert.ThrowsAsync<InvalidDataException>(() => client.GetTextAsync(new MemoryStream(new byte[size])));
    }

    [Fact]
    public async Task RejectsInvalidNativeTimestamps()
    {
        using var client = new ParakeetSpeechToTextClient(new(), (_, _) => [new("bad", TimeSpan.FromSeconds(1.16))]);
        await Assert.ThrowsAsync<InvalidDataException>(() => client.GetTextAsync(new MemoryStream(new byte[32000])));
    }

    [Fact]
    public async Task NativeChunkPaddingStartsArePreservedAndExplicitlyFlagged()
    {
        using var client = new ParakeetSpeechToTextClient(new() { ChunkSeconds = 1 },
            (_, _) => [new("\u2581Hola", TimeSpan.FromSeconds(1.04))]);
        var response = await client.GetTextAsync(new MemoryStream(new byte[32000]));
        var token = Assert.Single(Assert.IsType<ParakeetTranscript>(response.RawRepresentation).Tokens);
        Assert.Equal(TimeSpan.FromSeconds(1.04), token.Start);
        Assert.True(token.IsInPadding);
        Assert.Equal("Hola", response.Text);
    }

    [Fact]
    public async Task RejectsLanguageHintsTranslationAndModelOverrides()
    {
        using var client = new ParakeetSpeechToTextClient(new(), (_, _) => []);
        foreach (var options in new SpeechToTextOptions[]
        {
            new() { SpeechLanguage = "es" }, new() { TextLanguage = "en" },
            new() { ModelId = "parakeet-v2" }, new() { SpeechSampleRate = 48000 }
        })
            await Assert.ThrowsAsync<NotSupportedException>(() => client.GetTextAsync(new MemoryStream(), options));
    }

    [Fact]
    public async Task CancellationAndDisposalAreObserved()
    {
        var client = new ParakeetSpeechToTextClient(new(), (_, _) => []);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetTextAsync(new MemoryStream(), cancellationToken: cts.Token));
        client.Dispose();
        client.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.GetTextAsync(new MemoryStream()));
    }

    [Fact]
    public async Task StreamingProducesFinalTextWithNativeResult()
    {
        using var client = new ParakeetSpeechToTextClient(new(), (_, _) => [new("\u2581Hola", TimeSpan.Zero)]);
        var updates = new List<SpeechToTextResponseUpdate>();
        await foreach (var update in client.GetStreamingTextAsync(new MemoryStream(new byte[32000])))
            updates.Add(update);
        Assert.Equal("Hola", string.Concat(updates.Select(update => update.Text)));
        Assert.Contains(updates, update => update.RawRepresentation is ParakeetTranscript);
    }

    [Fact]
    public async Task CancellationDuringInferenceReleasesTheClientForReuse()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        using var client = new ParakeetSpeechToTextClient(new(), (_, token) =>
        {
            if (++calls == 1)
            {
                started.SetResult();
                token.WaitHandle.WaitOne(TimeSpan.FromSeconds(5));
                token.ThrowIfCancellationRequested();
                throw new TimeoutException("The test did not cancel inference.");
            }
            return [new("\u2581Hola", TimeSpan.Zero)];
        });
        using var cancellation = new CancellationTokenSource();
        var pending = client.GetTextAsync(new MemoryStream(new byte[32000]),
            cancellationToken: cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        var next = await client.GetTextAsync(new MemoryStream(new byte[32000]));
        Assert.Equal("Hola", next.Text);
    }

    [Fact]
    public void TdtBlankDoesNotCommitStateAndPositiveDurationAdvancesFrames()
    {
        int calls = 0;
        var tokens = TdtDecoder.Decode(4, 3, (frame, previous, state1, _) =>
        {
            calls++;
            if (calls == 1)
            {
                Assert.Equal(0, frame);
                return Step(token: 2, duration: 2, state: 99);
            }
            Assert.Equal(2, frame);
            Assert.Equal(2, previous);
            Assert.Equal(0, state1[0]);
            return Step(token: 0, duration: 2, state: 42);
        }, default);
        Assert.Equal(2, calls);
        Assert.Equal(new DecodedToken(0, 2), Assert.Single(tokens));
    }

    [Fact]
    public void TdtNonBlankCommitsStateAndBoundsZeroDurationLoops()
    {
        int calls = 0;
        var tokens = TdtDecoder.Decode(2, 3, (frame, previous, state1, _) =>
        {
            Assert.Equal(calls / 10, frame);
            Assert.Equal(calls == 0 ? 2 : 0, previous);
            Assert.Equal(calls, state1[0]);
            return Step(0, 0, ++calls);
        }, default);
        Assert.Equal(20, tokens.Count);
        Assert.Equal(20, calls);
    }

    [Fact]
    public void TdtRejectsInvalidLogitsAndCancellation()
    {
        Assert.Throws<InvalidDataException>(() => TdtDecoder.Decode(1, 3,
            (_, _, _, _) => new([float.NaN], [], []), default));
        Assert.Throws<InvalidDataException>(() => TdtDecoder.Decode(1, 3,
            (_, _, _, _) => new([float.NaN, 0, 1, 0, 1, 0, 0, 0], [], []), default));
        Assert.Throws<OperationCanceledException>(() => TdtDecoder.Decode(1, 3,
            (_, _, _, _) => Step(0, 1, 0), new CancellationToken(true)));
    }

    [Fact]
    public void UnicodeDetokenizationPreservesWordsAndPunctuation()
    {
        Assert.Equal("Hola, pa\u00eds!", ParakeetSpeechToTextClient.DecodeText(
            [new("\u2581Hola", TimeSpan.Zero), new(",", TimeSpan.Zero),
             new("\u2581pa", TimeSpan.Zero), new("\u00eds!", TimeSpan.Zero)]));
    }

    [Fact]
    public void WavValidationRejectsMalformedAndWrongFormatAudio()
    {
        var wav = Wav();
        Assert.Equal(2, ParakeetSpeechToTextClient.ReadPcm(wav, ParakeetInputFormat.Wav).Length);
        Assert.Throws<InvalidDataException>(() => ParakeetSpeechToTextClient.ReadPcm(wav, ParakeetInputFormat.RawPcm16));
        foreach (int offset in new[] { 20, 22, 24, 28, 32, 34, 40 })
        {
            var corrupt = wav.ToArray();
            corrupt[offset] ^= 0xFF;
            Assert.Throws<InvalidDataException>(() => ParakeetSpeechToTextClient.ReadPcm(corrupt, ParakeetInputFormat.Wav));
        }
        BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(16), -1);
        Assert.Throws<InvalidDataException>(() => ParakeetSpeechToTextClient.ReadPcm(wav, ParakeetInputFormat.Wav));
    }

    [Fact]
    public void ModelVerificationRejectsSameSizeCorruption()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, [1, 2]);
            var asset = new ModelAsset("fixture", 2, Convert.ToHexString(SHA256.HashData(new byte[] { 1, 2 })));
            ParakeetModel.VerifyAsset(path, asset);
            File.WriteAllBytes(path, [2, 1]);
            Assert.Throws<InvalidDataException>(() => ParakeetModel.VerifyAsset(path, asset));
        }

        finally { File.Delete(path); }
    }

    [Fact]
    public async Task DownloadObservesCancellationAndRejectsCorruptExistingAssets()
    {
        string directory = Path.Combine(Path.GetTempPath(), "parakeet-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string config = Path.Combine(directory, "config.json");
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                ParakeetModel.DownloadAsync(directory, new CancellationToken(true)));
            File.WriteAllText(config, "corrupt");
            await Assert.ThrowsAsync<InvalidDataException>(() => ParakeetModel.DownloadAsync(directory));
        }
        finally
        {
            if (File.Exists(config)) File.Delete(config);
            Directory.Delete(directory);
        }
    }

    [Fact]
    public void RegistrationIsOptInAndDoesNotLoadOrDownloadModels()
    {
        var services = new ServiceCollection();
        services.AddParakeetSpeechToText(new() { ModelDirectory = "not-downloaded" });
        Assert.Contains(services, service => service.ServiceType == typeof(ISpeechToTextClient));
        using var client = new ParakeetSpeechToTextClient(new(), (_, _) => []);
        Assert.Same(client, client.GetService(typeof(ISpeechToTextClient)));
        Assert.Same(client.Metadata, client.GetService(typeof(SpeechToTextClientMetadata)));
        Assert.Null(client.GetService(typeof(ISpeechToTextClient), "other"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public void OptionsValidateChunkLimits(int seconds) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ParakeetSpeechToTextClient(new() { ChunkSeconds = seconds }, (_, _) => []));

    private static DecoderStep Step(int token, int duration, int state)
    {
        float[] logits = new float[8];
        logits[token] = 1;
        logits[3 + duration] = 1;
        return new(logits, [state], [state]);
    }

    private static byte[] Wav()
    {
        byte[] wav = new byte[46];
        "RIFF"u8.CopyTo(wav);
        BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(4), 38);
        "WAVEfmt "u8.CopyTo(wav.AsSpan(8));
        BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(16), 16);
        BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(20), 1);
        BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(22), 1);
        BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(24), 16000);
        BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(28), 32000);
        BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(32), 2);
        BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(34), 16);
        "data"u8.CopyTo(wav.AsSpan(36));
        BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(40), 2);
        return wav;
    }
}
