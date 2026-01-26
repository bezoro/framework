using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(Percent))]
public class PercentInequalityOperatorTests
{
	[Fact]
	public void WhenEqual_ShouldReturnFalse()
	{
		var a = new Percent(50);
		var b = new Percent(50);

		(a != b).Should().BeFalse();
	}

	[Fact]
	public void WhenNotEqual_ShouldReturnTrue()
	{
		var a = new Percent(50);
		var b = new Percent(75);

		(a != b).Should().BeTrue();
	}
}
