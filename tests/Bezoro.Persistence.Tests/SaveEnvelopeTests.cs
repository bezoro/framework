using System.Buffers.Binary;
using System.Text;
using Xunit;

namespace Bezoro.Persistence.Tests;

public sealed class SaveEnvelopeTests
{
    private static byte[] GoldenEnvelope => Convert.FromHexString(
        "425A52534156450001002000070000000300000000000000FB6133F400000000616263");

    [Fact]
    public void Write_MatchesIndependentLittleEndianAndCrcFixture()
    {
        // Fixture calculated using Python struct.pack('<8sHHIQII', ...) and zlib.crc32.
        byte[] destination = new byte[35];
        Assert.Equal(35, SaveEnvelope.Write(7, Encoding.ASCII.GetBytes("abc"), destination));
        Assert.Equal(GoldenEnvelope, destination);
    }

    [Fact]
    public void Decode_ReadsIndependentGoldenEnvelope()
    {
        Assert.Equal(SaveReadStatus.Success,
            SaveEnvelope.Decode(GoldenEnvelope, 3, out var header, out var payload, expectedSchemaVersion: 7));
        Assert.Equal(1, header.FormatVersion);
        Assert.Equal(7u, header.SchemaVersion);
        Assert.Equal(3ul, header.PayloadLength);
        Assert.Equal(0xF43361FBu, header.Checksum);
        Assert.Equal(Encoding.ASCII.GetBytes("abc"), payload.ToArray());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(32)]
    [InlineData(1024)]
    [InlineData(65537)]
    public void WriteAndDecode_PreservePayloadAndLeaveDestinationTailUntouched(int length)
    {
        var payload = new byte[length];
        new Random(123).NextBytes(payload);
        int required = SaveEnvelope.GetEncodedLength(length);
        var encoded = Enumerable.Repeat((byte)0xA5, required + 11).ToArray();
        Assert.Equal(required, SaveEnvelope.Write(1, payload, encoded));
        Assert.All(encoded.Skip(required), value => Assert.Equal(0xA5, value));
        Assert.Equal(SaveReadStatus.Success,
            SaveEnvelope.Decode(encoded.AsSpan(0, required), length, out _, out var decoded));
        Assert.Equal(payload, decoded.ToArray());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(32)]
    public void Write_SupportsOverlappingSourceAndDestination(int payloadOffset)
    {
        byte[] buffer = new byte[35];
        Encoding.ASCII.GetBytes("abc").CopyTo(buffer, payloadOffset);
        SaveEnvelope.Write(7, buffer.AsSpan(payloadOffset, 3), buffer);
        Assert.Equal(GoldenEnvelope, buffer);
    }

    [Fact]
    public void Write_WithInsufficientDestination_DoesNotChangeIt()
    {
        byte[] destination = Enumerable.Repeat((byte)42, 34).ToArray();
        Assert.Throws<ArgumentException>(() => SaveEnvelope.Write(1, new byte[3], destination));
        Assert.All(destination, value => Assert.Equal(42, value));
    }

