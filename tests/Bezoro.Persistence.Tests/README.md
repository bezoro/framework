# Bezoro.Persistence.Tests

Tests for the engine-independent save envelope and file-store contracts. CRC vectors use IEEE CRC-32 reference values; the binary envelope fixture was generated independently with Python `struct` and `zlib`.

Filesystem tests use their own temporary directories and remove them after awaiting all operations. A controlled `MemoryManager<byte>` introduces cancellation or failure after checksumming, during payload I/O, without sleeps. Concurrency tests verify that readers see checksum-valid whole saves while one or multiple store instances replace a slot. A controlled path-gate hold verifies that queued snapshot reads and deletes cancel without releasing another operation's gate, modifying the destination buffer, or removing the save.

```sh
dotnet test tests/Bezoro.Persistence.Tests/Bezoro.Persistence.Tests.csproj -c Release
```

These tests run against .NET on the host filesystem. They do not validate a Unity player, IL2CPP, Godot export, browser storage, or a console save API. See the [module contracts and format](../../src/Bezoro.Persistence/README.md).
