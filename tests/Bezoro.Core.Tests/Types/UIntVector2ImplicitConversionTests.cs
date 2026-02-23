using System.Numerics;
using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(UIntVector2))]
public class UIntVector2ImplicitConversionTests
{
	[Fact]
	public void ImplicitConversion_WhenToVector2_ShouldProduceSameComponents()
	{
		var u = new UIntVector2(12u, 34u);

		Vector2 v = u;

		v.X.Should().Be(12f);
		v.Y.Should().Be(34f);
	}
}
