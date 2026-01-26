using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(Percent))]
public class PercentGetHashCodeMethodTests
{
	[Fact]
	public void WhenDifferentValue_ShouldReturnDifferentHashCode()
	{
		var a = new Percent(50);
		var b = new Percent(75);

		a.GetHashCode().Should().NotBe(b.GetHashCode());
	}

	[Fact]
	public void WhenSameValue_ShouldReturnSameHashCode()
	{
		var a = new Percent(50);
		var b = new Percent(50);

		a.GetHashCode().Should().Be(b.GetHashCode());
	}
}