    [Fact]
    public void Write_WithInvalidSchema_DoesNotChangeDestination()
    {
        byte[] destination = Enumerable.Repeat((byte)42, 35).ToArray();
        Assert.Throws<ArgumentOutOfRangeException>(() => SaveEnvelope.Write(0, new byte[3], destination));
        Assert.All(destination, value => Assert.Equal(42, value));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void LengthAndLimitValidation_RejectUnrepresentableEnvelopes(int invalid)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SaveEnvelope.GetEncodedLength(invalid));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaveEnvelope.DecodeHeader(GoldenEnvelope, invalid, out _));
    }

    [Fact]
    public void GetEncodedLength_HandlesRepresentableBoundaryWithoutAllocation()
    {
        Assert.Equal(int.MaxValue, SaveEnvelope.GetEncodedLength(SaveEnvelope.MaximumPayloadLength));
        Assert.Equal(SaveEnvelope.HeaderSize, SaveEnvelope.GetEncodedLength(0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(31)]
    public void Decode_RejectsShortHeaders(int length)
    {
        AssertFailure(GoldenEnvelope.AsSpan(0, length).ToArray(), SaveReadStatus.HeaderTooShort);
    }

    [Theory]
    [InlineData(0, 0, SaveReadStatus.InvalidMagic)]
    [InlineData(8, 2, SaveReadStatus.UnsupportedFormatVersion)]
    [InlineData(10, 31, SaveReadStatus.InvalidHeader)]
    [InlineData(12, 0, SaveReadStatus.InvalidHeader)]
    [InlineData(28, 1, SaveReadStatus.InvalidHeader)]
    [InlineData(24, 0, SaveReadStatus.ChecksumMismatch)]
    [InlineData(32, 0, SaveReadStatus.ChecksumMismatch)]
    public void Decode_RejectsCorruptedFields(int offset, byte value, SaveReadStatus expected)
    {
        byte[] encoded = GoldenEnvelope;
        encoded[offset] = value;
        AssertFailure(encoded, expected);
    }

    [Fact]
    public void Checksum_CoversSchemaMetadataAsWellAsPayload()
    {
        byte[] encoded = GoldenEnvelope;
        BinaryPrimitives.WriteUInt32LittleEndian(encoded.AsSpan(12), 8);
        AssertFailure(encoded, SaveReadStatus.ChecksumMismatch);
    }

    [Fact]
    public void Decode_RejectsUnboundedDeclaredLengthBeforeExposingMetadata()
    {
        byte[] encoded = GoldenEnvelope.AsSpan(0, SaveEnvelope.HeaderSize).ToArray();
        BinaryPrimitives.WriteUInt64LittleEndian(encoded.AsSpan(16), ulong.MaxValue);
        Assert.Equal(SaveReadStatus.PayloadTooLarge, SaveEnvelope.DecodeHeader(encoded, 1024, out var header));
        Assert.Equal(default, header);
        AssertFailure(encoded, SaveReadStatus.PayloadTooLarge);
    }

    [Fact]
    public void Decode_RespectsCallerPayloadLimit()
    {
        Assert.Equal(SaveReadStatus.PayloadTooLarge,
            SaveEnvelope.Decode(GoldenEnvelope, 2, out var header, out var payload));
        Assert.Equal(default, header);
        Assert.True(payload.IsEmpty);
    }

    [Fact]
    public void Decode_RejectsTruncatedPayload()
    {
        AssertFailure(GoldenEnvelope.AsSpan(0, 34).ToArray(), SaveReadStatus.TruncatedPayload);
    }

    [Fact]
    public void Decode_RejectsTrailingBytes()
    {
        AssertFailure(GoldenEnvelope.Concat(new byte[] { 0 }).ToArray(), SaveReadStatus.TrailingData);
    }

    [Fact]
    public void Decode_WithDifferentSchema_ReturnsValidatedHeaderWithoutPayload()
    {
        Assert.Equal(SaveReadStatus.SchemaMismatch,
            SaveEnvelope.Decode(GoldenEnvelope, 3, out var header, out var payload, expectedSchemaVersion: 8));
        Assert.Equal(7u, header.SchemaVersion);
        Assert.True(payload.IsEmpty);
    }

    [Fact]
    public void Decode_WithInvalidExpectedSchema_RejectsCallerArgument()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SaveEnvelope.Decode(GoldenEnvelope, 3, out _, out _, expectedSchemaVersion: 0));
    }

    [Fact]
    public void DecodeHeader_OnlyValidatesStructureAndDoesNotClaimChecksumValidation()
    {
        byte[] headerOnly = GoldenEnvelope.AsSpan(0, SaveEnvelope.HeaderSize).ToArray();
        headerOnly[24] = 0;
        Assert.Equal(SaveReadStatus.Success, SaveEnvelope.DecodeHeader(headerOnly, 3, out var header));
        Assert.Equal(3ul, header.PayloadLength);
    }

    [Fact]
    public void WriteHeader_DoesNotModifyItsUnusedTail()
    {
        byte[] header = Enumerable.Repeat((byte)42, 40).ToArray();
        SaveEnvelope.WriteHeader(7, Encoding.ASCII.GetBytes("abc"), header);
        Assert.Equal(GoldenEnvelope.AsSpan(0, 32).ToArray(), header.AsSpan(0, 32).ToArray());
        Assert.All(header.Skip(32), value => Assert.Equal(42, value));
    }

    [Fact]
    public void DefaultReadResult_DoesNotRepresentSuccess()
    {
        SaveReadResult result = default;
        Assert.False(result.IsSuccess);
        Assert.Equal(SaveReadStatus.Unknown, result.Status);
    }

    private static void AssertFailure(byte[] encoded, SaveReadStatus expected)
    {
        Assert.Equal(expected, SaveEnvelope.Decode(encoded, 1024, out var header, out var payload));
        Assert.Equal(default, header);
        Assert.True(payload.IsEmpty);
    }
}
