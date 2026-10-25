using System.Net;
using System.Text.Json;
using ElBruno.Speech.Tts.Ollama;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ElBruno.Speech.Tts.Ollama.Tests;

public sealed class OllamaSpeechClientTests
{
    [Fact]
    public async Task SendsConfiguredRequestAndReturnsMediaTypedAudio()
    {
        HttpMethod? capturedMethod = null;
        string? capturedUri = null;
        string? capturedBody = null;
        var handler = new StubHandler(async (request, _) =>
        {
            capturedMethod = request.Method;
            capturedUri = request.RequestUri!.ToString();
            capturedBody = await request.Content!.ReadAsStringAsync();
            return AudioResponse([1, 2, 3]);
        });
        using var httpClient = new HttpClient(handler);
        using var client = new OllamaSpeechClient(httpClient, new()
        {
            Endpoint = new Uri("http://localhost:11434/"),
            Model = "phonon-custom",
            Voice = "Freya",
            AudioFormat = OllamaSpeechAudioFormat.Pcm
        });

        var response = await client.GetAudioAsync("Hello.");

        Assert.Equal(HttpMethod.Post, capturedMethod);
        Assert.Equal("http://localhost:11434/v1/audio/speech", capturedUri);
        using var payload = JsonDocument.Parse(capturedBody!);
        Assert.Equal("phonon-custom", payload.RootElement.GetProperty("model").GetString());
        Assert.Equal("Hello.", payload.RootElement.GetProperty("input").GetString());
        Assert.Equal("Freya", payload.RootElement.GetProperty("voice").GetString());
        Assert.Equal("pcm", payload.RootElement.GetProperty("response_format").GetString());
        Assert.Equal("phonon-custom", response.ModelId);
        Assert.Equal(new byte[] { 1, 2, 3 }, response.RawRepresentation);
        var content = Assert.IsType<DataContent>(Assert.Single(response.Contents));
        Assert.Equal("audio/pcm", content.MediaType);
        Assert.Equal(new byte[] { 1, 2, 3 }, content.Data.ToArray());
    }

    [Theory]
    [InlineData("http://localhost:11434", "http://localhost:11434/v1/audio/speech")]
    [InlineData("http://localhost:11434/", "http://localhost:11434/v1/audio/speech")]
    [InlineData("http://localhost:11434/v1", "http://localhost:11434/v1/audio/speech")]
    [InlineData("http://localhost:11434/v1/", "http://localhost:11434/v1/audio/speech")]
    [InlineData("http://localhost:11434/v1/audio", "http://localhost:11434/v1/audio/speech")]
    [InlineData("http://localhost:11434/v1/audio/", "http://localhost:11434/v1/audio/speech")]
    [InlineData("http://localhost:11434/v1/audio/speech", "http://localhost:11434/v1/audio/speech")]
    [InlineData("http://localhost:11434/v1/audio/speech/", "http://localhost:11434/v1/audio/speech")]
    [InlineData("https://speech.example:8443/proxy%20name/v1?region=west%2Feast", "https://speech.example:8443/proxy%20name/v1/audio/speech?region=west%2Feast")]
    [InlineData("https://speech.example/proxy/v1/", "https://speech.example/proxy/v1/audio/speech")]
    [InlineData("https://speech.example/proxy/v1/audio/", "https://speech.example/proxy/v1/audio/speech")]
    [InlineData("https://speech.example/proxy/v1/audio/speech", "https://speech.example/proxy/v1/audio/speech")]
    [InlineData("https://speech.example/?region=west", "https://speech.example/v1/audio/speech?region=west")]
    [InlineData("https://speech.example/proxy/v1/?region=west", "https://speech.example/proxy/v1/audio/speech?region=west")]
    [InlineData("https://speech.example/custom/synthesize?region=west", "https://speech.example/custom/synthesize?region=west")]
    [InlineData("https://speech.example/custom/", "https://speech.example/custom/")]
    [InlineData("https://speech.example/v10", "https://speech.example/v10")]
    public async Task ResolvesEndpointForCompleteAndStreamingRequests(string configured, string expected)
    {
        var requests = new List<Uri>();
        using var http = new HttpClient(new StubHandler((request, _) =>
        {
            requests.Add(request.RequestUri!);
            return Task.FromResult(AudioResponse([1, 2]));
        }));
        var options = new OllamaSpeechOptions { Endpoint = new Uri(configured) };
        using var client = new OllamaSpeechClient(http, options);

        await client.GetAudioAsync("Hello.");
        await foreach (var _ in client.GetStreamingAudioAsync("Hello.")) { }

        Assert.Equal(new[] { new Uri(expected), new Uri(expected) }, requests);
        Assert.Equal(new Uri(configured), options.Endpoint);
    }

