# Binary span cursors

`BinarySpanWriter` and `BinarySpanReader` encode application-owned binary fields without allocating or growing storage. Integers use little-endian byte order. Float and double fields preserve their IEEE 754 bits, including signed zero, infinities, and NaN payloads. Applications decide whether those values are valid for their domain.

```csharp
using Bezoro.Core.Binary;

// This example allocates application storage once. Reuse it between checkpoints.
byte[] checkpoint = new byte[64];
var writer = new BinarySpanWriter(checkpoint);
if (!writer.TryWriteUInt64(simulationTick) ||
    !writer.TryWriteInt32(playerHealth) ||
    !writer.TryWriteSingle(playerX) ||
    !writer.TryWriteSingle(playerY))
    throw new InvalidOperationException("The checkpoint buffer is too small.");

int payloadLength = writer.Position;
// After the cursor's scope ends, the payload can be saved by ISaveStore:
// await store.WriteAsync("checkpoint", 1, checkpoint.AsMemory(0, payloadLength), cancellationToken);

var reader = new BinarySpanReader(checkpoint.AsSpan(0, payloadLength));
if (!reader.TryReadUInt64(out ulong restoredTick) ||
    !reader.TryReadInt32(out int restoredHealth) ||
    !reader.TryReadSingle(out float restoredX) ||
    !reader.TryReadSingle(out float restoredY) ||
    reader.Remaining != 0)
    throw new InvalidOperationException("The checkpoint layout is invalid.");
```

The cursors do not select serialization for `Bezoro.Persistence`. The application owns field order, schema versions, migration, and domain validation. A successful primitive read establishes only that the required bytes exist. Use the save envelope or a transport-specific integrity check when needed.

## Failure and length contracts

- Each Try operation either advances over the complete field or returns false without advancing. Read outputs are default on failure. Insufficient write capacity leaves the destination unchanged.
- Primitive fields are `Byte`, `Int16`, `UInt16`, `Int32`, `UInt32`, `Int64`, `UInt64`, `Single`, and `Double`. The `TryRead...` and `TryWrite...` names match those types.
- `TryReadBytes` returns a borrowed span, not a copied array. Negative requested lengths throw `ArgumentOutOfRangeException`.
- `TryWriteLengthPrefixedBytes` writes an Int32 byte count followed by the bytes. The complete field must fit. Source and destination may overlap, including overlap with the prefix.
- `TryReadLengthPrefixedBytes` requires an explicit inclusive `maximumLength`. A negative prefix, an excessive length, a truncated prefix, or a truncated payload returns false without exposing a field. A negative limit is a caller error and throws.
- Empty raw fields consume zero bytes. An empty length-prefixed field consumes four bytes. The default cursor is a valid empty buffer.

Atomic failure applies to one Try call. A sequence of successful calls followed by a failing call retains the earlier fields. Size a complete record before encoding if the application requires all-or-nothing publication of the entire record. For parsing, copy a reader as a tentative cursor and retain that copy only when the complete record succeeds. Copying a writer does not roll back writes.

## Borrowed memory and threads

These are mutable `ref struct` cursors because each operation advances a position over a span. Copies have independent positions over the same storage. `UnreadSpan`, `WrittenSpan`, and returned fields borrow that storage. A later writer, pool return, or disposal of an owner can invalidate their contents or lifetime.

Keep read storage unchanged while parsing. Serialize access to a shared destination, and keep the backing memory alive for every borrowed view. Cursors cannot be boxed, captured by a delegate, stored as fields of a class, or retained across an `await`. Encode synchronously, then pass the owned memory slice to an asynchronous save API. No implicit thread, buffer pool, byte-order detection, text encoding, or runtime type discovery is involved.

## Validation

Tests include an independent Python `struct` fixture, primitive failures at every truncated width, raw IEEE patterns, a seeded integer oracle, and overlapping source/destination arrangements. Allocation checks cover warmed cursor operations over reusable buffers. The [binary benchmarks](../../../benchmarks/Bezoro.Core.Benchmarks/BinaryBufferBenchmarks.cs) encode and decode a 41-byte gameplay record. They exclude storage creation and file I/O.
