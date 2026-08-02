using System.Buffers;

namespace ElBruno.Speech.Audio;

/// <summary>
/// A single-producer/single-consumer ring buffer for PCM bytes,
/// backed by <see cref="ArrayPool{T}.Shared"/> to avoid heap pressure.
/// </summary>
/// <remarks>
/// Ownership: the buffer is rented from <see cref="ArrayPool{T}.Shared"/> on construction
/// and returned in <see cref="Dispose"/>. Do not use the buffer after disposal.
/// </remarks>
public sealed class AudioRingBuffer : IDisposable
{
    private readonly byte[] _buffer;
    private readonly int _capacity;
    private int _readPos;
    private int _writePos;
    private int _available;
    private bool _disposed;

    /// <param name="capacityBytes">Maximum bytes the buffer can hold.</param>
    public AudioRingBuffer(int capacityBytes)
    {
        if (capacityBytes <= 0) throw new ArgumentOutOfRangeException(nameof(capacityBytes));
        _capacity = capacityBytes;
        _buffer = ArrayPool<byte>.Shared.Rent(capacityBytes);
    }

    /// <summary>Bytes currently available to read.</summary>
    public int Available => _available;

    /// <summary>Bytes of free space remaining.</summary>
    public int FreeSpace => _capacity - _available;

    /// <summary>Writes bytes into the ring buffer. Returns false if there is insufficient space.</summary>
    public bool TryWrite(ReadOnlySpan<byte> data)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (data.Length > FreeSpace) return false;

        int firstChunk = Math.Min(data.Length, _capacity - _writePos);
        data.Slice(0, firstChunk).CopyTo(_buffer.AsSpan(_writePos));
        if (firstChunk < data.Length)
            data.Slice(firstChunk).CopyTo(_buffer.AsSpan(0));

        _writePos = (_writePos + data.Length) % _capacity;
        _available += data.Length;
        return true;
    }

    /// <summary>Reads up to <paramref name="destination"/> bytes from the ring buffer.</summary>
    public int TryRead(Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        int count = Math.Min(destination.Length, _available);
        if (count == 0) return 0;

        int firstChunk = Math.Min(count, _capacity - _readPos);
        _buffer.AsSpan(_readPos, firstChunk).CopyTo(destination);
        if (firstChunk < count)
            _buffer.AsSpan(0, count - firstChunk).CopyTo(destination.Slice(firstChunk));

        _readPos = (_readPos + count) % _capacity;
        _available -= count;
        return count;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ArrayPool<byte>.Shared.Return(_buffer);
    }
}
