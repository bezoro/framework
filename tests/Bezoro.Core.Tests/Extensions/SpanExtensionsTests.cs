using System;
using Bezoro.Core.Extensions;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Extensions;

[TestSubject(typeof(SpanExtensions))]
public class SpanExtensionsTests
{
	[Fact]
	public void ThrowIfEmptyWhenReadOnlySpanHasItems_WhenCalled_ShouldReturnSpan()
	{
		int[]             data   = { 4, 5, 6 };
		ReadOnlySpan<int> span   = data.AsSpan();
		var               result = span.ThrowIfEmpty();

		result.SequenceEqual(span).Should().BeTrue();
	}

	[Fact]
	public void ThrowIfEmptyWhenReadOnlySpanIsEmpty_WhenCalled_ShouldThrowArgumentException()
	{
		Action action = () => ReadOnlySpan<int>.Empty.ThrowIfEmpty();

		action.Should().Throw<ArgumentException>().WithMessage("Sequence cannot be empty.*");
	}

	[Fact]
	public void ThrowIfEmptyWhenSpanHasItems_WhenCalled_ShouldReturnSpan()
	{
		int[] data   = { 1, 2, 3 };
		var   span   = data.AsSpan();
		var   result = span.ThrowIfEmpty();

		result.SequenceEqual(span).Should().BeTrue();
	}

	[Fact]
	public void ThrowIfEmptyWhenSpanIsEmpty_WhenCalled_ShouldThrowArgumentException()
	{
		Action action = () => Span<int>.Empty.ThrowIfEmpty();

		action.Should().Throw<ArgumentException>().WithMessage("Sequence cannot be empty.*");
	}
}
