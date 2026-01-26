using System.Numerics;
using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(UIntVector2))]
public class UIntVector2ToVector2Tests
{
	[Fact]
	public void WhenCalled_ShouldProduceSameComponents()
	{
		var u = new UIntVector2(5u, 6u);

		var v = u.ToVector2();

		v.Should().Be(new Vector2(5f, 6f));
	}
}
