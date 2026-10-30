using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using Xunit;

namespace Bezoro.Persistence.Tests;

public sealed class AtomicFileSaveStoreTests
{
    [Fact]
    public async Task WriteAndRead_RoundTripUsingCallerOwnedMemory()
    {
        using var directory = new TemporaryDirectory();
        var store = directory.CreateStore();
        byte[] payload = Encoding.UTF8.GetBytes("Game state, 日本語, café.");
        await store.WriteAsync("profile-1", 7, payload);
        byte[] destination = Enumerable.Repeat((byte)42, payload.Length + 5).ToArray();

        var read = await store.ReadAsync("profile-1", destination, expectedSchemaVersion: 7);

        Assert.True(read.IsSuccess);
        Assert.Equal(payload.Length, read.BytesWritten);
        Assert.Equal(payload.Length, read.RequiredCapacity);
        Assert.Equal(7u, read.Header.SchemaVersion);
        Assert.Equal(payload, destination.AsSpan(0, payload.Length).ToArray());
        Assert.All(destination.Skip(payload.Length), value => Assert.Equal(42, value));
        byte[] encoded = await File.ReadAllBytesAsync(directory.SlotPath("profile-1"));
        Assert.Equal(SaveReadStatus.Success, SaveEnvelope.Decode(encoded, 1024, out _, out var persisted));
        Assert.Equal(payload, persisted.ToArray());
        Assert.Empty(directory.TemporaryFiles());
    }

    [Fact]
    public async Task Write_AtomicallyReplacesAnExistingSlot()
    {
        using var directory = new TemporaryDirectory();
        var store = directory.CreateStore();
        await store.WriteAsync("profile", 1, new byte[] { 1, 2, 3 });
        await store.WriteAsync("profile", 2, new byte[] { 4, 5 });
        byte[] destination = new byte[8];
        var result = await store.ReadAsync("profile", destination);
        Assert.True(result.IsSuccess);
        Assert.Equal(2u, result.Header.SchemaVersion);
        Assert.Equal(new byte[] { 4, 5 }, destination.AsSpan(0, result.BytesWritten).ToArray());
        Assert.Empty(directory.TemporaryFiles());
    }

    [Fact]
    public async Task EmptyPayload_RespectsAZeroByteLimit()
    {
        using var directory = new TemporaryDirectory();
        var store = directory.CreateStore(maximumPayloadBytes: 0);
        await store.WriteAsync("empty", 1, ReadOnlyMemory<byte>.Empty);
        var read = await store.ReadAsync("empty", Memory<byte>.Empty);
        Assert.True(read.IsSuccess);
        Assert.Equal(0, read.BytesWritten);
        Assert.Equal(SaveEnvelope.HeaderSize, new FileInfo(directory.SlotPath("empty")).Length);
    }

