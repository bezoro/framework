# Toolkit console example

A single-owner host queries nearby pickups, inserts coins into a bounded inventory, advances a pure quest state machine, transfers the complete balance to a stash, and restores a byte checkpoint. A BitSet records the completed state. The process also executes both C# 9 API consumers, rather than only compiling them.

```sh
dotnet run -c Release --project samples/Bezoro.Toolkit.ConsoleDemo
```

Expected output includes three collected coins, one distant pickup, quest state 2, three stashed coins, and a 16-byte checkpoint. Initialization and restore allocate objects; the example does not claim zero allocation for the whole process.

The schema uses explicit little-endian fields. `BinarySpanReader` bounds reads but the application still validates state IDs, slot count, catalog IDs, and stack limits. A storage adapter can wrap these bytes in `Bezoro.Persistence.SaveEnvelope` and select its own application schema version. Byte cursors do not replace schema validation.

The host serializes inventory and spatial mutations. Individual inventory transfers are atomic with respect to validation failures; the pickup phase across two modules is application orchestration and requires exclusive ownership. Production hosts should define rollback or command policies if callbacks or other writers can interleave.

Unity and Godot can call the same data APIs from engine-specific adapters. This example does not access scene objects or establish player/export compatibility.
