using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(ArrayElementInfo<>))]
public class ArrayElementInfoIsFoundTests
{
	[Theory]
	[InlineData(null, false)]
	[InlineData(0u,   true)]
	[InlineData(5u,   true)]
	public void WhenIsFound_WhenCalled_ShouldReflectIndex(uint? index, bool expected)
	{
		var info = new ArrayElementInfo<object>(index, new(), index.HasValue ? index.Value + 1 : 0);

		info.IsFound.Should().Be(expected);
	}
}
