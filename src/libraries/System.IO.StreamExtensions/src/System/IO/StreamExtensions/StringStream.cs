// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace System.IO.StreamExtensions;

/// <summary>
/// Provides a read-only, non-seekable stream that encodes a string into bytes on-the-fly.
/// </summary>
public sealed class StringStream : Stream
{
    private readonly string _source;
    private readonly Encoder _encoder;
    private readonly Encoding _encoding;
    private int _position;
    private long? _cachedLength;
    private int _charPosition;
    private readonly byte[] _byteBuffer;
    private int _byteBufferCount;
    private int _byteBufferPosition;
    private bool _disposed;

    // Explicit flag to track if Position was manually changed
    private bool _needsResync;

    // For caching completed read tasks
    private Task<int>? _lastReadTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="StringStream"/> class with the specified source string using UTF-8 encoding.
    /// </summary>
    /// <param name="source">The string to read from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public StringStream(string source) // Default UTF8 encoding
        : this(source, Encoding.UTF8)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StringStream"/> class with the specified source string and encoding.
    /// </summary>
    /// <param name="source">The string to read from.</param>
    /// <param name="encoding">The encoding to use when converting the string to bytes.</param>
    /// <param name="bufferSize">The size of the internal buffer used for encoding. Default is 4096 bytes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public StringStream(string source, Encoding encoding, int bufferSize = 4096)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _encoder = (encoding ?? throw new ArgumentNullException(nameof(encoding))).GetEncoder();
        _encoding = encoding;
        _position = 0;
        _byteBuffer = new byte[bufferSize];
    }

    /// <inheritdoc/>
    public override bool CanRead => !_disposed;

    /// <inheritdoc/>
    public override bool CanSeek => !_disposed;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// Accessing this property for the first time requires encoding the entire source string
    /// to determine the byte count, which is an O(n) operation. The result is cached for
    /// subsequent accesses.
    /// </para>
    /// <para>
    /// If you are streaming to a destination that does not require knowing the length upfront
    /// (e.g., chunked HTTP transfer, file I/O), avoid accessing this property to maximize
    /// performance.  The stream will still encode data on-the-fly during read operations.
    /// </para>
    /// </remarks>
    public override long Length
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_cachedLength.HasValue)
            {
                _cachedLength = _encoding.GetByteCount(_source);
            }
            return _cachedLength.Value;
        }
    }

    /// <inheritdoc/>
    public override long Position
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _position;
        }
        set
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, int.MaxValue);

            int newPosition = (int)value;

            // Only flag resync if position manually changed
            if (_position != newPosition)
            {
                _position = newPosition;
                _needsResync = true;
            }
        }
    }

    /// <inheritdoc/>
    /// /// <remarks>
    /// <para>
    /// Encodes the source string on-the-fly in 1024-character chunks. If <see cref="Position"/>
    /// was modified (via setter or <see cref="Seek"/>), re-encodes from the beginning to reach
    /// the target byte position: an O(n) operation. This can be expensive for large strings and
    /// arbitrary seeks. For best performance, read sequentially without seeking/changing position manually.
    /// </para>
    /// </remarks>
    public override int Read(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);
        return Read(new Span<byte>(buffer, offset, count));
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// Core read implementation for both array and span overloads.
    /// </para>
    /// <para>
    /// Encodes the source string on-the-fly in 1024-character chunks. If <see cref="Position"/>
    /// was modified (via setter or <see cref="Seek"/>), re-encodes from the beginning to reach
    /// the target byte position: an O(n) operation. This can be expensive for large strings and
    /// arbitrary seeks. For best performance, read sequentially without seeking/changing position manually.
    /// </para>
    /// <param name="user_buffer">The span to read data into.</param>
    /// <returns>The number of bytes read, or zero if at end of stream.</returns>
    /// <exception cref="ObjectDisposedException">The stream is closed.</exception>
    /// </remarks>
    public override int Read(Span<byte> user_buffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_needsResync)
        {
            ResyncPosition();
            _needsResync = false; // Clear flag after resyncing
        }

        int totalBytesRead = 0;
        int count = user_buffer.Length;

        while (totalBytesRead < count) // Regular sequential read
        {
            if (_byteBufferPosition >= _byteBufferCount)
            {
                if (_charPosition >= _source.Length) break;

                int charsToEncode = Math.Min(1024, _source.Length - _charPosition);
                bool flush = _charPosition + charsToEncode >= _source.Length;

#if NET || NETCOREAPP
                _byteBufferCount = _encoder.GetBytes(
                    _source.AsSpan(_charPosition, charsToEncode),
                    _byteBuffer.AsSpan(),
                    flush);
#else
                char[] charBuffer = _source.ToCharArray(_charPosition, charsToEncode);
                _byteBufferCount = _encoder.GetBytes(charBuffer, 0, charsToEncode, _byteBuffer, 0, flush);
#endif

                _charPosition += charsToEncode;
                _byteBufferPosition = 0;

                if (_byteBufferCount == 0) break;
            }

            int bytesToCopy = Math.Min(count - totalBytesRead, _byteBufferCount - _byteBufferPosition);
            _byteBuffer.AsSpan(_byteBufferPosition, bytesToCopy).CopyTo(user_buffer.Slice(totalBytesRead));
            _byteBufferPosition += bytesToCopy;
            totalBytesRead += bytesToCopy;
            _position += bytesToCopy; // Update position as we read
        }

        return totalBytesRead;
    }

    /// <summary>
    /// Resynchronizes char position with byte position after Position property was changed.
    /// This is expensive (O(n)) because variable-length encoding requires re-encoding from start.
    /// </summary>
    private void ResyncPosition()
    {
        // Reset to beginning
        _encoder.Reset();
        _charPosition = 0;
        _byteBufferPosition = 0;
        _byteBufferCount = 0;

        if (_position == 0)
        {
            return;
        }

        int targetBytePosition = _position;
        int currentBytePosition = 0;

        // Re-encode from start until we reach target byte position
        while (currentBytePosition < targetBytePosition && _charPosition < _source.Length)
        {
            int charsToEncode = Math.Min(1024, _source.Length - _charPosition);
            bool flush = _charPosition + charsToEncode >= _source.Length;

#if NET || NETCOREAPP
            int bytesEncoded = _encoder.GetBytes(
                _source.AsSpan(_charPosition, charsToEncode),
                _byteBuffer.AsSpan(),
                flush);
#else
            char[] charBuffer = _source.ToCharArray(_charPosition, charsToEncode);
            int bytesEncoded = _encoder.GetBytes(charBuffer, 0, charsToEncode, _byteBuffer, 0, flush);
#endif

            if (currentBytePosition + bytesEncoded <= targetBytePosition)
            {
                // Skip this entire chunk
                currentBytePosition += bytesEncoded;
                _charPosition += charsToEncode;
            }
            else
            {
                // Target is within this chunk
                _byteBufferCount = bytesEncoded;
                _byteBufferPosition = targetBytePosition - currentBytePosition;
                _charPosition += charsToEncode;
                break;
            }
        }
    }

    /// <inheritdoc/>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ValidateBufferArguments(buffer, offset, count);

        // If cancellation was requested, bail early
        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled<int>(cancellationToken);

        try
        {
            int n = Read(buffer, offset, count);

            // Try to reuse the cached task if it has the same result
            Task<int>? lastReadTask = _lastReadTask;
            if (lastReadTask != null && lastReadTask.Result == n)
            {
                return lastReadTask;
            }

            // Create a new task and cache it
            Task<int> newTask = Task.FromResult(n);
            _lastReadTask = newTask;
            return newTask;
        }
        catch (OperationCanceledException oce)
        {
            return Task.FromCanceled<int>(oce.CancellationToken);
        }
        catch (Exception exception)
        {
            return Task.FromException<int>(exception);
        }
    }

    /// <inheritdoc/>
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled<int>(cancellationToken);
        }

        try
        {

            int bytesRead;
            if (MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> array))
            {
                // Fast path:  Memory<byte> wraps an array
                bytesRead = Read(array.Array!, array.Offset, array.Count);
            }
            else
            {
                // Slow path: rent a buffer, read, copy
                byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(buffer.Length);
                try
                {
                    bytesRead = Read(rentedBuffer, 0, buffer.Length);
                    rentedBuffer.AsSpan(0, bytesRead).CopyTo(buffer.Span);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(rentedBuffer);
                }
            }

            return new ValueTask<int>(bytesRead);
        }
        catch (OperationCanceledException oce)
        {
            return ValueTask.FromCanceled<int>(oce.CancellationToken);
        }
        catch (Exception exception)
        {
            return ValueTask.FromException<int>(exception);
        }
    }

    /// <inheritdoc/>
    public override void Flush() {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    /// <inheritdoc/>
    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        try
        {
            Flush();
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            return Task.FromException(ex);
        }
    }

    // If done before using Length(),
    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        long newPosition = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => this.Length + offset,
            _ => throw new ArgumentException("Invalid seek origin.", nameof(origin))
        };

        if (newPosition < 0)
            throw new IOException("An attempt was made to move the position before the beginning of the stream.");

        // Allow seeking beyond logical length up to buffer capacity (for write scenarios)
        // and even beyond buffer capacity (reads will return 0, writes will throw)
        ArgumentOutOfRangeException.ThrowIfGreaterThan(newPosition, int.MaxValue, nameof(offset));

        _position = (int)newPosition;
        return newPosition;
    }

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    // Not supported for String or ReadOnlyMemory scenarios
    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(ReadOnlySpan<byte> buffer) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _disposed = true;
            _lastReadTask = null;
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc/>
    public override ValueTask DisposeAsync()
    {
        Dispose();
        return default;
    }
}
