namespace Bezoro.Persistence;

/// <summary>Describes a save-envelope validation or store-read outcome.</summary>
public enum SaveReadStatus
{
    /// <summary>No read has been performed.</summary>
    Unknown,
    /// <summary>The complete envelope and its checksum are valid.</summary>
    Success,
    /// <summary>The requested save slot does not exist.</summary>
    NotFound,
    /// <summary>There are fewer bytes than the fixed header requires.</summary>
    HeaderTooShort,
    /// <summary>The header does not identify a Bezoro save envelope.</summary>
    InvalidMagic,
    /// <summary>The envelope format version is not supported by this codec.</summary>
    UnsupportedFormatVersion,
    /// <summary>The header size, reserved fields, or schema version are invalid.</summary>
    InvalidHeader,
    /// <summary>The declared payload exceeds the caller's configured limit.</summary>
    PayloadTooLarge,
    /// <summary>The payload is shorter than its declared length.</summary>
    TruncatedPayload,
    /// <summary>Bytes follow the declared payload.</summary>
    TrailingData,
    /// <summary>The header or payload has a different checksum from the stored value.</summary>
    ChecksumMismatch,
    /// <summary>A valid envelope uses a schema version different from the requested version.</summary>
    SchemaMismatch,
    /// <summary>The caller's output buffer cannot hold the declared payload.</summary>
    DestinationTooSmall
}
