using global::Whisper.net;

namespace ElBruno.Speech.Stt.Whisper;

internal sealed class NativeWhisper : IDisposable
{
    private readonly WhisperFactory _factory;
    private readonly int _threads;

    internal NativeWhisper(WhisperOptions options)
    {
        WhisperModel.Verify(options.ModelPath);
        _factory = WhisperFactory.FromPath(options.ModelPath, new WhisperFactoryOptions { UseGpu = false });
        _threads = options.Threads;
    }

    internal IReadOnlyList<WhisperSegment> Recognize(float[] samples, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var segments = new List<WhisperSegment>();
        using var processor = _factory.CreateBuilder().WithLanguage("auto").WithThreads(_threads)
            .WithNoContext().WithTemperature(0).WithTemperatureInc(0)
            .WithEncoderBeginHandler(_ => !cancellationToken.IsCancellationRequested)
            .WithSegmentEventHandler(segment => segments.Add(new(
                segment.Text, segment.Start, segment.End, segment.Language)))
            .Build();
        try
        {
            // Keep native work joined before releasing the client gate or disposing its factory.
            processor.Process(samples);
        }
        catch (WhisperProcessingException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return segments;
    }

    public void Dispose() => _factory.Dispose();
}
