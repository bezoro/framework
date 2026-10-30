using System;
using System.Buffers.Binary;
using System.Linq;
using Bezoro.Core.Binary;
using Xunit;

namespace Bezoro.Core.Tests.Binary;

public sealed class BinarySpanTests
{
    [Fact]
    public void PrimitiveEncoding_MatchesAnIndependentLittleEndianFixture()
    {
        // Python struct.pack("<BhHiIqQfd", ...) independently generated these bytes.
        byte[] expected = Convert.FromHexString(
            "ab0080ffff00000080ffffffff0000000000000080ffffffffffffffff000020c00000000000000a40");
        byte[] bytes = new byte[expected.Length + 3];
        Array.Fill(bytes, (byte)0xCC);
        var writer = new BinarySpanWriter(bytes);
        WriteFixture(ref writer);
        Assert.Equal(expected.Length, writer.Position);
        Assert.Equal(3, writer.Remaining);
        Assert.Equal(expected, writer.WrittenSpan.ToArray());
        Assert.Equal(new byte[] { 0xCC, 0xCC, 0xCC }, bytes.AsSpan(expected.Length).ToArray());

        var reader = new BinarySpanReader(expected);
        Assert.True(reader.TryReadByte(out byte b)); Assert.Equal(0xAB, b);
        Assert.True(reader.TryReadInt16(out short i16)); Assert.Equal(short.MinValue, i16);
        Assert.True(reader.TryReadUInt16(out ushort u16)); Assert.Equal(ushort.MaxValue, u16);
        Assert.True(reader.TryReadInt32(out int i32)); Assert.Equal(int.MinValue, i32);
        Assert.True(reader.TryReadUInt32(out uint u32)); Assert.Equal(uint.MaxValue, u32);
        Assert.True(reader.TryReadInt64(out long i64)); Assert.Equal(long.MinValue, i64);
        Assert.True(reader.TryReadUInt64(out ulong u64)); Assert.Equal(ulong.MaxValue, u64);
        Assert.True(reader.TryReadSingle(out float f)); Assert.Equal(-2.5f, f);
        Assert.True(reader.TryReadDouble(out double d)); Assert.Equal(3.25, d);
        Assert.Equal(expected.Length, reader.Position);
        Assert.Equal(0, reader.Remaining);
        Assert.True(reader.UnreadSpan.IsEmpty);
    }

    [Theory]
    [InlineData(Primitive.Byte, 1)]
    [InlineData(Primitive.Int16, 2)]
    [InlineData(Primitive.UInt16, 2)]
    [InlineData(Primitive.Int32, 4)]
    [InlineData(Primitive.UInt32, 4)]
    [InlineData(Primitive.Int64, 8)]
    [InlineData(Primitive.UInt64, 8)]
    [InlineData(Primitive.Single, 4)]
    [InlineData(Primitive.Double, 8)]
    public void PrimitiveTruncation_IsAtomicAtEveryIncompleteLength(Primitive primitive, int width)
    {
        for (int available = 0; available < width; available++)
        {
            byte[] bytes = Enumerable.Repeat((byte)0xCC, available + 1).ToArray();
            var writer = new BinarySpanWriter(bytes);
            Assert.True(writer.TryWriteByte(0x42));
            byte[] before = (byte[])bytes.Clone();
            Assert.False(TryWrite(ref writer, primitive));
            Assert.Equal(1, writer.Position);
            Assert.Equal(available, writer.Remaining);
            Assert.Equal(before, bytes);

            var reader = new BinarySpanReader(bytes);
            Assert.True(reader.TryReadByte(out _));
            Assert.False(TryRead(ref reader, primitive, out ulong raw));
            Assert.Equal(0ul, raw);
            Assert.Equal(1, reader.Position);
            Assert.Equal(available, reader.Remaining);
        }
    }

