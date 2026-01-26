using System;
using System.Numerics;
using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(UIntVector2))]
public class UIntVector2ExplicitConversionTests
{
	[Fact]
	public void WhenInvalidFromVector2_ShouldThrow()
	{
		var v = new Vector2(1.5f, 2f);

		var act = () => { _ = (UIntVector2)v; };

		act.Should().Throw<ArgumentOutOfRangeException>();
	}

	[Fact]
	public void WhenValidFromVector2_ShouldSucceed()
	{
		var v = new Vector2(4f, 2f);

		var u = (UIntVector2)v;

		u.Should().Be(new UIntVector2(4u, 2u));
	}
}
