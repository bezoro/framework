using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests;

[TestSubject(typeof(Singleton<>))]
public class SingletonTests
{
	[Fact]
	public async Task Instance_Is_ThreadSafe_Creates_Once()
	{
		// Arrange
		Singleton<DefaultSingleton>.Reset(true);
		DefaultSingleton.ResetCounters();

		// Act
		const int n = 64;
		var tasks = Enumerable.Range(0, n)
							  .Select(_ => Task.Run(() => Singleton<DefaultSingleton>.Instance))
							  .ToArray();

		await Task.WhenAll(tasks);

		// Assert
		DefaultSingleton.ConstructorCount.Should().Be(1);
		var first = await tasks[0];
		tasks.Select(t => t.Result).Should().OnlyContain(x => ReferenceEquals(x, first));
	}

	[Fact]
	public void Abstract_Type_Instance_Throws_TypeInitializationException()
	{
		// Act
		Action act = () => _ = Singleton<AbstractSingleton>.Instance;

		// Assert
		act.Should().Throw<TypeInitializationException>();
	}

	[Fact]
	public void ConfigureFactory_Recreate_Disposes_And_Uses_New_Factory()
	{
		// Arrange
		Singleton<PublicCtorSingleton>.Reset(true);
		PublicCtorSingleton.ResetCounters();

		var first = Singleton<PublicCtorSingleton>.Instance;
		first.Marker = 10;

		// Act
		Singleton<PublicCtorSingleton>.ConfigureFactory(() => new() { Marker = 42 }, true);

		// Assert
		Singleton<PublicCtorSingleton>.Instance.Marker.Should().Be(42);
		PublicCtorSingleton.DisposeCount.Should().Be(
			1,
			"previous default instance should be disposed on recreation");
	}

	[Fact]
	public void DirectConstruction_Is_Disallowed()
	{
		// Arrange
		Singleton<PublicCtorSingleton>.Reset(true);
		PublicCtorSingleton.ResetCounters();

		// Act
		Action act = () => _ = new PublicCtorSingleton();

		// Assert
		act.Should().Throw<InvalidOperationException>()
		   .WithMessage("*Direct construction*not allowed*");
	}

	[Fact]
	public void Factory_Returns_Null_Throws_TypeInitializationException()
	{
		// Arrange
		Singleton<PublicCtorSingleton>.Reset(true);

		// Act
		Action act = () => Singleton<PublicCtorSingleton>.Override(() => null!);

		// Assert
		act.Should().Throw<TypeInitializationException>();
	}

	[Fact]
	public void Flags_Reflect_State_Correctly_With_Override_And_Default()
	{
		// Arrange
		Singleton<PublicCtorSingleton>.Reset(true);

		// Initially
		Singleton<PublicCtorSingleton>.IsValueCreated.Should().BeFalse();
		Singleton<PublicCtorSingleton>.IsOverridden.Should().BeFalse();

		// Only override
		using var scope = Singleton<PublicCtorSingleton>.Override(() => new());
		Singleton<PublicCtorSingleton>.IsOverridden.Should().BeTrue();
		Singleton<PublicCtorSingleton>.IsValueCreated.Should().BeTrue();
	}

	[Fact]
	public void Initialize_Sets_Override_And_Subsequent_Initialize_Fails()
	{
		// Arrange
		Singleton<PublicCtorSingleton>.Reset(true);
		PublicCtorSingleton.ResetCounters();

		// Act
		Singleton<PublicCtorSingleton>.Initialize(() => new() { Marker = 77 });

		// Assert
		Singleton<PublicCtorSingleton>.IsOverridden.Should().BeTrue();
		Singleton<PublicCtorSingleton>.Instance.Marker.Should().Be(77);

		// Subsequent Initialize should fail
		var again = () => Singleton<PublicCtorSingleton>.Initialize(() => new());
		again.Should().Throw<InvalidOperationException>();
	}

	[Fact]
	public void LazyCreation_And_TryGet_And_IsValueCreated_Flow()
	{
		// Arrange
		Singleton<DefaultSingleton>.Reset(true);
		DefaultSingleton.ResetCounters();

		Singleton<DefaultSingleton>.IsValueCreated.Should().BeFalse();
		Singleton<DefaultSingleton>.TryGet(out var none).Should().BeFalse();
		none.Should().BeNull();

		// Act
		var instance = Singleton<DefaultSingleton>.Instance;

		// Assert
		instance.Should().NotBeNull();
		DefaultSingleton.ConstructorCount.Should().Be(1);
		Singleton<DefaultSingleton>.IsValueCreated.Should().BeTrue();
		Singleton<DefaultSingleton>.TryGet(out var got).Should().BeTrue();
		got.Should().BeSameAs(instance);
	}

