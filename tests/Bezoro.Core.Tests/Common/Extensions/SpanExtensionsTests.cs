using System;
using Bezoro.Core.Common.Extensions;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Common.Extensions;

[TestSubject(typeof(SpanExtensions))]
public class SpanExtensionsTests
{
	[Fact]
	public void ThrowIfEmpty_WhenSpanIsEmpty_ThrowsArgumentException()
	{
		Action action = () => Span<int>.Empty.ThrowIfEmpty();

		action.Should().Throw<ArgumentException>().WithMessage("Sequence cannot be empty.*");
	}

	[Fact]
	public void ThrowIfEmpty_WhenSpanHasItems_ReturnsSpan()
	{
		int[] data     = { 1, 2, 3 };
		Span<int> span = data.AsSpan();
		Span<int> result = span.ThrowIfEmpty();

		result.SequenceEqual(span).Should().BeTrue();
	}

	[Fact]
	public void ThrowIfEmpty_WhenReadOnlySpanIsEmpty_ThrowsArgumentException()
	{
		Action action = () => ReadOnlySpan<int>.Empty.ThrowIfEmpty();

		action.Should().Throw<ArgumentException>().WithMessage("Sequence cannot be empty.*");
	}

	[Fact]
	public void ThrowIfEmpty_WhenReadOnlySpanHasItems_ReturnsSpan()
	{
		int[] data               = { 4, 5, 6 };
		ReadOnlySpan<int> span   = data.AsSpan();
		ReadOnlySpan<int> result = span.ThrowIfEmpty();

		result.SequenceEqual(span).Should().BeTrue();
	}
}

