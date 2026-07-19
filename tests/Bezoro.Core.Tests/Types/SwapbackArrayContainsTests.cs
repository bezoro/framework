using System;
using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(SwapbackArray<>))]
public class SwapbackArrayContainsTests
{
	[Fact]
	public void WhenArrayIsEmpty_WhenCalled_ShouldReturnFalse()
	{
		var arr = new SwapbackArray<int>();

		arr.Contains(1).Should().BeFalse();
	}

	[Fact]
	public void WhenDefaultItemExists_WhenCalled_ShouldReturnTrue()
	{
		// ReSharper disable once PreferConcreteValueOverDefault
		var arr = new SwapbackArray<int> { 1, 2, 3, default };

		// ReSharper disable once PreferConcreteValueOverDefault
		arr.Contains(default).Should().BeTrue();
	}

	[Fact]
	public void WhenDefaultItemNotFound_WhenCalled_ShouldReturnFalse()
	{
		var arr = new SwapbackArray<int> { 1, 2, 3, 4 };

		// ReSharper disable once PreferConcreteValueOverDefault
		arr.Contains(default).Should().BeFalse();
	}

	[Fact]
	public void WhenItemExists_WhenCalled_ShouldReturnTrue()
	{
		var arr = new SwapbackArray<int> { 1, 2, 3, 4 };

		arr.Contains(2).Should().BeTrue();
	}

	[Fact]
	public void WhenItemNotFound_WhenCalled_ShouldReturnFalse()
	{
		var arr = new SwapbackArray<int> { 1, 2, 3, 4 };

		arr.Contains(5).Should().BeFalse();
	}

	[Fact]
	public void WhenNullItemExists_WhenCalled_ShouldReturnTrue()
	{
		var arr = new SwapbackArray<int?> { 1, 2, 3, null };

		arr.Contains(null).Should().BeTrue();
	}

	[Fact]
	public void WhenNullItemNotFound_WhenCalled_ShouldReturnFalse()
	{
		var arr = new SwapbackArray<int?> { 1, 2, 3, 4 };

		arr.Contains(null).Should().BeFalse();
	}

	[Fact]
	public void WhenEquivalentReferenceItemIsUsedAcrossLookupOperations_ShouldUseDefaultEqualityComparer()
	{
		var stored     = new EqualityByIdentifier("stored");
		var equivalent = new EqualityByIdentifier("STORED");
		var arr        = new SwapbackArray<EqualityByIdentifier> { stored };

		arr.Contains(equivalent).Should().BeTrue();
		arr.IndexOf(equivalent).Should().Be(0);
		arr.TryGetIndex(equivalent, out uint index).Should().BeTrue();
		index.Should().Be(0);

#pragma warning disable CS0618
		arr.TryIndexOf(equivalent, out uint? legacyIndex).Should().BeTrue();
#pragma warning restore CS0618

		legacyIndex.Should().Be(0);
		arr.TryRemove(equivalent).Should().BeTrue();
		arr.Should().BeEmpty();
	}
}

internal sealed class EqualityByIdentifier(string identifier)
{
	public string Identifier { get; } = identifier;

	public override bool Equals(object? obj) =>
		obj is EqualityByIdentifier other &&
		string.Equals(Identifier, other.Identifier, StringComparison.OrdinalIgnoreCase);

	public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Identifier);
}
