using System.Buffers.Binary;

namespace Bezoro.Core.Binary;

/// <summary>Writes little-endian values into a borrowed byte span without allocating storage.</summary>
/// <remarks>
/// Insufficient capacity leaves both the cursor and destination unchanged.
/// Copies have independent cursors but alias the same destination, so coordinate writes externally.
/// The default writer is a valid empty writer. No storage grows and unwritten capacity is not cleared.
/// </remarks>
public ref struct BinarySpanWriter
{
    private readonly Span<byte> _destination;
    private int _position;

    /// <summary>Creates a writer at the beginning of the borrowed destination.</summary>
    public BinarySpanWriter(Span<byte> destination)
    {
        _destination = destination;
        _position = 0;
    }

    /// <summary>Gets the number of bytes written.</summary>
    public readonly int Position => _position;

    /// <summary>Gets the number of bytes still available.</summary>
    public readonly int Remaining => _destination.Length - _position;

    /// <summary>Gets a borrowed read-only view of bytes written by this cursor.</summary>
    public readonly ReadOnlySpan<byte> WrittenSpan => _destination.Slice(0, _position);

    /// <summary>Writes one byte, or returns false without mutation when exhausted.</summary>
    public bool TryWriteByte(byte value)
    {
        if (Remaining < 1) return false;
        _destination[_position++] = value;
        return true;
    }

    /// <summary>Writes a 16-bit signed integer in little-endian order.</summary>
    public bool TryWriteInt16(short value)
    {
        if (Remaining < 2) return false;
        BinaryPrimitives.WriteInt16LittleEndian(_destination.Slice(_position, 2), value);
        _position += 2;
        return true;
    }

    /// <summary>Writes a 16-bit unsigned integer in little-endian order.</summary>
    public bool TryWriteUInt16(ushort value)
    {
        if (Remaining < 2) return false;
        BinaryPrimitives.WriteUInt16LittleEndian(_destination.Slice(_position, 2), value);
        _position += 2;
        return true;
    }

    /// <summary>Writes a 32-bit signed integer in little-endian order.</summary>
    public bool TryWriteInt32(int value)
    {
        if (Remaining < 4) return false;
        BinaryPrimitives.WriteInt32LittleEndian(_destination.Slice(_position, 4), value);
        _position += 4;
        return true;
    }

    /// <summary>Writes a 32-bit unsigned integer in little-endian order.</summary>
    public bool TryWriteUInt32(uint value)
    {
        if (Remaining < 4) return false;
        BinaryPrimitives.WriteUInt32LittleEndian(_destination.Slice(_position, 4), value);
        _position += 4;
        return true;
    }

    /// <summary>Writes a 64-bit signed integer in little-endian order.</summary>
    public bool TryWriteInt64(long value)
    {
        if (Remaining < 8) return false;
        BinaryPrimitives.WriteInt64LittleEndian(_destination.Slice(_position, 8), value);
        _position += 8;
        return true;
    }

    /// <summary>Writes a 64-bit unsigned integer in little-endian order.</summary>
    public bool TryWriteUInt64(ulong value)
    {
        if (Remaining < 8) return false;
        BinaryPrimitives.WriteUInt64LittleEndian(_destination.Slice(_position, 8), value);
        _position += 8;
        return true;
    }

    /// <summary>Writes the raw IEEE 754 binary32 representation, preserving non-finite values and signed zero.</summary>
    public bool TryWriteSingle(float value) => TryWriteInt32(BitConverter.SingleToInt32Bits(value));

    /// <summary>Writes the raw IEEE 754 binary64 representation, preserving non-finite values and signed zero.</summary>
    public bool TryWriteDouble(double value) => TryWriteInt64(BitConverter.DoubleToInt64Bits(value));

    /// <summary>Copies bytes into the destination; source and destination may overlap.</summary>
    public bool TryWriteBytes(scoped ReadOnlySpan<byte> value)
    {
        if (value.Length > Remaining) return false;
        value.CopyTo(_destination.Slice(_position));
        _position += value.Length;
        return true;
    }

    /// <summary>Writes an Int32 byte-count prefix and the bytes, supporting overlapping source and destination.</summary>
    /// <remarks>Capacity is checked for the complete field before writing any bytes.</remarks>
    public bool TryWriteLengthPrefixedBytes(scoped ReadOnlySpan<byte> value)
    {
        if (Remaining < sizeof(int) || value.Length > Remaining - sizeof(int)) return false;
        // Copy first: writing the prefix first could corrupt a source that overlaps the header.
        value.CopyTo(_destination.Slice(_position + sizeof(int), value.Length));
        BinaryPrimitives.WriteInt32LittleEndian(_destination.Slice(_position, sizeof(int)), value.Length);
        _position += sizeof(int) + value.Length;
        return true;
    }
}
