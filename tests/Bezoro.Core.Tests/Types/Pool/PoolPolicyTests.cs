using System;

using Bezoro.Core.Abstractions;
using Bezoro.Core.Types;

using FluentAssertions;

using JetBrains.Annotations;

using Xunit;

namespace Bezoro.Core.Tests.Types.Pool;

[TestSubject(typeof(PoolPolicy<>))]
public static class PoolPolicyTests
{
	private sealed class TestPooledObject : IPooledObject
	{
		public int OnRentCount { get; private set; }
		public int OnReturnCount { get; private set; }
		public bool ReturnValue { get; set; } = true;

		public void OnRent() => OnRentCount++;
		public bool OnReturn()
		{
			OnReturnCount++;
			return ReturnValue;
		}
	}

	private sealed class DisposableTestObject : IDisposable
	{
		public bool IsDisposed { get; private set; }
		public void Dispose() => IsDisposed = true;
	}

	public static class UnitTests
	{
		public class Constructors
		{
			[Fact]
			public void WithFactory_ShouldCreatePolicy()
			{
				var policy = new PoolPolicy<object>(() => new object());

				policy.Should().NotBeNull();
			}

			[Fact]
			public void WithNullFactory_ShouldThrow()
			{
				var act = () => new PoolPolicy<object>(null!);

				act.Should().Throw<ArgumentNullException>();
			}

			[Fact]
			public void WithAllDelegates_ShouldCreatePolicy()
			{
				var policy = new PoolPolicy<object>(
					factory: () => new object(),
					reset: _ => true,
					validate: _ => true,
					onDiscard: _ => { });

				policy.Should().NotBeNull();
			}
		}

		public class Create
		{
			[Fact]
			public void ShouldInvokeFactory()
			{
				var invoked = false;
				var policy = new PoolPolicy<object>(() =>
				{
					invoked = true;
					return new object();
				});

				var item = policy.Create();

				invoked.Should().BeTrue();
				item.Should().NotBeNull();
			}

			[Fact]
			public void ShouldReturnNewInstanceEachTime()
			{
				var policy = new PoolPolicy<object>(() => new object());

				var item1 = policy.Create();
				var item2 = policy.Create();

				item1.Should().NotBeSameAs(item2);
			}
		}

		public class Reset
		{
			[Fact]
			public void WithResetDelegate_ShouldInvokeDelegate()
			{
				var resetCalled = false;
				var policy = new PoolPolicy<object>(
					() => new object(),
					reset: _ =>
					{
						resetCalled = true;
						return true;
					});
				var item = new object();

				policy.Reset(item);

				resetCalled.Should().BeTrue();
			}

			[Fact]
			public void WithNoResetDelegate_ShouldReturnTrue()
			{
				var policy = new PoolPolicy<object>(() => new object());
				var item = new object();

				var result = policy.Reset(item);

				result.Should().BeTrue();
			}

			[Fact]
			public void WithIPooledObject_ShouldCallOnReturn()
			{
				var policy = new PoolPolicy<TestPooledObject>(() => new TestPooledObject());
				var item = new TestPooledObject();

				policy.Reset(item);

				item.OnReturnCount.Should().Be(1);
			}

			[Fact]
			public void WithIPooledObject_ShouldReturnOnReturnResult()
			{
				var policy = new PoolPolicy<TestPooledObject>(() => new TestPooledObject());
				var item = new TestPooledObject { ReturnValue = false };

				var result = policy.Reset(item);

				result.Should().BeFalse();
			}

			[Fact]
			public void WithIPooledObject_ShouldPreferOnReturnOverDelegate()
			{
				var delegateCalled = false;
				var policy = new PoolPolicy<TestPooledObject>(
					() => new TestPooledObject(),
					reset: _ =>
					{
						delegateCalled = true;
						return true;
					});
				var item = new TestPooledObject();

				policy.Reset(item);

				delegateCalled.Should().BeFalse();
				item.OnReturnCount.Should().Be(1);
			}
		}

		public class Validate
		{
			[Fact]
			public void WithValidateDelegate_ShouldInvokeDelegate()
			{
				var validateCalled = false;
				var policy = new PoolPolicy<object>(
					() => new object(),
					validate: _ =>
					{
						validateCalled = true;
						return true;
					});
				var item = new object();

				policy.Validate(item);

				validateCalled.Should().BeTrue();
			}

			[Fact]
			public void WithNoValidateDelegate_ShouldReturnTrue()
			{
				var policy = new PoolPolicy<object>(() => new object());
				var item = new object();

				var result = policy.Validate(item);

				result.Should().BeTrue();
			}

			[Fact]
			public void WithValidateDelegate_ShouldReturnDelegateResult()
			{
				var policy = new PoolPolicy<object>(
					() => new object(),
					validate: _ => false);
				var item = new object();

				var result = policy.Validate(item);

				result.Should().BeFalse();
			}
		}

		public class OnDiscard
		{
			[Fact]
			public void WithOnDiscardDelegate_ShouldInvokeDelegate()
			{
				var discardCalled = false;
				var policy = new PoolPolicy<object>(
					() => new object(),
					onDiscard: _ => discardCalled = true);
				var item = new object();

				policy.OnDiscard(item);

				discardCalled.Should().BeTrue();
			}

			[Fact]
			public void WithDisposableItem_ShouldDispose()
			{
				var policy = new PoolPolicy<DisposableTestObject>(() => new DisposableTestObject());
				var item = new DisposableTestObject();

				policy.OnDiscard(item);

				item.IsDisposed.Should().BeTrue();
			}

			[Fact]
			public void WithDiscardDelegateAndDisposable_ShouldCallBoth()
			{
				var discardCalled = false;
				var policy = new PoolPolicy<DisposableTestObject>(
					() => new DisposableTestObject(),
					onDiscard: _ => discardCalled = true);
				var item = new DisposableTestObject();

				policy.OnDiscard(item);

				discardCalled.Should().BeTrue();
				item.IsDisposed.Should().BeTrue();
			}
		}
	}
}