    [Fact]
    public async Task DefaultEndpointAndOptionsSnapshotUseResolvedSpeechRoute()
    {
        var requests = new List<Uri>();
        using var http = new HttpClient(new StubHandler((request, _) =>
        {
            requests.Add(request.RequestUri!);
            return Task.FromResult(AudioResponse([1, 2]));
        }));
        var options = new OllamaSpeechOptions();
        Assert.Equal(new Uri("http://localhost:11434/"), options.Endpoint);
        using var defaultClient = new OllamaSpeechClient(http);
        using var configuredClient = new OllamaSpeechClient(http, options);
        options.Endpoint = new Uri("https://different.example/");

        await defaultClient.GetAudioAsync("Hello.");
        await configuredClient.GetAudioAsync("Hello.");

        Assert.All(requests, uri => Assert.Equal(new Uri("http://localhost:11434/v1/audio/speech"), uri));
        Assert.Equal(2, requests.Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("/relative")]
    [InlineData("ftp://speech.example/")]
    [InlineData("file:///tmp/audio")]
    public void RejectsInvalidEndpointsBeforeSendingRequests(string? endpoint)
    {
        var options = new OllamaSpeechOptions
        {
            Endpoint = endpoint is null ? null! : new Uri(endpoint, UriKind.RelativeOrAbsolute)
        };
        Assert.ThrowsAny<ArgumentException>(() => new OllamaSpeechClient(options));
    }

    [Fact]
    public async Task OmitsOptionalVoiceAndAllowsPerRequestOverrides()
    {
        string? requestBody = null;
        var handler = new StubHandler(async (request, _) =>
        {
            requestBody = await request.Content!.ReadAsStringAsync();
            return AudioResponse([9]);
        });
        using var httpClient = new HttpClient(handler);
        using var client = new OllamaSpeechClient(httpClient);

        var response = await client.GetAudioAsync("Hi", new()
        {
            ModelId = "phonon-override",
            VoiceId = "Zoey",
            AudioFormat = "audio/wav"
        });

        using var payload = JsonDocument.Parse(requestBody!);
        Assert.Equal("phonon-override", payload.RootElement.GetProperty("model").GetString());
        Assert.Equal("Zoey", payload.RootElement.GetProperty("voice").GetString());
        Assert.Equal("wav", payload.RootElement.GetProperty("response_format").GetString());
        Assert.Equal("audio/wav", Assert.IsType<DataContent>(Assert.Single(response.Contents)).MediaType);

        await client.GetAudioAsync("Hi");
        using var defaultPayload = JsonDocument.Parse(requestBody!);
        Assert.False(defaultPayload.RootElement.TryGetProperty("voice", out _));
        Assert.Equal("phonon", defaultPayload.RootElement.GetProperty("model").GetString());
        Assert.Equal("wav", defaultPayload.RootElement.GetProperty("response_format").GetString());
    }

    [Fact]
    public async Task StreamingYieldsSeparateAudioUpdatesAsTheResponseIsRead()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new ChunkedStream([1, 2, 3, 4, 5, 6], 2))
            }));
        using var httpClient = new HttpClient(handler);
        using var client = new OllamaSpeechClient(httpClient);

        var updates = new List<TextToSpeechResponseUpdate>();
        await foreach (var update in client.GetStreamingAudioAsync("Hello."))
            updates.Add(update);

        Assert.Equal(3, updates.Count);
        Assert.All(updates, update =>
        {
            Assert.Equal(TextToSpeechResponseUpdateKind.AudioUpdated, update.Kind);
            Assert.Equal("phonon", update.ModelId);
            Assert.Equal("audio/wav", Assert.IsType<DataContent>(Assert.Single(update.Contents)).MediaType);
        });
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 },
            updates.SelectMany(update => Assert.IsType<byte[]>(update.RawRepresentation)).ToArray());
    }

    [Fact]
    public async Task StreamingReturnsFirstChunkBeforeEofAndDisposesOnEarlyExit()
    {
        var stream = new GatedStream();
        using var http = new HttpClient(new StubHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) })));
        using var client = new OllamaSpeechClient(http);
        await using (var updates = client.GetStreamingAudioAsync("Hello",
            new() { AudioFormat = "pcm" }).GetAsyncEnumerator())
        {
            Assert.True(await updates.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(new byte[] { 1, 2 }, Assert.IsType<DataContent>(Assert.Single(updates.Current.Contents)).Data.ToArray());
            Assert.False(stream.ReadToEnd);
            Assert.False(stream.Disposed);
        }
        Assert.True(stream.Disposed);
        Assert.False(stream.ReadToEnd);
    }

    [Fact]
    public async Task CancellationDuringStreamingReadPropagatesAndDisposesResponse()
    {
        var stream = new GatedStream();
        using var http = new HttpClient(new StubHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) })));
        using var client = new OllamaSpeechClient(http);
        using var cancellation = new CancellationTokenSource();
        await using (var updates = client.GetStreamingAudioAsync("Hello",
            cancellationToken: cancellation.Token).GetAsyncEnumerator())
        {
            Assert.True(await updates.MoveNextAsync());
            var next = updates.MoveNextAsync().AsTask();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => next);
        }
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task ReportsHttpStatusAndOllamaErrorBody()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("unknown voice")
        }));
        using var httpClient = new HttpClient(handler);
        using var client = new OllamaSpeechClient(httpClient);

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAudioAsync("Hello."));

        Assert.Equal(HttpStatusCode.BadRequest, error.StatusCode);
        Assert.Contains("unknown voice", error.Message);
        Assert.Contains("400", error.Message);
    }

    [Fact]
    public async Task BoundsErrorResponseReadingAndDisposesStream()
    {
        var stream = new CountingStream(new byte[10000]);
        using var http = new HttpClient(new StubHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StreamContent(stream) })));
        using var client = new OllamaSpeechClient(http);
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAudioAsync("Hello"));
        Assert.Equal(1025, stream.BytesRead);
        Assert.True(stream.Disposed);
        Assert.EndsWith("...", error.Message);
    }

    [Fact]
    public async Task RejectsUnsupportedOptionsAndTextOverLimit()
    {
        using var httpClient = new HttpClient(new StubHandler((_, _) =>
            Task.FromResult(AudioResponse([1]))));
        using var client = new OllamaSpeechClient(httpClient);

        await Assert.ThrowsAsync<NotSupportedException>(() => client.GetAudioAsync("Hi", new() { Speed = 1 }));
        await Assert.ThrowsAsync<NotSupportedException>(() => client.GetAudioAsync("Hi", new() { Language = "en" }));
        await Assert.ThrowsAsync<NotSupportedException>(() => client.GetAudioAsync("Hi", new() { AudioFormat = "mp3" }));
        await Assert.ThrowsAsync<NotSupportedException>(() => client.GetAudioAsync("Hi",
            new() { RawRepresentationFactory = _ => new object() }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.GetAudioAsync(new string('a', 4097)));

        var boundaryResponse = await client.GetAudioAsync(new string('a', 4096));
        Assert.Equal("phonon", boundaryResponse.ModelId);
    }

    [Fact]
    public async Task CancellationIsPassedToTheHttpRequest()
    {
        using var cancellation = new CancellationTokenSource();
        var requestStarted = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new StubHandler((_, token) =>
        {
            requestStarted.TrySetResult(token);
            return WaitForCancellation(token);
        });
        using var httpClient = new HttpClient(handler);
        using var client = new OllamaSpeechClient(httpClient);
        var request = client.GetAudioAsync("Hello.", cancellationToken: cancellation.Token);
        var sentToken = await requestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(sentToken.CanBeCanceled);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
    }

    private static async Task<HttpResponseMessage> WaitForCancellation(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken);
        throw new InvalidOperationException("Cancellation was not observed.");
    }

    [Fact]
    public void RegistersAsITextToSpeechClientAndValidatesConfiguration()
    {
        var services = new ServiceCollection();
        services.AddOllamaSpeech(new() { Model = "phonon-custom" });
        using var provider = services.BuildServiceProvider();
        using var client = Assert.IsType<OllamaSpeechClient>(provider.GetRequiredService<ITextToSpeechClient>());
        Assert.Equal("Ollama", client.Metadata.ProviderName);
        Assert.Equal("phonon-custom", client.Metadata.DefaultModelId);
        Assert.Same(client, client.GetService(typeof(ITextToSpeechClient)));
        Assert.Same(client.Metadata, client.GetService(typeof(TextToSpeechClientMetadata)));
        Assert.Null(client.GetService(typeof(ITextToSpeechClient), "other"));

        Assert.Throws<ArgumentException>(() => new OllamaSpeechClient(new OllamaSpeechOptions
        {
            Endpoint = new Uri("file:///tmp/audio")
        }));
        Assert.Throws<ArgumentException>(() => new OllamaSpeechClient(new OllamaSpeechOptions { Model = " " }));
    }

    private static HttpResponseMessage AudioResponse(byte[] bytes) =>
        new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }

    private sealed class ChunkedStream(byte[] bytes, int chunkSize) : MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(chunkSize, buffer.Length)], cancellationToken);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            base.ReadAsync(buffer, offset, Math.Min(chunkSize, count), cancellationToken);
    }

    private sealed class GatedStream : MemoryStream
    {
        private bool _first = true;
        public bool ReadToEnd { get; private set; }
        public bool Disposed { get; private set; }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_first)
            {
                _first = false;
                buffer.Span[0] = 1;
                buffer.Span[1] = 2;
                return 2;
            }
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            ReadToEnd = true;
            return 0;
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }

    }

    private sealed class CountingStream(byte[] data) : MemoryStream(data)
    {
        public int BytesRead { get; private set; }
        public bool Disposed { get; private set; }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int count = await base.ReadAsync(buffer, cancellationToken);
            BytesRead += count;
            return count;
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
