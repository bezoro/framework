using Bezoro.Core.Types.Pool;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types.Pool;

[TestSubject(typeof(ObjectPool<>))]
public class ObjectPoolTryRentTests
{
	[Fact]
	public void WhenDisposed_ShouldReturnFalse()
	{
		var pool = new ObjectPool<object>(() => new());
		pool.Dispose();

		bool result = pool.TryRent(out object? item);

		result.Should().BeFalse();
		item.Should().BeNull();
	}

	[Fact]
	public void WhenPoolEmpty_ShouldReturnFalse()
	{
		var pool = new ObjectPool<object>(() => new());

		bool result = pool.TryRent(out object? item);

		result.Should().BeFalse();
		item.Should().BeNull();
	}

	[Fact]
	public void WhenPoolHasItems_ShouldReturnTrue()
	{
		var    pool     = new ObjectPool<object>(() => new());
		object original = pool.Rent();
		pool.Return(original);

		bool result = pool.TryRent(out object? item);

		result.Should().BeTrue();
		item.Should().BeSameAs(original);
	}
}
