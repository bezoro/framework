using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(ArrayElementInfo<>))]
public class ArrayElementInfoRuntimeElementTypeTests
{
	[Fact]
	public void WhenElementIsNull_WhenCalled_ShouldFallbackToArrayType()
	{
		var info = new ArrayElementInfo<string>(5, null, 7);

		info.RuntimeElementType.Should().Be<string>();
	}

	[Fact]
	public void WhenNullableHasValue_WhenCalled_ShouldBeUnderlyingType()
	{
		var info = new ArrayElementInfo<int?>(1, 5, 3);

		info.RuntimeElementType.Should().Be<int>(); // boxing Nullable<T> with value yields underlying T
		ArrayElementInfo<int?>.ElementType.Should().Be<int?>();
	}

	[Fact]
	public void WhenNullableIsNull_WhenCalled_ShouldBeArrayType()
	{
		var info = new ArrayElementInfo<int?>(null, null, 3);

		info.RuntimeElementType.Should().Be<int?>();
	}
}
