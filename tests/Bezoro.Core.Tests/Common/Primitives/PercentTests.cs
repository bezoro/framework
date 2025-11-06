using System;
using Bezoro.Core.Common.Primitives;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Common.Primitives;

[TestSubject(typeof(Percent))]
public static class PercentTests
{
	public static class UnitTests
	{
		public class Constructors
		{
			[Fact]
			public void WhenValidValue_ShouldCreateObject()
			{
				var p = new Percent(10);

				p.Value.Should().Be(10);
			}

			[Fact]
			public void WhenValueOver100_ShouldThrow()
			{
				var act = () => new Percent(101);

				act.Should().Throw<ArgumentOutOfRangeException>();
			}
		}

		public class ToRatio
		{
			[Fact]
			public void WhenCalled_ShouldReturnRatio()
			{
				var p = new Percent(10);

				double r = p.ToRatio();

				r.Should().Be(0.1f);
			}
		}

		public class ToStringTests
		{
			[Fact]
			public void WhenCalled_ShouldReturnPercentString()
			{
				var p = new Percent(10);

				p.ToString().Should().Be("10%");
			}
		}
	}
}
