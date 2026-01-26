using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(ArrayElementInfo<>))]
public class ArrayElementInfoToStringTests
{
	[Fact]
	public void WhenNullElement_ShouldFormatAsNullString()
	{
		var info = new ArrayElementInfo<string>(null, null, 5);

		info.ToString().Should().Be("ArrayElementInfo<String> { NotFound, Element = <null>, ArrayLength = 5 }");
	}

	[Fact]
	public void WhenValidElements_ShouldFormatAsExpected()
	{
		var info = new ArrayElementInfo<string>(1, "foo", 5);

		info.ToString().Should().Be("ArrayElementInfo<String> { Index = 1, Element = foo, ArrayLength = 5 }");
	}
}
