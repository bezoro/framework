using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(SwapbackArray<>))]
public class SwapbackArrayAsSpanTests
{
	[Fact]
	public void WhenCalled_WhenCalled_ShouldReturnSpanWithExpectedLength()
	{
		int[] values = [1, 2, 3, 4];
		var   arr    = new SwapbackArray<int>(values);

		var span = arr.AsSpan();

		span.Length.Should().Be((int)arr.Count);
	}

	[Fact]
	public void WhenCalled_WhenCalled_ShouldReturnSpanWithExpectedValues()
	{
		int[] values = [1, 2, 3, 4];
		var   arr    = new SwapbackArray<int>(values);

		var span = arr.AsSpan();

		span.ToArray().Should().Equal(values);
	}
}
