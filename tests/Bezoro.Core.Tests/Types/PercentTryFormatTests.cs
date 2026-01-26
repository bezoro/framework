using System;
using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(Percent))]
public class PercentTryFormatTests
{
#if NET6_0_OR_GREATER
	[Fact]
	public void WhenBufferSufficient_ShouldFormatAndReturnTrue()
	{
		var        p      = new Percent(42);
		Span<char> buffer = stackalloc char[4];

		bool result = p.TryFormat(buffer, out int charsWritten, default, null);

		result.Should().BeTrue();
		charsWritten.Should().Be(3);
		buffer[..charsWritten].ToString().Should().Be("42%");
	}

	[Fact]
	public void WhenBufferTooSmall_ShouldReturnFalse()
	{
		var        p      = new Percent(100);
		Span<char> buffer = stackalloc char[3];

		bool result = p.TryFormat(buffer, out int charsWritten, default, null);

		result.Should().BeFalse();
	}

	[Fact]
	public void WhenFull_ShouldFormat100Percent()
	{
		Span<char> buffer = stackalloc char[4];

		bool result = Percent.Full.TryFormat(buffer, out int charsWritten, default, null);

		result.Should().BeTrue();
		charsWritten.Should().Be(4);
		buffer[..charsWritten].ToString().Should().Be("100%");
	}

	[Fact]
	public void WhenZero_ShouldFormat0Percent()
	{
		Span<char> buffer = stackalloc char[4];

		bool result = Percent.Zero.TryFormat(buffer, out int charsWritten, default, null);

		result.Should().BeTrue();
		charsWritten.Should().Be(2);
		buffer[..charsWritten].ToString().Should().Be("0%");
	}
#endif
}
