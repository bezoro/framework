using System;
using Bezoro.Core.Extensions;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;
using CoreMemoryExtensions = Bezoro.Core.Extensions.MemoryExtensions;

namespace Bezoro.Core.Tests.Common.Extensions;

[TestSubject(typeof(CoreMemoryExtensions))]
public class MemoryExtensionsTests
{
	[Fact]
	public void ThrowIfEmpty_WhenMemoryIsEmpty_ThrowsArgumentException()
	{
		Memory<int> memory = Memory<int>.Empty;

		Action action = () => memory.ThrowIfEmpty();

		action.Should().Throw<ArgumentException>().WithMessage("Sequence cannot be empty.*");
	}

	[Fact]
	public void ThrowIfEmpty_WhenMemoryHasItems_ReturnsMemory()
	{
		Memory<int> memory = new[] { 1, 2, 3 };

		Memory<int> result = memory.ThrowIfEmpty();

		result.Span.SequenceEqual(memory.Span).Should().BeTrue();
	}

	[Fact]
	public void ThrowIfEmpty_WhenReadOnlyMemoryIsEmpty_ThrowsArgumentException()
	{
		ReadOnlyMemory<int> memory = ReadOnlyMemory<int>.Empty;

		Action action = () => memory.ThrowIfEmpty();

		action.Should().Throw<ArgumentException>().WithMessage("Sequence cannot be empty.*");
	}

	[Fact]
	public void ThrowIfEmpty_WhenReadOnlyMemoryHasItems_ReturnsMemory()
	{
		ReadOnlyMemory<int> memory = new[] { 4, 5, 6 };

		ReadOnlyMemory<int> result = memory.ThrowIfEmpty();

		result.Span.SequenceEqual(memory.Span).Should().BeTrue();
	}
}



