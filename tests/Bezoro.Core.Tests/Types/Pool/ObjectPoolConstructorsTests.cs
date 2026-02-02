using System;
using Bezoro.Core.Abstractions;
using Bezoro.Core.Types.Pool;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types.Pool;

[TestSubject(typeof(ObjectPool<>))]
public class ObjectPoolConstructorsTests
{
	[Fact]
	public void WithFactory_ShouldCreatePool()
	{
		var pool = new ObjectPool<object>(() => new());

		pool.AvailableCount.Should().Be(0);
		pool.TotalCount.Should().Be(0);
		pool.MaxCapacity.Should().Be(-1);
	}

	[Fact]
	public void WithFactoryAndOptions_ShouldRespectOptions()
	{
		var pool = new ObjectPool<object>(
			() => new(),
			new() { MaxCapacity = 5, InitialCapacity = 3 }
		);

		pool.AvailableCount.Should().Be(3);
		pool.TotalCount.Should().Be(3);
		pool.MaxCapacity.Should().Be(5);
	}

	[Fact]
	public void WithInitialCapacity_ShouldPrewarm()
	{
		var createCount = 0;
		var pool = new ObjectPool<object>(
			() =>
			{
				createCount++;
				return new();
			},
			new() { InitialCapacity = 5 }
		);

		createCount.Should().Be(5);
		pool.AvailableCount.Should().Be(5);
	}

	[Fact]
	public void WithNullFactory_ShouldThrow()
	{
		var act = () => new ObjectPool<object>(null!);

		act.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void WithNullPolicy_ShouldThrow()
	{
		var act = () => new ObjectPool<object>((IPoolPolicy<object>)null!);

		act.Should().Throw<ArgumentNullException>();
	}
}