    [Fact]
    public async Task MissingSlot_ReturnsNotFoundWithoutCreatingDirectoryOrChangingDestination()
    {
        using var directory = new TemporaryDirectory();
        string root = Path.Combine(directory.Root, "not-created");
        var store = new AtomicFileSaveStore(root, 128);
        byte[] destination = new byte[] { 1, 2, 3 };
        var read = await store.ReadAsync("missing", destination);
        Assert.Equal(SaveReadStatus.NotFound, read.Status);
        Assert.Equal(new byte[] { 1, 2, 3 }, destination);
        await store.DeleteAsync("missing");
        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public async Task Read_WithUndersizedDestination_ReportsBoundedRequiredCapacityWithoutWriting()
    {
        using var directory = new TemporaryDirectory();
        var store = directory.CreateStore();
        await store.WriteAsync("profile", 1, new byte[] { 1, 2, 3 });
        byte[] destination = new byte[] { 42, 42 };
        var read = await store.ReadAsync("profile", destination);
        Assert.Equal(SaveReadStatus.DestinationTooSmall, read.Status);
        Assert.Equal(3, read.RequiredCapacity);
        Assert.Equal(0, read.BytesWritten);
        Assert.Equal(default, read.Header);
        Assert.Equal(new byte[] { 42, 42 }, destination);
    }

    [Fact]
    public async Task Read_WithDifferentSchema_ReturnsValidatedVersionForAnExplicitMigrationDecision()
    {
        using var directory = new TemporaryDirectory();
        var store = directory.CreateStore();
        await store.WriteAsync("profile", 2, new byte[] { 1, 2, 3 });
        byte[] destination = new byte[3];
        var read = await store.ReadAsync("profile", destination, expectedSchemaVersion: 3);
        Assert.Equal(SaveReadStatus.SchemaMismatch, read.Status);
        Assert.Equal(2u, read.Header.SchemaVersion);
        Assert.Equal(0, read.BytesWritten);
        Assert.True((await store.ReadAsync("profile", destination)).IsSuccess);
    }

    [Theory]
    [InlineData(0, SaveReadStatus.HeaderTooShort)]
    [InlineData(31, SaveReadStatus.HeaderTooShort)]
    [InlineData(33, SaveReadStatus.TruncatedPayload)]
    [InlineData(36, SaveReadStatus.TrailingData)]
    public async Task Read_RejectsMalformedFileLengths(int length, SaveReadStatus expected)
    {
        using var directory = new TemporaryDirectory();
        var store = directory.CreateStore();
        byte[] encoded = new byte[35];
        SaveEnvelope.Write(1, new byte[3], encoded);
        Array.Resize(ref encoded, length);
        await File.WriteAllBytesAsync(directory.SlotPath("profile"), encoded);
        Assert.Equal(expected, (await store.ReadAsync("profile", new byte[8])).Status);
    }

    [Fact]
    public async Task Read_RejectsPayloadCorruption()
    {
        using var directory = new TemporaryDirectory();
        var store = directory.CreateStore();
        await store.WriteAsync("profile", 1, new byte[] { 1, 2, 3 });
        byte[] encoded = await File.ReadAllBytesAsync(directory.SlotPath("profile"));
        encoded[^1] ^= 1;
        await File.WriteAllBytesAsync(directory.SlotPath("profile"), encoded);
        var read = await store.ReadAsync("profile", new byte[3]);
        Assert.Equal(SaveReadStatus.ChecksumMismatch, read.Status);
        Assert.False(read.IsSuccess);
        Assert.Equal(0, read.BytesWritten);
    }

    [Fact]
    public async Task Read_RejectsUnboundedAdvertisedLengthBeforeTouchingCallerBuffer()
    {
        using var directory = new TemporaryDirectory();
        var store = directory.CreateStore(maximumPayloadBytes: 128);
        byte[] encoded = new byte[SaveEnvelope.HeaderSize];
        SaveEnvelope.WriteHeader(1, ReadOnlySpan<byte>.Empty, encoded);
        BinaryPrimitives.WriteUInt64LittleEndian(encoded.AsSpan(16), ulong.MaxValue);
        await File.WriteAllBytesAsync(directory.SlotPath("profile"), encoded);
        byte[] destination = new byte[] { 42 };
        var read = await store.ReadAsync("profile", destination);
        Assert.Equal(SaveReadStatus.PayloadTooLarge, read.Status);
        Assert.Equal(0, read.RequiredCapacity);
        Assert.Equal(new byte[] { 42 }, destination);
    }

    [Fact]
    public async Task Write_RejectsOversizedPayloadAndPreservesExistingSave()
    {
        using var directory = new TemporaryDirectory();
        var store = directory.CreateStore(maximumPayloadBytes: 2);
        await store.WriteAsync("profile", 1, new byte[] { 1 });
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.WriteAsync("profile", 1, new byte[3]).AsTask());
        byte[] destination = new byte[2];
        var read = await store.ReadAsync("profile", destination);
        Assert.Equal(1, read.BytesWritten);
        Assert.Equal(1, destination[0]);
        Assert.Empty(directory.TemporaryFiles());
    }

