using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(Percent))]
public class PercentEqualsMethodTests
{
	[Fact]
	public void WhenDifferentValue_ShouldReturnFalse()
	{
		var a = new Percent(50);
		var b = new Percent(75);

		a.Equals(b).Should().BeFalse();
	}

	[Fact]
	public void WhenObjectIsDifferentType_ShouldReturnFalse()
	{
		var    a = new Percent(50);
		object b = 50;

		a.Equals(b).Should().BeFalse();
	}

	[Fact]
	public void WhenObjectIsNull_ShouldReturnFalse()
	{
		var a = new Percent(50);

		a.Equals(null).Should().BeFalse();
	}

	[Fact]
	public void WhenObjectIsSameValue_ShouldReturnTrue()
	{
		var    a = new Percent(50);
		object b = new Percent(50);

		a.Equals(b).Should().BeTrue();
	}

	[Fact]
	public void WhenSameValue_ShouldReturnTrue()
	{
		var a = new Percent(50);
		var b = new Percent(50);

		a.Equals(b).Should().BeTrue();
	}
}