    [Fact]
    public void FloatingPointEncoding_PreservesAllBitsIncludingNonFiniteValues()
    {
        int[] singlePatterns = { 0, unchecked((int)0x80000000), 0x7F800000,
            unchecked((int)0xFF800000), 0x7FC12345, 0x7FA12345, 1, 0x7F7FFFFF };
        long[] doublePatterns = { 0, unchecked((long)0x8000000000000000), 0x7FF0000000000000,
            unchecked((long)0xFFF0000000000000), 0x7FF8123456789ABC, 0x7FF0123456789ABC, 1, 0x7FEFFFFFFFFFFFFF };
        Span<byte> bytes = stackalloc byte[8];
        foreach (int bits in singlePatterns)
        {
            var writer = new BinarySpanWriter(bytes);
            Assert.True(writer.TryWriteSingle(BitConverter.Int32BitsToSingle(bits)));
            Assert.Equal(bits, BinaryPrimitives.ReadInt32LittleEndian(bytes));
            var reader = new BinarySpanReader(writer.WrittenSpan);
            Assert.True(reader.TryReadSingle(out float value));
            Assert.Equal(bits, BitConverter.SingleToInt32Bits(value));
        }
        foreach (long bits in doublePatterns)
        {
            var writer = new BinarySpanWriter(bytes);
            Assert.True(writer.TryWriteDouble(BitConverter.Int64BitsToDouble(bits)));
            Assert.Equal(bits, BinaryPrimitives.ReadInt64LittleEndian(bytes));
            var reader = new BinarySpanReader(writer.WrittenSpan);
            Assert.True(reader.TryReadDouble(out double value));
            Assert.Equal(bits, BitConverter.DoubleToInt64Bits(value));
        }
    }

    [Fact]
    public void DefaultCursors_AreValidEmptyBuffers()
    {
        BinarySpanWriter writer = default;
        Assert.Equal(0, writer.Position);
        Assert.Equal(0, writer.Remaining);
        Assert.True(writer.WrittenSpan.IsEmpty);
        Assert.False(writer.TryWriteByte(1));
        Assert.False(writer.TryWriteLengthPrefixedBytes(ReadOnlySpan<byte>.Empty));
        Assert.True(writer.TryWriteBytes(ReadOnlySpan<byte>.Empty));

        BinarySpanReader reader = default;
        Assert.Equal(0, reader.Position);
        Assert.Equal(0, reader.Remaining);
        Assert.True(reader.UnreadSpan.IsEmpty);
        Assert.False(reader.TryReadByte(out byte value));
        Assert.Equal(0, value);
        Assert.True(reader.TryReadBytes(0, out var empty));
        Assert.True(empty.IsEmpty);
        Assert.False(reader.TryReadLengthPrefixedBytes(0, out var absent));
        Assert.True(absent.IsEmpty);
    }

    [Fact]
    public void CopiedCursors_AdvanceIndependentlyAndBorrowTheSameStorage()
    {
        byte[] bytes = new byte[8];
        var writer = new BinarySpanWriter(bytes);
        Assert.True(writer.TryWriteInt32(42));
        var copy = writer;
        Assert.True(copy.TryWriteInt32(99));
        Assert.Equal(4, writer.Position);
        Assert.Equal(8, copy.Position);
        Assert.True(writer.TryWriteInt32(7));
        Assert.Equal(7, BinaryPrimitives.ReadInt32LittleEndian(copy.WrittenSpan.Slice(4)));

        var reader = new BinarySpanReader(bytes);
        var readerCopy = reader;
        Assert.True(reader.TryReadInt32(out int first));
        Assert.Equal(42, first);
        Assert.Equal(0, readerCopy.Position);
        Assert.Equal(8, readerCopy.Remaining);
    }

