// IEnumerable-strategy for stream wrappers.
//
// The key insight from IEnumerable<T>:
//   - The SOURCE (collection) is immutable — it just provides data access
//   - The CURSOR (enumerator) is a separate object that holds mutable state
//   - Extension methods (LINQ) create new cursors, never mutate the source
//
// Applied to streams:
//   - IStreamSource: immutable data provider (like IEnumerable)
//   - StreamSourceReader: mutable cursor/reader (like IEnumerator)
//   - DIMs on IStreamSource provide factory/query methods that don't need mutation
//   - All position-tracking mutation lives in the reader (a class, no boxing)

using System;
using System.Buffers;
using System.IO;

namespace IStreamableBenchmarks;

/// <summary>
/// Immutable data source interface — analogous to IEnumerable&lt;T&gt;.
///
/// The source provides data access and creates readers, but holds NO mutable state.
/// This solves the DIM+boxing showstopper: since the source is immutable,
/// boxing a source struct loses no state.
///
/// Pattern comparison:
///   IEnumerable&lt;T&gt;.GetEnumerator() → IEnumerator&lt;T&gt; (holds Current/MoveNext state)
///   IStreamSource.CreateReader()     → StreamSourceReader (holds Position/Read state)
/// </summary>
public interface IStreamSource
{
    // === Core — MUST implement ===

    /// <summary>Total length of the data in bytes.</summary>
    long Length { get; }

    /// <summary>
    /// Read <paramref name="count"/> bytes starting at <paramref name="offset"/>
    /// into <paramref name="buffer"/>.
    /// This is a STATELESS read — the source doesn't track position.
    /// </summary>
    /// <returns>Number of bytes actually read.</returns>
    int ReadAt(long offset, Span<byte> buffer);

    /// <summary>Whether the source supports writing.</summary>
    bool CanWrite => false;

    /// <summary>
    /// Write bytes at the given offset. Only valid if CanWrite is true.
    /// </summary>
    void WriteAt(long offset, ReadOnlySpan<byte> buffer) =>
        throw new NotSupportedException();

    // === DIM defaults — free behavior derived from the core ===

    /// <summary>
    /// Creates a reader (cursor) over this source — analogous to GetEnumerator().
    /// The reader holds mutable position state; the source remains immutable.
    ///
    /// DIM default: creates a generic StreamSourceReader that delegates back
    /// to ReadAt/WriteAt. Types MAY override for optimized readers.
    /// </summary>
    StreamSourceReader CreateReader() => new StreamSourceReader(this);

    /// <summary>
    /// Read a single byte at the given offset.
    /// DIM default: delegates to ReadAt with a 1-byte span.
    /// Types MAY override for direct indexing performance.
    /// </summary>
    int ReadByteAt(long offset)
    {
        byte b = 0;
        return ReadAt(offset, new Span<byte>(ref b)) == 1 ? b : -1;
    }

    /// <summary>
    /// Copy all data from <paramref name="offset"/> to the destination stream.
    /// DIM default: reads in a loop with a rented buffer.
    /// Types MAY override for bulk copy (e.g., single Span.CopyTo).
    /// </summary>
    void CopyTo(long offset, Stream destination)
    {
        byte[] buf = ArrayPool<byte>.Shared.Rent(4096);
        try
        {
            long pos = offset;
            int n;
            while ((n = ReadAt(pos, buf)) > 0)
            {
                destination.Write(buf.AsSpan(0, n));
                pos += n;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buf);
        }
    }
}

/// <summary>
/// Mutable reader/cursor over an IStreamSource — analogous to IEnumerator&lt;T&gt;.
///
/// This is a CLASS (reference type), so:
///   - No boxing when stored in fields or passed around
///   - Mutations to Position are always reflected (no copy-on-box)
///   - The DIM+boxing showstopper does not apply
///
/// The reader delegates all data access back to the source via ReadAt/WriteAt.
/// It only manages position tracking, bounds checking, and seek logic.
/// </summary>
public class StreamSourceReader
{
    private readonly IStreamSource _source;
    private long _position;

    public StreamSourceReader(IStreamSource source)
    {
        _source = source;
    }

    public long Length => _source.Length;
    public bool CanRead => true;
    public bool CanWrite => _source.CanWrite;
    public bool CanSeek => true;

    public long Position
    {
        get => _position;
        set => _position = value;
    }

    public int Read(Span<byte> buffer)
    {
        int n = _source.ReadAt(_position, buffer);
        _position += n;
        return n;
    }

    public int ReadByte()
    {
        int result = _source.ReadByteAt(_position);
        if (result >= 0) _position++;
        return result;
    }

    public void Write(ReadOnlySpan<byte> buffer)
    {
        _source.WriteAt(_position, buffer);
        _position += buffer.Length;
    }

    public void WriteByte(byte value)
    {
        Span<byte> single = stackalloc byte[1] { value };
        _source.WriteAt(_position, single);
        _position++;
    }

    public long Seek(long offset, SeekOrigin origin)
    {
        long pos = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _source.Length + offset,
            _ => throw new ArgumentException("Invalid seek origin.", nameof(origin))
        };
        _position = pos;
        return pos;
    }

    public void CopyTo(Stream destination)
    {
        _source.CopyTo(_position, destination);
        _position = _source.Length;
    }

    public void SetLength(long value) =>
        throw new NotSupportedException();
}
