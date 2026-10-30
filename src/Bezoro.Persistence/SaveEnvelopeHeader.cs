namespace Bezoro.Persistence;

/// <summary>Immutable metadata from a structurally valid envelope header.</summary>
/// <remarks><see cref="SaveEnvelope.DecodeHeader"/> validates structure only; complete decoding also validates the checksum.</remarks>
public readonly record struct SaveEnvelopeHeader
{
    internal SaveEnvelopeHeader(uint schemaVersion, ulong payloadLength, uint checksum)
    {
        FormatVersion = SaveEnvelope.FormatVersion;
        SchemaVersion = schemaVersion;
        PayloadLength = payloadLength;
        Checksum = checksum;
    }

    /// <summary>Gets the binary envelope format version.</summary>
    public ushort FormatVersion { get; }
    /// <summary>Gets the application's schema version, which starts at one.</summary>
    public uint SchemaVersion { get; }
    /// <summary>Gets the number of payload bytes, excluding the header.</summary>
    public ulong PayloadLength { get; }
    /// <summary>Gets the checksum over the header metadata and payload.</summary>
    public uint Checksum { get; }
}
