using System;
using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(Percent))]
public class PercentExplicitCastFromByteTests
{
	[Fact]
	public void WhenInvalidByte_ShouldThrow()
	{
		byte value = 101;

		var act = () => (Percent)value;

		act.Should().Throw<ArgumentOutOfRangeException>();
	}

	[Fact]
	public void WhenValidByte_ShouldCreatePercent()
	{
		byte value = 42;

		var p = (Percent)value;

		p.Value.Should().Be(42);
	}
}