	[Fact]
	public void Null_Factory_Arguments_Throw()
	{
		// Arrange
		Singleton<PublicCtorSingleton>.Reset(true);

		// Act
		var    cfg  = () => Singleton<PublicCtorSingleton>.ConfigureFactory(null!);
		Action ov   = () => Singleton<PublicCtorSingleton>.Override(null!);
		var    init = () => Singleton<PublicCtorSingleton>.Initialize(null!);

		// Assert
		cfg.Should().Throw<ArgumentNullException>();
		ov.Should().Throw<ArgumentNullException>();
		init.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void Override_Scope_Replaces_And_Restores_Instance()
	{
		// Arrange
		Singleton<PublicCtorSingleton>.Reset(true);
		PublicCtorSingleton.ResetCounters();

		// Prime default instance
		var defaultInstance = Singleton<PublicCtorSingleton>.Instance;
		defaultInstance.Marker.Should().Be(0);

		// Act + Assert
		using (Singleton<PublicCtorSingleton>.Override(() => new() { Marker = 1 }))
		{
			Singleton<PublicCtorSingleton>.IsOverridden.Should().BeTrue();
			Singleton<PublicCtorSingleton>.Instance.Marker.Should().Be(1);

			using (Singleton<PublicCtorSingleton>.Override(() => new() { Marker = 2 }))
			{
				Singleton<PublicCtorSingleton>.Instance.Marker.Should().Be(2);
			}

			Singleton<PublicCtorSingleton>.IsOverridden.Should().BeTrue();
			Singleton<PublicCtorSingleton>.Instance.Marker.Should().Be(1);
		}

		Singleton<PublicCtorSingleton>.IsOverridden.Should().BeFalse();
		Singleton<PublicCtorSingleton>.Instance.Should().BeSameAs(defaultInstance);
	}

	[Fact]
	public void ReflectionConstruction_Is_Disallowed()
	{
		// Arrange
		Singleton<DefaultSingleton>.Reset(true);
		DefaultSingleton.ResetCounters();

		// Act
		Action act = () => Activator.CreateInstance(typeof(DefaultSingleton), true);

		// Assert
		act.Should().Throw<TargetInvocationException>()
		   .WithInnerException<InvalidOperationException>()
		   .WithMessage("*Direct construction*not allowed*");

		DefaultSingleton.ConstructorCount.Should().Be(0);
	}

	[Fact]
	public void Reset_Swallows_Dispose_Errors()
	{
		// Arrange
		Singleton<ThrowOnDisposeSingleton>.Reset(true);
		_ = Singleton<ThrowOnDisposeSingleton>.Instance;

		// Act
		var act = () => Singleton<ThrowOnDisposeSingleton>.Reset(true);

		// Assert
		act.Should().NotThrow("Dispose exceptions should be swallowed during reset");
	}
}

internal abstract class AbstractSingleton : Singleton<AbstractSingleton> { }

internal sealed class DefaultSingleton : Singleton<DefaultSingleton>, IDisposable
{
	public static int ConstructorCount;
	public static int DisposeCount;

	private DefaultSingleton()
	{
		Interlocked.Increment(ref ConstructorCount);
	}

	public static void ResetCounters()
	{
		ConstructorCount = 0;
		DisposeCount     = 0;
	}

	public void Dispose()
	{
		Interlocked.Increment(ref DisposeCount);
	}
}

internal sealed class PublicCtorSingleton : Singleton<PublicCtorSingleton>, IDisposable
{
	public static int DisposeCount;

	// public ctor allows custom factory to "new" it; guarded by base to prevent direct construction.

	public int Marker { get; set; }

	public static void ResetCounters()
	{
		DisposeCount = 0;
	}

	public void Dispose()
	{
		Interlocked.Increment(ref DisposeCount);
	}
}

internal sealed class ThrowOnDisposeSingleton : Singleton<ThrowOnDisposeSingleton>, IDisposable
{
	public void Dispose()
	{
		throw new InvalidOperationException("Dispose failed");
	}
}
