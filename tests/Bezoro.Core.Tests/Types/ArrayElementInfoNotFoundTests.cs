using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(ArrayElementInfo<>))]
public class ArrayElementInfoNotFoundTests
{
	[Fact]
	public void WhenNotFound_WhenCalled_ShouldCreateNotFoundInstance()
	{
		var info = ArrayElementInfo<string>.NotFound("y", 9);

		info.Index.Should().Be(null);
		info.IsFound.Should().BeFalse();
		info.Element.Should().Be("y");
		info.ArrayLength.Should().Be(9);
	}
}
