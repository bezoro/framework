using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(ArrayElementInfo<>))]
public class ArrayElementInfoDeconstructorTests
{
	[Fact]
	public void WhenCalled_ShouldReturnFields()
	{
		var info = new ArrayElementInfo<int>(3, 7, 11);

		(uint? idx, int elem, uint len) = info;

		idx.Should().Be(3);
		elem.Should().Be(7);
		len.Should().Be(11);
	}
}
