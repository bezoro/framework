using System;
using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(Percent))]
public class PercentConstructorTests
{
	[Fact]
	public void WhenBoundaryValue0_ShouldCreateObject()
	{
		var p = new Percent(0);

		p.Value.Should().Be(0);
	}

	[Fact]
	public void WhenBoundaryValue100_ShouldCreateObject()
	{
		var p = new Percent(100);

		p.Value.Should().Be(100);
	}

	[Fact]
	public void WhenValidValue_ShouldCreateObject()
	{
		var p = new Percent(10);

		p.Value.Should().Be(10);
	}

	[Fact]
	public void WhenValueOver100_ShouldThrow()
	{
		var act = () => new Percent(101);

		act.Should().Throw<ArgumentOutOfRangeException>();
	}
}
