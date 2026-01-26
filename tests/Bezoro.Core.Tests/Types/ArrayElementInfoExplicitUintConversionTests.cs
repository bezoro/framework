using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(ArrayElementInfo<>))]
public class ArrayElementInfoExplicitUintConversionTests
{
	[Fact]
	public void WhenFound_ShouldReturnIndex()
	{
		var info = ArrayElementInfo<string>.Found(3, "hello", 10);

		var index = (uint?)info;

		index.Should().Be(3);
	}

	[Fact]
	public void WhenNotFound_ShouldReturnNull()
	{
		var info = ArrayElementInfo<string>.NotFound("missing", 10);

		var index = (uint?)info;

		index.Should().BeNull();
	}
}
