using System;
using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(ArrayElementInfo<>))]
public class ArrayElementInfoTryFormatTests
{
#if NET6_0_OR_GREATER
	[Fact]
	public void WhenFound_WhenCalled_ShouldFormatCorrectly()
	{
		var        info   = ArrayElementInfo<string>.Found(1, "foo", 5);
		Span<char> buffer = stackalloc char[256];

		bool result = info.TryFormat(buffer, out int charsWritten, default, null);

		result.Should().BeTrue();
		buffer[..charsWritten].ToString().Should()
							  .Be("ArrayElementInfo<String> { Index = 1, Element = foo, ArrayLength = 5 }");
	}

	[Fact]
	public void WhenNotFound_WhenCalled_ShouldFormatCorrectly()
	{
		var        info   = ArrayElementInfo<string>.NotFound(null, 5);
		Span<char> buffer = stackalloc char[256];

		bool result = info.TryFormat(buffer, out int charsWritten, default, null);

		result.Should().BeTrue();
		buffer[..charsWritten].ToString().Should()
							  .Be("ArrayElementInfo<String> { NotFound, Element = <null>, ArrayLength = 5 }");
	}

	[Fact]
	public void WhenBufferTooSmall_WhenCalled_ShouldReturnFalse()
	{
		var        info   = ArrayElementInfo<string>.Found(1, "foo", 5);
		Span<char> buffer = stackalloc char[10];

		bool result = info.TryFormat(buffer, out int charsWritten, default, null);

		result.Should().BeFalse();
	}

	[Fact]
	public void ToStringWithFormat_WhenCalled_ShouldReturnSameAsToString()
	{
		var info = ArrayElementInfo<string>.Found(1, "foo", 5);

		var result = info.ToString(null, null);

		result.Should().Be(info.ToString());
	}
#endif
}
