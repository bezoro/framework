using BenchmarkDotNet.Attributes;

namespace Bezoro.Persistence.Benchmarks;

[MemoryDiagnoser]
public class SaveEnvelopeBenchmarks
{
    private byte[] _payload = null!;
    private byte[] _encoded = null!;

    [Params(256, 4096, 65536)]
    public int PayloadBytes { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _payload = new byte[PayloadBytes];
        for (int index = 0; index < _payload.Length; index++) _payload[index] = (byte)index;
        _encoded = new byte[SaveEnvelope.GetEncodedLength(PayloadBytes)];
        SaveEnvelope.Write(1, _payload, _encoded);
    }

    [Benchmark]
    public uint Checksum() => Crc32.Compute(_payload);

    [Benchmark]
    public int Encode() => SaveEnvelope.Write(1, _payload, _encoded);

    [Benchmark]
    public SaveReadStatus Validate() => SaveEnvelope.Decode(_encoded, PayloadBytes, out _, out _);
}
