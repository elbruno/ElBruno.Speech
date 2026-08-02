using Microsoft.ML.OnnxRuntime;
using ElBruno.Speech;

namespace ElBruno.Speech.Vad.Silero;

/// <summary>
/// Creates <see cref="SileroVadClient"/> instances sharing a single ONNX
/// <see cref="InferenceSession"/>. Register as a singleton in DI.
/// </summary>
public sealed class SileroVadClientFactory : IAsyncDisposable
{
    private readonly VadOptions _options;
    private InferenceSession? _session;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _disposed;

    public SileroVadClientFactory(VadOptions? options = null)
    {
        _options = options ?? new VadOptions();
    }

    /// <summary>
    /// Ensures the ONNX session is initialized (downloading the model if needed)
    /// and returns a new <see cref="SileroVadClient"/> with independent recurrent state.
    /// </summary>
    public async Task<SileroVadClient> CreateAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_session is null)
        {
            await _initLock.WaitAsync(cancellationToken);
            try
            {
                if (_session is null)
                {
                    var modelPath = _options.ModelPath ?? ModelDownloader.DefaultModelPath;
                    await ModelDownloader.EnsureModelAsync(modelPath, cancellationToken);
                    _session = new InferenceSession(modelPath);
                }
            }
            finally
            {
                _initLock.Release();
            }
        }

        return new SileroVadClient(_session, _options);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;
        _session?.Dispose();
        _initLock.Dispose();
        return ValueTask.CompletedTask;
    }
}
