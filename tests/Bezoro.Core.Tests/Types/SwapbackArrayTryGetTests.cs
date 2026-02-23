using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(SwapbackArray<>))]
public class SwapbackArrayTryGetTests
{
	[Fact]
	public void WhenIndexEqualsCount_WhenCalled_ShouldReturnFalse()
	{
		var arr = new SwapbackArray<int> { 1, 2, 3 };

		arr.TryGet(arr.Count, out int _).Should().BeFalse();
	}

	[Fact]
	public void WhenIndexIsOutOfBounds_WhenCalled_ShouldReturnFalse()
	{
		var arr = new SwapbackArray<int?> { 1, 2 };

		arr.TryGet(2, out int? _).Should().BeFalse();
	}

	[Fact]
	public void WhenValidIndex_WhenCalled_ShouldReturnItem()
	{
		var arr = new SwapbackArray<int?> { 1, 2 };

		arr.TryGet(0, out int? value).Should().BeTrue();

		value.Should().Be(1);
	}
}
