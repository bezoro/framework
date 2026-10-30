using System.Text;
using Xunit;

namespace Bezoro.Persistence.Tests;

public sealed class Crc32Tests
{
    [Theory]
    [InlineData("", 0u)]
    [InlineData("123456789", 0xCBF43926u)]
    [InlineData("The quick brown fox jumps over the lazy dog", 0x414FA339u)]
    public void Compute_MatchesIndependentPublishedVectors(string value, uint expected)
    {
        // CRC-32/ISO-HDLC catalogue check value: 123456789 -> CBF43926.
        Assert.Equal(expected, Crc32.Compute(Encoding.ASCII.GetBytes(value)));
    }

    [Fact]
    public void Append_ProducesTheSameChecksumForEverySplit()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("A portable save with multiple chunks: café, 日本語, 🎮.");
        uint expected = Crc32.Compute(bytes);
        for (int split = 0; split <= bytes.Length; split++)
            Assert.Equal(expected, Crc32.Append(Crc32.Compute(bytes.AsSpan(0, split)), bytes.AsSpan(split)));
    }
}
