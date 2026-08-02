using System.Buffers;

namespace ElBruno.Speech.Audio;

/// <summary>
/// An <see cref="IDisposable"/> wrapper around a pooled byte array.
/// Ownership is transferred to the caller of the factory method; call <see cref="Dispose"/>
/// exactly once when the buffer is no longer needed to return it to the pool.
/// </summary>
public sealed class PooledAudioBuffer : IDisposable
{
    private byte[]? _buffer;
    private bool _disposed;

    /// <param name="sizeHint">Minimum number of bytes required.</param>
    public PooledAudioBuffer(int sizeHint)
    {
        if (sizeHint <= 0) throw new ArgumentOutOfRangeException(nameof(sizeHint));
        _buffer = ArrayPool<byte>.Shared.Rent(sizeHint);
        Length = sizeHint;
    }

    /// <summary>The usable portion of the underlying pooled array.</summary>
    public Span<byte> Span => _buffer.AsSpan(0, Length);

    /// <summary>The usable portion as a <see cref="Memory{T}"/>.</summary>
    public Memory<byte> Memory => _buffer.AsMemory(0, Length);

    /// <summary>Logical length of the usable portion (may be less than the rented array length).</summary>
    public int Length { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_buffer is not null)
        {
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = null;
        }
    }
}
