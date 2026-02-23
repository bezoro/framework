using System;
using Bezoro.Core.Extensions;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;
using CoreMemoryExtensions = Bezoro.Core.Extensions.MemoryExtensions;

namespace Bezoro.Core.Tests.Extensions;

[TestSubject(typeof(CoreMemoryExtensions))]
public class MemoryExtensionsTests
{
	[Fact]
	public void ThrowIfEmptyWhenMemoryHasItems_WhenCalled_ShouldReturnMemory()
	{
		Memory<int> memory = new[] { 1, 2, 3 };

		var result = memory.ThrowIfEmpty();

		result.Span.SequenceEqual(memory.Span).Should().BeTrue();
	}

	[Fact]
	public void ThrowIfEmptyWhenMemoryIsEmpty_WhenCalled_ShouldThrowArgumentException()
	{
		var memory = Memory<int>.Empty;

		Action action = () => memory.ThrowIfEmpty();

		action.Should().Throw<ArgumentException>().WithMessage("Sequence cannot be empty.*");
	}

	[Fact]
	public void ThrowIfEmptyWhenReadOnlyMemoryHasItems_WhenCalled_ShouldReturnMemory()
	{
		ReadOnlyMemory<int> memory = new[] { 4, 5, 6 };

		var result = memory.ThrowIfEmpty();

		result.Span.SequenceEqual(memory.Span).Should().BeTrue();
	}

	[Fact]
	public void ThrowIfEmptyWhenReadOnlyMemoryIsEmpty_WhenCalled_ShouldThrowArgumentException()
	{
		var memory = ReadOnlyMemory<int>.Empty;

		Action action = () => memory.ThrowIfEmpty();

		action.Should().Throw<ArgumentException>().WithMessage("Sequence cannot be empty.*");
	}
}
