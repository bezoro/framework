using System;
using System.Runtime.CompilerServices;
using Bezoro.ECS.Internal;
using FluentAssertions;
using Xunit;

namespace Bezoro.ECS.Tests.Internal;

public sealed class ComponentTypeTraitsTests
{
	[Fact]
	public void ContainsReferences_MatchesRuntimeForNestedAndGenericValueTypes()
	{
		AssertMatchesRuntime<int>();
		AssertMatchesRuntime<bool>();
		AssertMatchesRuntime<char>();
		AssertMatchesRuntime<decimal>();
		AssertMatchesRuntime<DateTime>();
		AssertMatchesRuntime<Guid>();
		AssertMatchesRuntime<DayOfWeek>();
		AssertMatchesRuntime<(int, double)>();
		AssertMatchesRuntime<(int, string)>();
		AssertMatchesRuntime<(int, string)?>();
		AssertMatchesRuntime<(int, double)?>();
		AssertMatchesRuntime<ManagedComponent>();
	}

	private static void AssertMatchesRuntime<T>() =>
		ComponentTypeTraits.ContainsReferences(typeof(T)).Should().Be(RuntimeHelpers.IsReferenceOrContainsReferences<T>());

	private readonly record struct ManagedComponent((int, string) Value);
}
