using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(Percent))]
public class PercentStaticFieldsTests
{
	[Fact]
	public void Full_ShouldBe100()
	{
		Percent.Full.Value.Should().Be(100);
	}

	[Fact]
	public void Half_ShouldBe50()
	{
		Percent.Half.Value.Should().Be(50);
	}

	[Fact]
	public void Ninety_ShouldBe90()
	{
		Percent.Ninety.Value.Should().Be(90);
	}

	[Fact]
	public void Quarter_ShouldBe25()
	{
		Percent.Quarter.Value.Should().Be(25);
	}

	[Fact]
	public void ThreeQuarters_ShouldBe75()
	{
		Percent.ThreeQuarters.Value.Should().Be(75);
	}

	[Fact]
	public void Zero_ShouldBe0()
	{
		Percent.Zero.Value.Should().Be(0);
	}
}
