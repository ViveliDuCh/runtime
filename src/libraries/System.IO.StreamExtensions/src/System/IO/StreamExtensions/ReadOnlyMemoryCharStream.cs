// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System;
using System.Collections.Generic;
using System.Text;

namespace System.IO.StreamExtensions;

/// <summary>
/// Provides a read-only <see cref="Stream"/> implementation that encodes a string to bytes on-the-fly.
/// </summary>
public class ReadOnlyMemoryCharStream : Stream
{
    // Supports memory slices without string allocation
    // Can wrap externally-provided char buffers
    // Identical encoding logic but different source type
    private readonly ReadOnlyMemory<char> _source;
    private readonly Encoder _encoder;
    private int _charPosition;
    private readonly byte[] _byteBuffer;
    private int _byteBufferCount;
    private int _byteBufferPosition;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReadOnlyMemoryCharStream"/> class with the specified source ReadOnlyMemory{char} using UTF-8 encoding.
    /// </summary>
    /// <param name="source">The ReadOnlyMemory{char} to read from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public ReadOnlyMemoryCharStream(ReadOnlyMemory<char> source)
        : this(source, Encoding.UTF8)
    {
    } // Probably better unified with StringStream as a ctor overload**

    /// <summary>
    /// Initializes a new instance of the <see cref="ReadOnlyMemoryCharStream"/> class with the specified source string and encoding.
    /// </summary>
    /// <param name="source">The ReadOnlyMemory{char} to read from.</param>
    /// <param name="encoding">The encoding to use when converting the string to bytes.</param>
    /// <param name="bufferSize">The size of the internal buffer used for encoding. Default is 4096 bytes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public ReadOnlyMemoryCharStream(ReadOnlyMemory<char> source, Encoding encoding, int bufferSize = 4096)
    {
        _source = source;
        _encoder = (encoding ?? throw new ArgumentNullException(nameof(encoding))).GetEncoder();
        //_encoder = encoding.GetEncoder();
        _byteBuffer = new byte[bufferSize];
    }

    /// <inheritdoc/>
    public override bool CanRead => !_disposed;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    // Read method encodes chunks of the underlying string into the provided buffer "on-the-fly"
    // with a 4KB window (_byteBuffer) for encoding
    /// <inheritdoc/>
    public override int Read(byte[] user_buffer, int offset, int count)
    {
        ValidateBufferArguments(user_buffer, offset, count);
        ObjectDisposedException.ThrowIf(_disposed, this);

        int totalBytesRead = 0;

        while (totalBytesRead < count)
        {
            if (_byteBufferPosition >= _byteBufferCount)
            {
                if (_charPosition >= _source.Length) break;

                int charsToEncode = Math.Min(1024, _source.Length - _charPosition);
                bool flush = _charPosition + charsToEncode >= _source.Length;

#if NET || NETCOREAPP
                _byteBufferCount = _encoder.GetBytes(_source.Span.Slice(_charPosition, charsToEncode), _byteBuffer.AsSpan(), flush);
#else
                // For .NET Standard 2.0 and .NET Framework, use char array approach
                char[] charBuffer = _source.ToCharArray(_charPosition, charsToEncode);
                _byteBufferCount = _encoder.GetBytes(charBuffer, 0, charsToEncode, _byteBuffer, 0, flush);
#endif

                _charPosition += charsToEncode;
                _byteBufferPosition = 0;

                if (_byteBufferCount == 0) break;
            }

            int bytesToCopy = Math.Min(count - totalBytesRead, _byteBufferCount - _byteBufferPosition);
            Array.Copy(_byteBuffer, _byteBufferPosition, user_buffer, offset + totalBytesRead, bytesToCopy);
            _byteBufferPosition += bytesToCopy;
            totalBytesRead += bytesToCopy;
        }

        return totalBytesRead;
    }

    /// <inheritdoc/>
    public override void Flush() { }
    // Seek not supported - read-only stream. Data is read sequentially.
    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();
    // Not supported for String or ReadOnlyMemory scenarios
    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        _disposed = true;
        base.Dispose(disposing);
    }
}
