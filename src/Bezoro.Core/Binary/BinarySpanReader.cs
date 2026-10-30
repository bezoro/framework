using System.Buffers.Binary;

namespace Bezoro.Core.Binary;

/// <summary>Reads little-endian values from a borrowed byte span without allocating storage.</summary>
/// <remarks>
/// A failed Try method leaves the cursor unchanged and returns a default output value.
/// Copies have independent cursors over the same borrowed memory. Keep that memory alive and unchanged while reading.
/// The default reader is a valid empty reader. No byte order, schema, or type metadata is inferred.
/// </remarks>
public ref struct BinarySpanReader
{
    private readonly ReadOnlySpan<byte> _source;
    private int _position;

    /// <summary>Creates a reader at the beginning of the borrowed source.</summary>
    public BinarySpanReader(ReadOnlySpan<byte> source)
    {
        _source = source;
        _position = 0;
    }

    /// <summary>Gets the number of bytes consumed.</summary>
    public readonly int Position => _position;

    /// <summary>Gets the number of bytes still available.</summary>
    public readonly int Remaining => _source.Length - _position;

    /// <summary>Gets a borrowed view of the unconsumed bytes.</summary>
    public readonly ReadOnlySpan<byte> UnreadSpan => _source.Slice(_position);

    /// <summary>Reads one byte, or returns false without advancing when exhausted.</summary>
    public bool TryReadByte(out byte value)
    {
        if (Remaining < 1) { value = default; return false; }
        value = _source[_position++];
        return true;
    }

    /// <summary>Reads a 16-bit signed integer in little-endian order.</summary>
    public bool TryReadInt16(out short value)
    {
        if (Remaining < 2) { value = default; return false; }
        value = BinaryPrimitives.ReadInt16LittleEndian(_source.Slice(_position, 2));
        _position += 2;
        return true;
    }

    /// <summary>Reads a 16-bit unsigned integer in little-endian order.</summary>
    public bool TryReadUInt16(out ushort value)
    {
        if (Remaining < 2) { value = default; return false; }
        value = BinaryPrimitives.ReadUInt16LittleEndian(_source.Slice(_position, 2));
        _position += 2;
        return true;
    }

    /// <summary>Reads a 32-bit signed integer in little-endian order.</summary>
    public bool TryReadInt32(out int value)
    {
        if (Remaining < 4) { value = default; return false; }
        value = BinaryPrimitives.ReadInt32LittleEndian(_source.Slice(_position, 4));
        _position += 4;
        return true;
    }

    /// <summary>Reads a 32-bit unsigned integer in little-endian order.</summary>
    public bool TryReadUInt32(out uint value)
    {
        if (Remaining < 4) { value = default; return false; }
        value = BinaryPrimitives.ReadUInt32LittleEndian(_source.Slice(_position, 4));
        _position += 4;
        return true;
    }

    /// <summary>Reads a 64-bit signed integer in little-endian order.</summary>
    public bool TryReadInt64(out long value)
    {
        if (Remaining < 8) { value = default; return false; }
        value = BinaryPrimitives.ReadInt64LittleEndian(_source.Slice(_position, 8));
        _position += 8;
        return true;
    }

    /// <summary>Reads a 64-bit unsigned integer in little-endian order.</summary>
    public bool TryReadUInt64(out ulong value)
    {
        if (Remaining < 8) { value = default; return false; }
        value = BinaryPrimitives.ReadUInt64LittleEndian(_source.Slice(_position, 8));
        _position += 8;
        return true;
    }

    /// <summary>Reads the raw IEEE 754 binary32 representation, preserving non-finite values and signed zero.</summary>
    public bool TryReadSingle(out float value)
    {
        if (!TryReadInt32(out int bits)) { value = default; return false; }
        value = BitConverter.Int32BitsToSingle(bits);
        return true;
    }

    /// <summary>Reads the raw IEEE 754 binary64 representation, preserving non-finite values and signed zero.</summary>
    public bool TryReadDouble(out double value)
    {
        if (!TryReadInt64(out long bits)) { value = default; return false; }
        value = BitConverter.Int64BitsToDouble(bits);
        return true;
    }

    /// <summary>Reads a borrowed view of the requested number of bytes without copying.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The requested byte count is negative.</exception>
    public bool TryReadBytes(int byteCount, out ReadOnlySpan<byte> value)
    {
        if (byteCount < 0) throw new ArgumentOutOfRangeException(nameof(byteCount));
        if (byteCount > Remaining) { value = default; return false; }
        value = _source.Slice(_position, byteCount);
        _position += byteCount;
        return true;
    }

    /// <summary>Reads an Int32 length prefix followed by a borrowed view of that many bytes.</summary>
    /// <param name="maximumLength">The application's explicit, inclusive field-length limit.</param>
    /// <param name="value">The borrowed field, or the default span when the prefix or payload is invalid or incomplete.</param>
    /// <returns>False for a negative length, a length above the limit, or an incomplete field; the cursor is unchanged.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The field-length limit is negative.</exception>
    public bool TryReadLengthPrefixedBytes(int maximumLength, out ReadOnlySpan<byte> value)
    {
        if (maximumLength < 0) throw new ArgumentOutOfRangeException(nameof(maximumLength));
        if (Remaining < sizeof(int)) { value = default; return false; }
        int length = BinaryPrimitives.ReadInt32LittleEndian(_source.Slice(_position, sizeof(int)));
        if (length < 0 || length > maximumLength || length > Remaining - sizeof(int))
        {
            value = default;
            return false;
        }
        value = _source.Slice(_position + sizeof(int), length);
        _position += sizeof(int) + length;
        return true;
    }
}
