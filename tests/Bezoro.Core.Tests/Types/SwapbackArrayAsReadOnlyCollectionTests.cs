using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(SwapbackArray<>))]
public class SwapbackArrayAsReadOnlyCollectionTests
{
	[Fact]
	public void WhenCalled_ShouldReturnReadOnlyCollectionWithSameItems()
	{
		var arr = new SwapbackArray<int> { 1, 2, 3 };

		var rc = arr.AsReadOnlyCollection();

		rc.Should().Equal(arr);
	}
}
