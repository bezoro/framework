using Bezoro.GameSystems.ActivationSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.GameSystems.Tests.ActivationSystem;

[TestSubject(typeof(ActivationHandle))]
public class ActivationHandleTests
{
	[Fact]
	public void None_ShouldHaveIdZero()
	{
		ActivationHandle.None.Id.Should().Be(0);
	}

	[Fact]
	public void None_ShouldNotBeValid()
	{
		ActivationHandle.None.IsValid.Should().BeFalse();
	}

	[Fact]
	public void WhenIdIsPositive_ShouldBeValid()
	{
		var handle = new ActivationHandle(1);

		handle.IsValid.Should().BeTrue();
	}

	[Fact]
	public void WhenSameId_ShouldBeEqual()
	{
		var a = new ActivationHandle(42);
		var b = new ActivationHandle(42);

		a.Should().Be(b);
		(a == b).Should().BeTrue();
		(a != b).Should().BeFalse();
	}

	[Fact]
	public void WhenDifferentId_ShouldNotBeEqual()
	{
		var a = new ActivationHandle(1);
		var b = new ActivationHandle(2);

		a.Should().NotBe(b);
		(a == b).Should().BeFalse();
		(a != b).Should().BeTrue();
	}

	[Fact]
	public void WhenSameId_ShouldHaveSameHashCode()
	{
		var a = new ActivationHandle(42);
		var b = new ActivationHandle(42);

		a.GetHashCode().Should().Be(b.GetHashCode());
	}

	[Fact]
	public void ToString_ShouldContainId()
	{
		var handle = new ActivationHandle(7);

		handle.ToString().Should().Be("Activation(7)");
	}

	[Fact]
	public void WhenComparedToObject_ShouldHandleCorrectly()
	{
		var handle = new ActivationHandle(1);

		handle.Equals((object)new ActivationHandle(1)).Should().BeTrue();
		handle.Equals((object)new ActivationHandle(2)).Should().BeFalse();
		handle.Equals("not a handle").Should().BeFalse();
	}

	[Fact]
	public void Default_ShouldEqualNone()
	{
		ActivationHandle handle = default;

		handle.Should().Be(ActivationHandle.None);
	}
}
