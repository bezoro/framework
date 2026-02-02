using Bezoro.Events.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Events.Tests.Types;

[TestSubject(typeof(SubscriptionHandle))]
public static class SubscriptionHandleTests
{
	public class Unit
	{
		[Fact]
		public void Constructor_WithNegativeId_ShouldNotBeValid()
		{
			var handle = new SubscriptionHandle(-1);
			handle.IsValid.Should().BeFalse();
		}

		[Fact]
		public void Constructor_WithPositiveId_ShouldBeValid()
		{
			var handle = new SubscriptionHandle(1);
			handle.IsValid.Should().BeTrue();
		}

		[Fact]
		public void Default_ShouldEqualNone()
		{
			SubscriptionHandle handle = default;
			(handle == SubscriptionHandle.None).Should().BeTrue();
		}

		[Fact]
		public void EqualityOperator_WhenSameId_ShouldReturnTrue()
		{
			var a = new SubscriptionHandle(3);
			var b = new SubscriptionHandle(3);
			(a == b).Should().BeTrue();
		}

		[Fact]
		public void Equals_WhenDifferentId_ShouldReturnFalse()
		{
			var a = new SubscriptionHandle(1);
			var b = new SubscriptionHandle(2);
			a.Equals(b).Should().BeFalse();
		}

		[Fact]
		public void Equals_WhenSameId_ShouldReturnTrue()
		{
			var a = new SubscriptionHandle(5);
			var b = new SubscriptionHandle(5);
			a.Equals(b).Should().BeTrue();
		}

		[Fact]
		public void EqualsObject_WhenNotSubscriptionHandle_ShouldReturnFalse()
		{
			var a = new SubscriptionHandle(5);
			a.Equals("not a handle").Should().BeFalse();
		}

		[Fact]
		public void EqualsObject_WhenSameId_ShouldReturnTrue()
		{
			var    a = new SubscriptionHandle(5);
			object b = new SubscriptionHandle(5);
			a.Equals(b).Should().BeTrue();
		}

		[Fact]
		public void GetHashCode_WhenSameId_ShouldBeEqual()
		{
			var a = new SubscriptionHandle(7);
			var b = new SubscriptionHandle(7);
			a.GetHashCode().Should().Be(b.GetHashCode());
		}

		[Fact]
		public void InequalityOperator_WhenDifferentId_ShouldReturnTrue()
		{
			var a = new SubscriptionHandle(1);
			var b = new SubscriptionHandle(2);
			(a != b).Should().BeTrue();
		}

		[Fact]
		public void None_ShouldHaveIdZero()
		{
			SubscriptionHandle.None.Id.Should().Be(0);
		}

		[Fact]
		public void None_ShouldNotBeValid()
		{
			SubscriptionHandle.None.IsValid.Should().BeFalse();
		}

		[Fact]
		public void ToString_ShouldContainId()
		{
			var handle = new SubscriptionHandle(42);
			handle.ToString().Should().Be("Subscription(42)");
		}
	}
}