    [Fact]
    public void RawBytes_UseBorrowedViewsAndPreserveCursorOnTruncation()
    {
        byte[] bytes = { 1, 2, 3, 4 };
        var reader = new BinarySpanReader(bytes);
        Assert.True(reader.TryReadBytes(2, out var first));
        bytes[0] = 9;
        Assert.Equal(9, first[0]);
        Assert.Equal(new byte[] { 3, 4 }, reader.UnreadSpan.ToArray());
        Assert.False(reader.TryReadBytes(3, out var absent));
        Assert.True(absent.IsEmpty);
        Assert.Equal(2, reader.Position);
        Assert.True(reader.TryReadBytes(2, out var last));
        Assert.Equal(new byte[] { 3, 4 }, last.ToArray());
    }

    [Fact]
    public void StackAllocatedSources_CanHaveAShorterLifetimeThanTheWriter()
    {
        Span<byte> destination = stackalloc byte[10];
        var writer = new BinarySpanWriter(destination);
        {
            Span<byte> source = stackalloc byte[] { 1, 2, 3 };
            Assert.True(writer.TryWriteBytes(source));
            Assert.True(writer.TryWriteLengthPrefixedBytes(source));
        }
        Assert.Equal(10, writer.Position);
        Assert.Equal(new byte[] { 1, 2, 3, 3, 0, 0, 0, 1, 2, 3 }, writer.WrittenSpan.ToArray());
    }

