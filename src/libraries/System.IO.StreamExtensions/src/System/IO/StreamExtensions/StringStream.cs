// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System.Text;

namespace System.IO.StreamExtensions;

/// <summary>
/// Provides a read-only, non-seekable stream that encodes a string into bytes on-the-fly.
/// </summary>
public sealed class StringStream : Stream
{
    private readonly string _source;
    private readonly Encoder _encoder;
    private int _charPosition;
    private readonly byte[] _byteBuffer;
    private int _byteBufferCount;
    private int _byteBufferPosition;
    private bool _disposed;

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
        _encoder = encoding.GetEncoder();
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
                _byteBufferCount = _encoder.GetBytes(_source.AsSpan(_charPosition, charsToEncode), _byteBuffer.AsSpan(), flush);
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
