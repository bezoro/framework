namespace Bezoro.Persistence;

/// <summary>Stores serialized payloads without selecting an application's serialization or migration policy.</summary>
public interface ISaveStore
{
    /// <summary>Writes a payload. The caller must keep its memory unchanged until the operation completes.</summary>
    ValueTask WriteAsync(string slot, uint schemaVersion, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default);

    /// <summary>Reads into caller-owned memory. Consume output only when the result succeeds.</summary>
    /// <remarks>The destination may be modified on failure or cancellation. Its memory is exclusively borrowed until completion.</remarks>
    ValueTask<SaveReadResult> ReadAsync(
        string slot, Memory<byte> destination, uint? expectedSchemaVersion = null, CancellationToken cancellationToken = default);

    /// <summary>Deletes a slot if present.</summary>
    ValueTask DeleteAsync(string slot, CancellationToken cancellationToken = default);
}
