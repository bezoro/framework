using System;
using BenchmarkDotNet.Attributes;
using Bezoro.Core.Binary;

namespace Bezoro.Core.Benchmarks;

/// <summary>Encodes and decodes one small gameplay record using reusable byte storage.</summary>
[MemoryDiagnoser]
public class BinaryBufferBenchmarks
{
    private readonly byte[] _destination = new byte[64];
    private readonly byte[] _encoded = new byte[41];
    private readonly byte[] _payload = new byte[16];

    [GlobalSetup]
    public void Setup()
    {
        for (int index = 0; index < _payload.Length; index++) _payload[index] = (byte)index;
        var writer = new BinarySpanWriter(_encoded);
        if (!WriteRecord(ref writer) || writer.Position != _encoded.Length)
            throw new InvalidOperationException("The benchmark record must fill its encoded fixture.");
    }

    [Benchmark]
    public int WriteGameplayRecord()
    {
        var writer = new BinarySpanWriter(_destination);
        WriteRecord(ref writer);
        return writer.Position;
    }

    [Benchmark]
    public double ReadGameplayRecord()
    {
        var reader = new BinarySpanReader(_encoded);
        reader.TryReadByte(out byte kind);
        reader.TryReadUInt32(out uint actor);
        reader.TryReadUInt64(out ulong tick);
        reader.TryReadSingle(out float x);
        reader.TryReadSingle(out float y);
        reader.TryReadLengthPrefixedBytes(16, out var payload);
        return kind + actor + tick + x + y + payload[15];
    }

    private bool WriteRecord(ref BinarySpanWriter writer) =>
        writer.TryWriteByte(2) &&
        writer.TryWriteUInt32(42) &&
        writer.TryWriteUInt64(1_000_000) &&
        writer.TryWriteSingle(-12.5f) &&
        writer.TryWriteSingle(7.25f) &&
        writer.TryWriteLengthPrefixedBytes(_payload);
}
