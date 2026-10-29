using System;
using System.Runtime.InteropServices;
using Bezoro.ECS.Internal;
using FluentAssertions;
using Xunit;

namespace Bezoro.ECS.Tests.Internal;

public sealed class ComponentColumnSafetyTests
{
	[Theory]
	[InlineData(-1)]
	[InlineData(2)]
	[InlineData(int.MaxValue)]
	public void CopyElementTo_RejectsInvalidNativeDestinationBeforeWriting(int destinationIndex)
	{
		using var source = ComponentColumn.Create(typeof(int), 2);
		using var destination = ComponentColumn.Create(typeof(int), 2);
		source.SetValue(0, 42);

		Action copy = () => source.CopyElementTo(0, destination, destinationIndex);

		copy.Should().Throw<ArgumentOutOfRangeException>();
		destination.GetValue(0).Should().Be(0);
	}

	[Fact]
	public void Clear_RejectsOverflowingRangeBeforeAccessingNativeMemory()
	{
		using var column = ComponentColumn.Create(typeof(int), 2);
		Action clear = () => column.Clear(int.MaxValue, 2);
		clear.Should().Throw<ArgumentOutOfRangeException>();
	}

	[Theory]
	[InlineData(typeof(int))]
	[InlineData(typeof(string))]
	public void DisposedColumns_RejectReadWriteCopyAndClear(Type componentType)
	{
		using var column = ComponentColumn.Create(componentType, 2);
		using var destination = ComponentColumn.Create(componentType, 2);
		column.Dispose();

		Action read = () => column.GetValue(0);
		Action write = () => column.SetValue(0, componentType == typeof(int) ? 1 : "value");
		Action clear = () => column.Clear(0, 1);
		Action copy = () => column.CopyElementTo(0, destination, 0);

		read.Should().Throw<ObjectDisposedException>();
		write.Should().Throw<ObjectDisposedException>();
		clear.Should().Throw<ObjectDisposedException>();
		copy.Should().Throw<ObjectDisposedException>();
	}

	[Fact]
	public void CopyRangeTo_RejectsDisposedDestinationEvenForEmptyCopy()
	{
		using var source = ComponentColumn.Create(typeof(int), 2);
		using var destination = ComponentColumn.Create(typeof(int), 2);
		destination.Dispose();
		Action copy = () => source.CopyRangeTo(0, destination, 0, 0);
		copy.Should().Throw<ObjectDisposedException>();
	}
	[Fact]
	public void NativeCapacityOverflow_IsRejectedBeforeAllocating()
	{
		Action create = () => ComponentColumn.Create(typeof(long), int.MaxValue);
		create.Should().Throw<OverflowException>();
	}

	[Fact]
	public void ClosedGenericValueType_RoundTripsWithoutMarshalSizeOfFailure()
	{
		using var column = ComponentColumn.Create(typeof(GenericComponent<int>), 2);
		column.SetValue(1, new GenericComponent<int> { Value = 42 });
		column.GetReference<GenericComponent<int>>(1).Value.Should().Be(42);
		column.GetValue(1).Should().Be(new GenericComponent<int> { Value = 42 });
	}

	[Fact]
	public void CustomMarshaledField_UsesTypedStorageWithoutNativeLayoutMismatch()
	{
		using var column = ComponentColumn.Create(typeof(CustomMarshaledComponent), 2);
		column.IsUnmanaged.Should().BeFalse();
		column.GetReference<CustomMarshaledComponent>(1).Value = 123456;
		((CustomMarshaledComponent)column.GetValue(1)).Value.Should().Be(123456);
	}

	private struct GenericComponent<T> { public T Value; }
	private struct CustomMarshaledComponent
	{
		[MarshalAs(UnmanagedType.I1)] public int Value;
	}

}