    [Fact]
    public void NegativeReadLengths_ThrowWithoutAdvancing()
    {
        var reader = new BinarySpanReader(new byte[8]);
        bool rejectedCount = false, rejectedLimit = false;
        try { reader.TryReadBytes(-1, out _); }
        catch (ArgumentOutOfRangeException error) { Assert.Equal("byteCount", error.ParamName); rejectedCount = true; }
        try { reader.TryReadLengthPrefixedBytes(-1, out _); }
        catch (ArgumentOutOfRangeException error) { Assert.Equal("maximumLength", error.ParamName); rejectedLimit = true; }
        Assert.True(rejectedCount);
        Assert.True(rejectedLimit);
        Assert.Equal(0, reader.Position);
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(int.MinValue, 10)]
    [InlineData(int.MaxValue, int.MaxValue)]
    [InlineData(4, 3)]
    [InlineData(5, 10)]
    public void InvalidLengthPrefixes_DoNotPublishOrConsumeTheField(int length, int limit)
    {
        byte[] bytes = { 0xAB, 0, 0, 0, 0, 1, 2, 3, 4 };
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(1), length);
        var reader = new BinarySpanReader(bytes);
        Assert.True(reader.TryReadByte(out _));
        Assert.False(reader.TryReadLengthPrefixedBytes(limit, out var value));
        Assert.True(value.IsEmpty);
        Assert.Equal(1, reader.Position);
        Assert.Equal(8, reader.Remaining);
    }

    [Fact]
    public void LengthPrefixedFields_ValidateTheWholeFieldAtEveryTruncatedLength()
    {
        byte[] field = { 3, 0, 0, 0, 0xAB, 0xCD, 0xEF };
        for (int length = 0; length < field.Length; length++)
        {
            var reader = new BinarySpanReader(field.AsSpan(0, length));
            Assert.False(reader.TryReadLengthPrefixedBytes(3, out var value));
            Assert.True(value.IsEmpty);
            Assert.Equal(0, reader.Position);

            byte[] destination = Enumerable.Repeat((byte)0xCC, length).ToArray();
            var writer = new BinarySpanWriter(destination);
            Assert.False(writer.TryWriteLengthPrefixedBytes(field.AsSpan(4)));
            Assert.Equal(0, writer.Position);
            Assert.All(destination, b => Assert.Equal(0xCC, b));
        }
        var complete = new BinarySpanReader(field);
        Assert.True(complete.TryReadLengthPrefixedBytes(3, out var payload));
        Assert.Equal(field.AsSpan(4).ToArray(), payload.ToArray());
        Assert.Equal(field.Length, complete.Position);
    }

    [Fact]
    public void EmptyLengthPrefixedField_ConsumesOnlyThePrefix()
    {
        byte[] bytes = { 0xCC, 0xCC, 0xCC, 0xCC };
        var writer = new BinarySpanWriter(bytes);
        Assert.True(writer.TryWriteLengthPrefixedBytes(ReadOnlySpan<byte>.Empty));
        Assert.Equal(new byte[4], bytes);
        Assert.Equal(4, writer.Position);
        var reader = new BinarySpanReader(bytes);
        Assert.True(reader.TryReadLengthPrefixedBytes(0, out var payload));
        Assert.True(payload.IsEmpty);
        Assert.Equal(4, reader.Position);
    }

    [Fact]
    public void RawByteCopies_SupportEveryOverlapDirection()
    {
        for (int sourceStart = 0; sourceStart < 12; sourceStart++)
        for (int destinationStart = 0; destinationStart < 12; destinationStart++)
        for (int length = 0; length <= Math.Min(12 - sourceStart, 12 - destinationStart); length++)
        {
            byte[] bytes = Enumerable.Range(0, 12).Select(i => (byte)i).ToArray();
            byte[] expected = (byte[])bytes.Clone();
            Array.Copy(expected, sourceStart, expected, destinationStart, length);
            var writer = new BinarySpanWriter(bytes.AsSpan(destinationStart));
            Assert.True(writer.TryWriteBytes(bytes.AsSpan(sourceStart, length)));
            Assert.Equal(expected, bytes);
            Assert.Equal(length, writer.Position);
        }
    }

    [Fact]
    public void LengthPrefixedCopies_SupportSourceOverlapWithBothHeaderAndPayload()
    {
        for (int sourceStart = 0; sourceStart <= 16; sourceStart++)
        for (int destinationStart = 0; destinationStart <= 12; destinationStart++)
        for (int length = 0; length <= Math.Min(16 - sourceStart, 12 - destinationStart); length++)
        {
            byte[] bytes = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();
            byte[] sourceSnapshot = bytes.AsSpan(sourceStart, length).ToArray();
            byte[] expected = (byte[])bytes.Clone();
            Array.Copy(sourceSnapshot, 0, expected, destinationStart + 4, length);
            BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(destinationStart), length);
            var writer = new BinarySpanWriter(bytes.AsSpan(destinationStart));
            Assert.True(writer.TryWriteLengthPrefixedBytes(bytes.AsSpan(sourceStart, length)));
            Assert.Equal(expected, bytes);
            Assert.Equal(length + 4, writer.Position);
        }
    }

    [Fact]
    public void LargeSeededIntegerSequences_RoundTripAndMatchBitShiftReference()
    {
        var random = new Random(4281);
        byte[] bytes = new byte[8];
        for (int iteration = 0; iteration < 20_000; iteration++)
        {
            random.NextBytes(bytes);
            ulong value = 0;
            for (int index = 0; index < 8; index++) value |= (ulong)bytes[index] << (index * 8);
            var reader = new BinarySpanReader(bytes);
            Assert.True(reader.TryReadUInt64(out ulong decoded));
            Assert.Equal(value, decoded);
            Array.Clear(bytes);
            var writer = new BinarySpanWriter(bytes);
            Assert.True(writer.TryWriteUInt64(value));
            for (int index = 0; index < 8; index++) Assert.Equal(unchecked((byte)(value >> (index * 8))), bytes[index]);
        }
    }

    [Fact]
    public void WarmedOperations_AllocateNoManagedMemory()
    {
        byte[] bytes = new byte[64];
        byte[] payload = { 1, 2, 3, 4 };
        for (int i = 0; i < 1000; i++) Exercise(bytes, payload);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10_000; i++) Exercise(bytes, payload);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
    }

    private static void Exercise(Span<byte> bytes, ReadOnlySpan<byte> payload)
    {
        var writer = new BinarySpanWriter(bytes);
        writer.TryWriteByte(1);
        writer.TryWriteInt16(-2);
        writer.TryWriteUInt16(3);
        writer.TryWriteInt32(-4);
        writer.TryWriteUInt32(5);
        writer.TryWriteInt64(-6);
        writer.TryWriteUInt64(7);
        writer.TryWriteSingle(0.5f);
        writer.TryWriteDouble(0.25);
        writer.TryWriteBytes(payload);
        writer.TryWriteLengthPrefixedBytes(payload);
        var reader = new BinarySpanReader(writer.WrittenSpan);
        reader.TryReadByte(out _);
        reader.TryReadInt16(out _);
        reader.TryReadUInt16(out _);
        reader.TryReadInt32(out _);
        reader.TryReadUInt32(out _);
        reader.TryReadInt64(out _);
        reader.TryReadUInt64(out _);
        reader.TryReadSingle(out _);
        reader.TryReadDouble(out _);
        reader.TryReadBytes(4, out _);
        reader.TryReadLengthPrefixedBytes(4, out _);
    }

    private static void WriteFixture(ref BinarySpanWriter writer)
    {
        Assert.True(writer.TryWriteByte(0xAB));
        Assert.True(writer.TryWriteInt16(short.MinValue));
        Assert.True(writer.TryWriteUInt16(ushort.MaxValue));
        Assert.True(writer.TryWriteInt32(int.MinValue));
        Assert.True(writer.TryWriteUInt32(uint.MaxValue));
        Assert.True(writer.TryWriteInt64(long.MinValue));
        Assert.True(writer.TryWriteUInt64(ulong.MaxValue));
        Assert.True(writer.TryWriteSingle(-2.5f));
        Assert.True(writer.TryWriteDouble(3.25));
    }

    private static bool TryWrite(ref BinarySpanWriter writer, Primitive primitive) => primitive switch
    {
        Primitive.Byte => writer.TryWriteByte(0xA5),
        Primitive.Int16 => writer.TryWriteInt16(short.MinValue),
        Primitive.UInt16 => writer.TryWriteUInt16(ushort.MaxValue),
        Primitive.Int32 => writer.TryWriteInt32(int.MinValue),
        Primitive.UInt32 => writer.TryWriteUInt32(uint.MaxValue),
        Primitive.Int64 => writer.TryWriteInt64(long.MinValue),
        Primitive.UInt64 => writer.TryWriteUInt64(ulong.MaxValue),
        Primitive.Single => writer.TryWriteSingle(-2.5f),
        Primitive.Double => writer.TryWriteDouble(3.25),
        _ => throw new ArgumentOutOfRangeException(nameof(primitive))
    };

    private static bool TryRead(ref BinarySpanReader reader, Primitive primitive, out ulong raw)
    {
        bool success;
        switch (primitive)
        {
            case Primitive.Byte: success = reader.TryReadByte(out byte b); raw = b; break;
            case Primitive.Int16: success = reader.TryReadInt16(out short i16); raw = unchecked((ulong)i16); break;
            case Primitive.UInt16: success = reader.TryReadUInt16(out ushort u16); raw = u16; break;
            case Primitive.Int32: success = reader.TryReadInt32(out int i32); raw = unchecked((ulong)i32); break;
            case Primitive.UInt32: success = reader.TryReadUInt32(out uint u32); raw = u32; break;
            case Primitive.Int64: success = reader.TryReadInt64(out long i64); raw = unchecked((ulong)i64); break;
            case Primitive.UInt64: success = reader.TryReadUInt64(out ulong u64); raw = u64; break;
            case Primitive.Single: success = reader.TryReadSingle(out float f); raw = unchecked((uint)BitConverter.SingleToInt32Bits(f)); break;
            case Primitive.Double: success = reader.TryReadDouble(out double d); raw = unchecked((ulong)BitConverter.DoubleToInt64Bits(d)); break;
            default: throw new ArgumentOutOfRangeException(nameof(primitive));
        }
        return success;
    }

    public enum Primitive { Byte, Int16, UInt16, Int32, UInt32, Int64, UInt64, Single, Double }
}
