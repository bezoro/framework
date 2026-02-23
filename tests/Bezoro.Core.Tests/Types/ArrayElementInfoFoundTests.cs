using System;
using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(ArrayElementInfo<>))]
public class ArrayElementInfoFoundTests
{
	[Fact]
	public void WhenFound_WhenCalled_ShouldCreateFoundInstance()
	{
		var info = ArrayElementInfo<string>.Found(2, "hello", 10);

		info.Index.Should().Be(2);
		info.IsFound.Should().BeTrue();
		info.Element.Should().Be("hello");
		info.ArrayLength.Should().Be(10);
	}

	[Fact]
	public void WhenIndexOutOfRange_WhenCalled_ShouldThrow()
	{
		var act = () => ArrayElementInfo<string>.Found(10, "test", 5);

		act.Should().Throw<ArgumentOutOfRangeException>()
		   .WithParameterName("index");
	}
}
