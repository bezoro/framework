using System.Buffers.Binary;

namespace Bezoro.Persistence;

/// <summary>Encodes and validates a versioned, little-endian save envelope without allocating payload buffers.</summary>
public static class SaveEnvelope
{
    private const ulong Magic = 0x0045564153525A42UL; // ASCII "BZRSAVE\0".
    private const int ChecksumOffset = 24;
    private const int ReservedOffset = 28;
    private const int ChecksumChunkSize = 65_536;

    /// <summary>The supported binary format version.</summary>
    public const ushort FormatVersion = 1;
    /// <summary>The fixed header size, in bytes.</summary>
    public const int HeaderSize = 32;
    /// <summary>The largest payload whose entire encoded envelope fits a span.</summary>
    public const int MaximumPayloadLength = int.MaxValue - HeaderSize;

    /// <summary>Returns the complete encoded length, rejecting negative or unrepresentable payload lengths.</summary>
    public static int GetEncodedLength(int payloadLength)
    {
        ValidateMaximumPayloadBytes(payloadLength, nameof(payloadLength));
        return HeaderSize + payloadLength;
    }

    /// <summary>Writes an envelope and returns the bytes written. Any unused destination tail is unchanged.</summary>
    /// <remarks>Source and destination may overlap. Invalid arguments and insufficient capacity do not change the destination.</remarks>
    public static int Write(uint schemaVersion, ReadOnlySpan<byte> payload, Span<byte> destination)
    {
        int required = GetEncodedLength(payload.Length);
        if (destination.Length < required)
            throw new ArgumentException("The destination cannot hold the complete save envelope.", nameof(destination));

        Span<byte> header = stackalloc byte[HeaderSize];
        WriteHeader(schemaVersion, payload, header);
        payload.CopyTo(destination.Slice(HeaderSize, payload.Length));
        header.CopyTo(destination);
        return required;
    }

    /// <summary>Writes only the fixed header for the supplied payload.</summary>
    /// <remarks>The checksum includes the payload, so the same unchanged payload must follow this header.</remarks>
    public static void WriteHeader(uint schemaVersion, ReadOnlySpan<byte> payload, Span<byte> destination) =>
        WriteHeader(schemaVersion, payload, destination, CancellationToken.None);

    /// <summary>Validates a complete envelope and returns a borrowed payload span on success.</summary>
    /// <remarks>
    /// On schema mismatch the header is checksum-validated and the payload output is empty.
    /// On every other failure both outputs are default. A null expected schema permits the application to select a migration.
    /// </remarks>
    public static SaveReadStatus Decode(
        ReadOnlySpan<byte> source, int maximumPayloadBytes,
        out SaveEnvelopeHeader header, out ReadOnlySpan<byte> payload,
        uint? expectedSchemaVersion = null)
    {
        ValidateExpectedSchema(expectedSchemaVersion);
        header = default;
        payload = default;
        SaveReadStatus status = DecodeHeader(source, maximumPayloadBytes, out var parsed);
        if (status != SaveReadStatus.Success) return status;

        int length = (int)parsed.PayloadLength;
        int available = source.Length - HeaderSize;
        if (available < length) return SaveReadStatus.TruncatedPayload;
        if (available > length) return SaveReadStatus.TrailingData;
        var contents = source.Slice(HeaderSize, length);
        if (ComputeChecksum(source.Slice(0, HeaderSize), contents, CancellationToken.None) != parsed.Checksum)
            return SaveReadStatus.ChecksumMismatch;

        header = parsed;
        if (expectedSchemaVersion.HasValue && expectedSchemaVersion.Value != parsed.SchemaVersion)
            return SaveReadStatus.SchemaMismatch;
        payload = contents;
        return SaveReadStatus.Success;
    }

    /// <summary>Validates fixed-header structure and bounds before any payload allocation or read.</summary>
    /// <remarks>This does not validate the checksum or require payload bytes. Bytes beyond the header are ignored.</remarks>
    public static SaveReadStatus DecodeHeader(
        ReadOnlySpan<byte> source, int maximumPayloadBytes, out SaveEnvelopeHeader header)
    {
        ValidateMaximumPayloadBytes(maximumPayloadBytes, nameof(maximumPayloadBytes));
        header = default;
        if (source.Length < HeaderSize) return SaveReadStatus.HeaderTooShort;
        if (BinaryPrimitives.ReadUInt64LittleEndian(source) != Magic) return SaveReadStatus.InvalidMagic;
        if (BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(8)) != FormatVersion)
            return SaveReadStatus.UnsupportedFormatVersion;
        if (BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(10)) != HeaderSize ||
            BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(ReservedOffset)) != 0)
            return SaveReadStatus.InvalidHeader;

        uint schemaVersion = BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(12));
        if (schemaVersion == 0) return SaveReadStatus.InvalidHeader;
        ulong length = BinaryPrimitives.ReadUInt64LittleEndian(source.Slice(16));
        if (length > (ulong)maximumPayloadBytes) return SaveReadStatus.PayloadTooLarge;
        header = new(schemaVersion, length, BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(ChecksumOffset)));
        return SaveReadStatus.Success;
    }

    internal static void ValidateMaximumPayloadBytes(int value, string parameterName)
    {
        if (value < 0 || value > MaximumPayloadLength)
            throw new ArgumentOutOfRangeException(parameterName, "The payload limit must fit a complete envelope in a span.");
    }

    internal static void ValidateExpectedSchema(uint? expectedSchemaVersion)
    {
        if (expectedSchemaVersion == 0)
            throw new ArgumentOutOfRangeException(nameof(expectedSchemaVersion), "Schema versions start at one.");
    }

    internal static void WriteHeader(
        uint schemaVersion, ReadOnlySpan<byte> payload, Span<byte> destination, CancellationToken cancellationToken)
    {
        if (schemaVersion == 0)
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), "Schema versions start at one.");
        GetEncodedLength(payload.Length);
        if (destination.Length < HeaderSize)
            throw new ArgumentException("The destination cannot hold the fixed header.", nameof(destination));

        Span<byte> header = stackalloc byte[HeaderSize];
        header.Clear();
        BinaryPrimitives.WriteUInt64LittleEndian(header, Magic);
        BinaryPrimitives.WriteUInt16LittleEndian(header.Slice(8), FormatVersion);
        BinaryPrimitives.WriteUInt16LittleEndian(header.Slice(10), HeaderSize);
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(12), schemaVersion);
        BinaryPrimitives.WriteUInt64LittleEndian(header.Slice(16), (ulong)payload.Length);
        uint checksum = ComputeChecksum(header, payload, cancellationToken);
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(ChecksumOffset), checksum);
        header.CopyTo(destination);
    }

    internal static uint ComputeChecksum(
        ReadOnlySpan<byte> header, ReadOnlySpan<byte> payload, CancellationToken cancellationToken)
    {
        uint checksum = Crc32.Compute(header.Slice(0, ChecksumOffset));
        checksum = Crc32.Append(checksum, header.Slice(ReservedOffset, sizeof(uint)));
        for (int offset = 0; offset < payload.Length;)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int count = Math.Min(ChecksumChunkSize, payload.Length - offset);
            checksum = Crc32.Append(checksum, payload.Slice(offset, count));
            offset += count;
        }
        cancellationToken.ThrowIfCancellationRequested();
        return checksum;
    }
}
