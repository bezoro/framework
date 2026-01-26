using System.Numerics;
using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(UIntVector2))]
public class UIntVector2TryFromVector2Tests
{
	[Fact]
	public void WhenInvalid_ShouldReturnFalseAndNull()
	{
		var v = new Vector2(-1f, 0f);

		bool ok = UIntVector2.TryFromVector2(v, out var result);

		ok.Should().BeFalse();
		result.Should().Be(null);
	}

	[Fact]
	public void WhenValid_ShouldReturnTrueAndResult()
	{
		var v = new Vector2(8f, 9f);

		bool ok = UIntVector2.TryFromVector2(v, out var result);

		ok.Should().BeTrue();
		result.Should().Be(new UIntVector2(8u, 9u));
	}
}
