using System.Buffers;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using ElBruno.Speech;
using ElBruno.Speech.Audio;

namespace ElBruno.Speech.Vad.Silero;

/// <summary>
/// Voice activity detector backed by the Silero VAD v4 ONNX model.
/// One instance = one VAD session with independent recurrent state (h, c tensors).
/// The underlying <see cref="InferenceSession"/> is shared across instances and must
/// not be disposed by this class.
/// </summary>
public sealed class SileroVadClient : IVadClient
{
    // Silero VAD v4 @ 16 kHz: exactly 512 samples per inference call
    private const int SamplesPerChunk = 512;
    private const int BytesPerChunk = SamplesPerChunk * 2; // Int16
    private const int TargetSampleRate = 16_000;

    private readonly InferenceSession _session;
    private readonly VadOptions _options;

    // Per-session recurrent state (h and c: shape [2, 1, 64])
    private readonly float[] _h = new float[2 * 1 * 64];
    private readonly float[] _c = new float[2 * 1 * 64];

    // Accumulator for incoming frame bytes (SPSC - single call site only)
    private readonly AudioRingBuffer _ringBuffer;

    // Chunk scratch buffer (rented once, reused every inference call)
    private readonly float[] _chunkFloat = new float[SamplesPerChunk];

    // Hysteresis counters
    private int _consecutiveSpeechFrames;
    private int _consecutiveSilenceFrames;
    private VoiceActivityState _currentState = VoiceActivityState.Silence;
    private float _lastConfidence;

    private bool _disposed;

    internal SileroVadClient(InferenceSession session, VadOptions options)
    {
        _session = session;
        _options = options;
        // Buffer up to 4 chunks (128 ms) to handle bursts
        _ringBuffer = new AudioRingBuffer(BytesPerChunk * 4);
    }

    // Test-only constructor — session is null, inference will throw but format validation runs first
    internal SileroVadClient(VadOptions options)
        : this(null!, options)
    {
    }

    /// <inheritdoc/>
    public ValueTask<VoiceActivityResult> ProcessFrameAsync(
        AudioFrame frame, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (frame.Format.SampleRate != TargetSampleRate)
            throw new SpeechPipelineException(
                $"SileroVadClient requires 16 kHz audio; got {frame.Format.SampleRate} Hz. Resample first.");
        if (frame.Format.SampleFormat != AudioSampleFormat.Int16)
            throw new SpeechPipelineException(
                $"SileroVadClient requires Int16 PCM; got {frame.Format.SampleFormat}. Convert first.");
        if (frame.Format.Channels != 1)
            throw new SpeechPipelineException(
                $"SileroVadClient requires mono audio; got {frame.Format.Channels} channels. Downmix first.");

        // Write frame bytes into ring buffer (ignore overflow — drop oldest if ring is full)
        _ringBuffer.TryWrite(frame.Data.Span);

        // Process all complete 512-sample chunks
        Span<byte> chunk = stackalloc byte[BytesPerChunk];
        while (_ringBuffer.Available >= BytesPerChunk)
        {
            int read = _ringBuffer.TryRead(chunk);
            if (read < BytesPerChunk) break;

            float prob = RunInference(chunk);
            UpdateState(prob);
        }

        return ValueTask.FromResult(new VoiceActivityResult(_currentState, _lastConfidence, frame));
    }

    private float RunInference(ReadOnlySpan<byte> int16Bytes)
    {
        // Convert Int16 → Float32 in-place, no allocation
        PcmConverter.Int16ToFloat32(int16Bytes, _chunkFloat);

        var inputTensor = new DenseTensor<float>(_chunkFloat.AsMemory(0, SamplesPerChunk), new[] { 1, SamplesPerChunk });
        var srTensor = new DenseTensor<long>(new long[] { TargetSampleRate }.AsMemory(), new[] { 1 });
        var hTensor = new DenseTensor<float>(_h.AsMemory(), new[] { 2, 1, 64 });
        var cTensor = new DenseTensor<float>(_c.AsMemory(), new[] { 2, 1, 64 });

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input", inputTensor),
            NamedOnnxValue.CreateFromTensor("sr", srTensor),
            NamedOnnxValue.CreateFromTensor("h", hTensor),
            NamedOnnxValue.CreateFromTensor("c", cTensor),
        };

        using var results = _session.Run(inputs);

        float prob = results.First(r => r.Name == "output")
            .AsEnumerable<float>().First();

        // Update recurrent state in-place
        var hn = results.First(r => r.Name == "hn").AsEnumerable<float>().ToArray();
        var cn = results.First(r => r.Name == "cn").AsEnumerable<float>().ToArray();
        hn.CopyTo(_h, 0);
        cn.CopyTo(_c, 0);

        _lastConfidence = prob;
        return prob;
    }

    private void UpdateState(float prob)
    {
        if (prob >= _options.Threshold)
        {
            _consecutiveSpeechFrames++;
            _consecutiveSilenceFrames = 0;
            if (_consecutiveSpeechFrames >= _options.MinSpeechFrames)
                _currentState = VoiceActivityState.Speech;
        }
        else
        {
            _consecutiveSilenceFrames++;
            _consecutiveSpeechFrames = 0;
            if (_consecutiveSilenceFrames >= _options.MinSilenceFrames)
                _currentState = VoiceActivityState.Silence;
        }
    }

    /// <inheritdoc/>
    public void Reset()
    {
        Array.Clear(_h);
        Array.Clear(_c);
        _consecutiveSpeechFrames = 0;
        _consecutiveSilenceFrames = 0;
        _currentState = VoiceActivityState.Silence;
        _lastConfidence = 0f;
        // Drain the ring buffer
        Span<byte> drain = stackalloc byte[BytesPerChunk];
        while (_ringBuffer.Available > 0)
            _ringBuffer.TryRead(drain);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;
        _ringBuffer.Dispose();
        // Do NOT dispose _session — it is shared and owned by the factory
        return ValueTask.CompletedTask;
    }
}
