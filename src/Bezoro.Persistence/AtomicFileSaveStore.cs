namespace Bezoro.Persistence;

/// <summary>Stores versioned envelopes using temporary files and same-directory replacement.</summary>
/// <remarks>
/// Intended for a trusted, application-owned directory on a local filesystem with atomic replacement support.
/// Operations own their streams and may run concurrently; same-process commits and deletes
/// for a normalized path share bounded synchronization. The last filesystem commit wins.
/// Atomic visibility is not a guarantee of survival across power loss or failure of a filesystem or storage device.
/// </remarks>
public sealed class AtomicFileSaveStore : ISaveStore
{
    private const int IoChunkSize = 65_536;
    private const int CommitRetryLimit = 8;
    private const int CommitShardCount = 64;
    private static readonly SemaphoreSlim[] CommitGates = CreateCommitGates();

    /// <summary>Creates a store configuration. The directory is created by the first write, not the constructor.</summary>
    /// <param name="rootDirectory">A trusted save directory, resolved to an absolute path at construction.</param>
    /// <param name="maximumPayloadBytes">An explicit payload limit, which may be zero for empty saves only.</param>
    /// <param name="flushToDisk">Whether to request an additional synchronous disk flush before committing a write.</param>
    public AtomicFileSaveStore(string rootDirectory, int maximumPayloadBytes, bool flushToDisk = true)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
            throw new ArgumentException("A save directory is required.", nameof(rootDirectory));
        SaveEnvelope.ValidateMaximumPayloadBytes(maximumPayloadBytes, nameof(maximumPayloadBytes));
        RootDirectory = Path.GetFullPath(rootDirectory);
        MaximumPayloadBytes = maximumPayloadBytes;
        FlushToDisk = flushToDisk;
    }

    /// <summary>Gets the absolute save-directory path.</summary>
    public string RootDirectory { get; }
    /// <summary>Gets the maximum payload size accepted by writes and read headers.</summary>
    public int MaximumPayloadBytes { get; }
    /// <summary>Gets whether writes request a synchronous disk flush before replacement.</summary>
    public bool FlushToDisk { get; }

    /// <inheritdoc />
    /// <remarks>
    /// Cancellation or failure before replacement preserves an existing save and removes this operation's temporary file.
    /// No cancellation check occurs after replacement: a committed write reports success.
    /// Checksumming runs on the calling execution context; async I/O does not automatically offload CPU work.
    /// </remarks>
    public async ValueTask WriteAsync(
        string slot, uint schemaVersion, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        string path = GetSlotPath(slot);
        if (payload.Length > MaximumPayloadBytes)
            throw new ArgumentOutOfRangeException(nameof(payload), "The payload exceeds the configured save limit.");
        cancellationToken.ThrowIfCancellationRequested();
        var header = new byte[SaveEnvelope.HeaderSize];
        SaveEnvelope.WriteHeader(schemaVersion, payload.Span, header, cancellationToken);
        RejectSymbolicLink(path);
        Directory.CreateDirectory(RootDirectory);
        string temporaryPath = Path.Combine(RootDirectory, $".{slot}.{Guid.NewGuid():N}.tmp");

        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                       8192, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await stream.WriteAsync(header.AsMemory(), cancellationToken).ConfigureAwait(false);
                for (int offset = 0; offset < payload.Length;)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int count = Math.Min(IoChunkSize, payload.Length - offset);
                    await stream.WriteAsync(payload.Slice(offset, count), cancellationToken).ConfigureAwait(false);
                    offset += count;
                }
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (FlushToDisk) stream.Flush(flushToDisk: true);
            }

            var gate = GetCommitGate(path);
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try { Commit(temporaryPath, path, cancellationToken); }
            finally { gate.Release(); }
        }
        catch (Exception writeFailure)
        {
            try { File.Delete(temporaryPath); }
            catch (Exception cleanupFailure)
            {
                throw new AggregateException("The save failed and its temporary file could not be removed.", writeFailure, cleanupFailure);
            }
            throw;
        }
    }

    /// <inheritdoc />
    public async ValueTask<SaveReadResult> ReadAsync(
        string slot, Memory<byte> destination, uint? expectedSchemaVersion = null, CancellationToken cancellationToken = default)
    {
        string path = GetSlotPath(slot);
        SaveEnvelope.ValidateExpectedSchema(expectedSchemaVersion);
        cancellationToken.ThrowIfCancellationRequested();
        RejectSymbolicLink(path);
        FileStream stream;
        try
        {
            stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete,
                8192, FileOptions.Asynchronous | FileOptions.SequentialScan);
        }
        catch (FileNotFoundException) { return new(SaveReadStatus.NotFound); }
        catch (DirectoryNotFoundException) { return new(SaveReadStatus.NotFound); }

        using (stream)
        {
            long fileLength = stream.Length;
            if (fileLength < SaveEnvelope.HeaderSize) return new(SaveReadStatus.HeaderTooShort);
            var headerBytes = new byte[SaveEnvelope.HeaderSize + 1];
            if (!await ReadExactlyAsync(stream, headerBytes.AsMemory(0, SaveEnvelope.HeaderSize), cancellationToken).ConfigureAwait(false))
                return new(SaveReadStatus.HeaderTooShort);
            var status = SaveEnvelope.DecodeHeader(headerBytes.AsSpan(0, SaveEnvelope.HeaderSize), MaximumPayloadBytes, out var header);
            if (status != SaveReadStatus.Success) return new(status);

            int length = (int)header.PayloadLength;
            long requiredLength = SaveEnvelope.HeaderSize + (long)length;
            if (fileLength < requiredLength) return new(SaveReadStatus.TruncatedPayload);
            if (fileLength > requiredLength) return new(SaveReadStatus.TrailingData);
            if (destination.Length < length)
                return new(SaveReadStatus.DestinationTooSmall, requiredCapacity: length);

            Memory<byte> payload = destination.Slice(0, length);
            if (!await ReadExactlyAsync(stream, payload, cancellationToken).ConfigureAwait(false))
                return new(SaveReadStatus.TruncatedPayload);
            if (await stream.ReadAsync(headerBytes.AsMemory(SaveEnvelope.HeaderSize, 1), cancellationToken).ConfigureAwait(false) != 0)
                return new(SaveReadStatus.TrailingData);
            if (SaveEnvelope.ComputeChecksum(headerBytes.AsSpan(0, SaveEnvelope.HeaderSize), payload.Span, cancellationToken) != header.Checksum)
                return new(SaveReadStatus.ChecksumMismatch);
            if (expectedSchemaVersion.HasValue && expectedSchemaVersion.Value != header.SchemaVersion)
                return new(SaveReadStatus.SchemaMismatch, header, length);
            return new(SaveReadStatus.Success, header, length);
        }
    }

    /// <inheritdoc />
    /// <remarks>The commit-gate wait is asynchronous and cancelable; the final filesystem unlink is synchronous.</remarks>
    public async ValueTask DeleteAsync(string slot, CancellationToken cancellationToken = default)
    {
        string path = GetSlotPath(slot);
        cancellationToken.ThrowIfCancellationRequested();
        var gate = GetCommitGate(path);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            RejectSymbolicLink(path);
            try { File.Delete(path); }
            catch (DirectoryNotFoundException) { }
        }
        finally { gate.Release(); }
    }

    private string GetSlotPath(string slot)
    {
        if (string.IsNullOrEmpty(slot) || slot.Length > 64)
            throw new ArgumentException("A save slot must contain between one and 64 lowercase ASCII letters, digits, underscores or hyphens.", nameof(slot));
        foreach (char character in slot)
            if (character is not (>= 'a' and <= 'z') and not (>= '0' and <= '9') and not '_' and not '-')
                throw new ArgumentException("Save slot names use lowercase ASCII letters, digits, underscores and hyphens only.", nameof(slot));
        if (slot is "con" or "prn" or "aux" or "nul" ||
            (slot.Length == 4 && (slot.StartsWith("com", StringComparison.Ordinal) || slot.StartsWith("lpt", StringComparison.Ordinal)) &&
             slot[3] is >= '0' and <= '9'))
            throw new ArgumentException("The save slot is a reserved portable filename.", nameof(slot));
        return Path.Combine(RootDirectory, slot + ".bzsave");
    }

    private static void RejectSymbolicLink(string path)
    {
        try
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Symbolic-link save slots are not supported.");
        }
        catch (FileNotFoundException) { }
        catch (DirectoryNotFoundException) { }
    }

    private static SemaphoreSlim GetCommitGate(string path)
    {
        // Case-insensitive hashing coordinates Windows path casing. On case-sensitive
        // filesystems it can only serialize extra paths, just like any shard collision.
        uint hash = unchecked((uint)StringComparer.OrdinalIgnoreCase.GetHashCode(path));
        return CommitGates[hash & (CommitShardCount - 1)];
    }

    private static SemaphoreSlim[] CreateCommitGates()
    {
        var gates = new SemaphoreSlim[CommitShardCount];
        for (int index = 0; index < gates.Length; index++) gates[index] = new(1, 1);
        return gates;
    }

    private static void Commit(string temporaryPath, string destinationPath, CancellationToken cancellationToken)
    {
        IOException? contention = null;
        for (int attempt = 0; attempt < CommitRetryLimit; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(destinationPath))
            {
                try
                {
                    File.Replace(temporaryPath, destinationPath, destinationBackupFileName: null);
                    return;
                }
                catch (FileNotFoundException error) when (File.Exists(temporaryPath))
                {
                    // Another operation deleted the destination after the existence check.
                    contention = error;
                }
            }
            else
            {
                try
                {
                    File.Move(temporaryPath, destinationPath);
                    return;
                }
                catch (IOException error) when (File.Exists(temporaryPath))
                {
                    // A competing create/delete can change the destination again before
                    // an existence check. Keep the temporary source and retry boundedly.
                    contention = error;
                }
            }
        }
        throw new IOException("Atomic save replacement failed after repeated commit attempts.", contention);
    }

    private static async ValueTask<bool> ReadExactlyAsync(Stream stream, Memory<byte> destination, CancellationToken cancellationToken)
    {
        for (int offset = 0; offset < destination.Length;)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int count = Math.Min(IoChunkSize, destination.Length - offset);
            int read = await stream.ReadAsync(destination.Slice(offset, count), cancellationToken).ConfigureAwait(false);
            if (read == 0) return false;
            offset += read;
        }
        return true;
    }
}