    [Fact]
    public async Task CanceledOperations_DoNotChangeTheExistingSave()
    {
        using var directory = new TemporaryDirectory();
        var store = directory.CreateStore();
        await store.WriteAsync("profile", 1, new byte[] { 1, 2, 3 });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            store.WriteAsync("profile", 1, new byte[] { 9 }, cancellation.Token).AsTask());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            store.ReadAsync("profile", new byte[3], cancellationToken: cancellation.Token).AsTask());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.DeleteAsync("profile", cancellation.Token).AsTask());
        byte[] destination = new byte[3];
        Assert.True((await store.ReadAsync("profile", destination)).IsSuccess);
        Assert.Equal(new byte[] { 1, 2, 3 }, destination);
        Assert.Empty(directory.TemporaryFiles());
    }

    [Fact]
    public async Task CancellationDuringPayloadIo_PreservesOldSaveAndRemovesTemporaryFile()
    {
        using var directory = new TemporaryDirectory();
        var store = directory.CreateStore(maximumPayloadBytes: 262_144);
        await store.WriteAsync("profile", 1, new byte[] { 1, 2, 3 });
        using var cancellation = new CancellationTokenSource();
        using var memory = new HookedMemory(new byte[131_072]);
        ReadOnlyMemory<byte> payload = memory.Memory;
        memory.AfterChecksum(cancellation.Cancel);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            store.WriteAsync("profile", 1, payload, cancellation.Token).AsTask());

        Assert.True(memory.HookInvoked);
        byte[] destination = new byte[3];
        Assert.True((await store.ReadAsync("profile", destination)).IsSuccess);
        Assert.Equal(new byte[] { 1, 2, 3 }, destination);
        Assert.Empty(directory.TemporaryFiles());
    }

    [Fact]
    public async Task FailureDuringPayloadIo_PreservesOldSaveAndRemovesTemporaryFile()
    {
        using var directory = new TemporaryDirectory();
        var store = directory.CreateStore(maximumPayloadBytes: 262_144);
        await store.WriteAsync("profile", 1, new byte[] { 1, 2, 3 });
        using var memory = new HookedMemory(new byte[131_072]);
        ReadOnlyMemory<byte> payload = memory.Memory;
        memory.AfterChecksum(static () => throw new IOException("Controlled payload access failure."));

        await Assert.ThrowsAsync<IOException>(() => store.WriteAsync("profile", 1, payload).AsTask());

        Assert.True(memory.HookInvoked);
        byte[] destination = new byte[3];
        Assert.True((await store.ReadAsync("profile", destination)).IsSuccess);
        Assert.Equal(new byte[] { 1, 2, 3 }, destination);
        Assert.Empty(directory.TemporaryFiles());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentWritersAndReaders_ObserveOnlyWholeValidSaves(bool multipleInstances)
    {
        using var directory = new TemporaryDirectory();
        var first = directory.CreateStore();
        var second = multipleInstances ? directory.CreateStore() : first;
        await first.WriteAsync("shared", 1, new byte[1024]);
        var writes = Enumerable.Range(1, 12).Select(value =>
            (value % 2 == 0 ? first : second).WriteAsync("shared", 1,
                Enumerable.Repeat((byte)value, 1024).ToArray()).AsTask()).ToArray();
        var reads = Enumerable.Range(0, 4).Select(_ => ReadRepeatedlyAsync()).ToArray();
        await Task.WhenAll(writes.Concat(reads));
        Assert.Empty(directory.TemporaryFiles());

        async Task ReadRepeatedlyAsync()
        {
            var buffer = new byte[1024];
            for (int iteration = 0; iteration < 20; iteration++)
            {
                var result = await second.ReadAsync("shared", buffer, expectedSchemaVersion: 1);
                Assert.True(result.IsSuccess, result.Status.ToString());
                Assert.Equal(1024, result.BytesWritten);
                byte value = buffer[0];
                Assert.InRange(value, (byte)0, (byte)12);
                Assert.All(buffer, item => Assert.Equal(value, item));
            }
        }
    }

    [Fact]
    public async Task Delete_IsIdempotentAndLeavesOtherSlotsIntact()
    {
        using var directory = new TemporaryDirectory();
        var store = directory.CreateStore();
        await store.WriteAsync("first", 1, new byte[] { 1 });
        await store.WriteAsync("second", 1, new byte[] { 2 });
        await store.DeleteAsync("first");
        await store.DeleteAsync("first");
        Assert.Equal(SaveReadStatus.NotFound, (await store.ReadAsync("first", new byte[1])).Status);
        byte[] destination = new byte[1];
        Assert.True((await store.ReadAsync("second", destination)).IsSuccess);
        Assert.Equal(2, destination[0]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("../outside")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("Profile")]
    [InlineData("a b")]
    [InlineData("a.b")]
    [InlineData("con")]
    [InlineData("nul")]
    [InlineData("com1")]
    [InlineData("lpt0")]
    [InlineData("日本語")]
    public async Task SlotNames_ArePortableAndCannotEscapeTheRoot(string? slot)
    {
        using var directory = new TemporaryDirectory();
        var store = directory.CreateStore();
        await Assert.ThrowsAsync<ArgumentException>(() => store.WriteAsync(slot!, 1, ReadOnlyMemory<byte>.Empty).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() => store.ReadAsync(slot!, Memory<byte>.Empty).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() => store.DeleteAsync(slot!).AsTask());
        Assert.Empty(Directory.EnumerateFileSystemEntries(directory.Root));
    }

    [Fact]
    public async Task SlotNames_RespectTheLengthBoundary()
    {
        using var directory = new TemporaryDirectory();
        var store = directory.CreateStore();
        await store.WriteAsync(new string('a', 64), 1, ReadOnlyMemory<byte>.Empty);
        await Assert.ThrowsAsync<ArgumentException>(() => store.WriteAsync(new string('a', 65), 1, ReadOnlyMemory<byte>.Empty).AsTask());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void Constructor_RejectsInvalidLimits(int maximumPayloadBytes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AtomicFileSaveStore(Path.GetTempPath(), maximumPayloadBytes));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RejectsAnEmptyDirectory(string? root)
    {
        Assert.Throws<ArgumentException>(() => new AtomicFileSaveStore(root!, 1));
    }

    [Fact]
    public async Task DefaultFlushToDisk_IsExercisedWithoutASeparateLifetimeToDispose()
    {
        using var directory = new TemporaryDirectory();
        var store = new AtomicFileSaveStore(directory.Root, 16);
        Assert.True(store.FlushToDisk);
        await store.WriteAsync("profile", 1, new byte[] { 1 });
        Assert.True((await store.ReadAsync("profile", new byte[1])).IsSuccess);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Root = Path.Combine(Path.GetTempPath(), "bezoro-persistence-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }
        public AtomicFileSaveStore CreateStore(int maximumPayloadBytes = 4096) => new(Root, maximumPayloadBytes, flushToDisk: false);
        public string SlotPath(string slot) => Path.Combine(Root, slot + ".bzsave");
        public IEnumerable<string> TemporaryFiles() => Directory.EnumerateFiles(Root, "*.tmp");
        public void Dispose() => Directory.Delete(Root, recursive: true);
    }

    private sealed class HookedMemory(byte[] bytes) : MemoryManager<byte>
    {
        private Action? _hook;
        private int _remaining;
        private int _invoked;

        public bool HookInvoked => Volatile.Read(ref _invoked) != 0;

        public void AfterChecksum(Action hook)
        {
            _remaining = 1;
            _hook = hook;
        }

        public override Span<byte> GetSpan()
        {
            Access();
            return bytes;
        }

        public override MemoryHandle Pin(int elementIndex = 0)
        {
            Access();
            return bytes.AsMemory(elementIndex).Pin();
        }

        public override void Unpin() { }
        protected override void Dispose(bool disposing) { }

        private void Access()
        {
            if (Volatile.Read(ref _hook) is null || Interlocked.Decrement(ref _remaining) >= 0) return;
            var hook = Interlocked.Exchange(ref _hook, null);
            if (hook is null) return;
            Volatile.Write(ref _invoked, 1);
            hook();
        }
    }
}
