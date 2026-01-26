using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(UIntVector2))]
public class UIntVector2ToStringTests
{
	[Fact]
	public void WhenCalled_ShouldFormatAsExpected()
	{
		var u = new UIntVector2(3u, 4u);

		u.ToString().Should().Be("(3, 4)");
	}
}
