namespace Bezoro.Persistence;

/// <summary>Describes a read into a caller-owned buffer. A default result is not successful.</summary>
public readonly record struct SaveReadResult
{
    internal SaveReadResult(SaveReadStatus status, SaveEnvelopeHeader header = default, int requiredCapacity = 0)
    {
        Status = status;
        Header = header;
        RequiredCapacity = requiredCapacity;
        BytesWritten = status == SaveReadStatus.Success ? requiredCapacity : 0;
    }

    /// <summary>Gets the read outcome.</summary>
    public SaveReadStatus Status { get; }
    /// <summary>Gets whether the complete save passed validation and any requested schema check.</summary>
    public bool IsSuccess => Status == SaveReadStatus.Success;
    /// <summary>Gets checksum-validated metadata on success or schema mismatch; otherwise the value is default.</summary>
    public SaveEnvelopeHeader Header { get; }
    /// <summary>Gets the payload bytes available for consumption. This is zero on failure.</summary>
    public int BytesWritten { get; }
    /// <summary>Gets the bounded payload size on success, schema mismatch, or an undersized destination.</summary>
    /// <remarks>An undersized-destination result does not validate the payload checksum.</remarks>
    public int RequiredCapacity { get; }
}
