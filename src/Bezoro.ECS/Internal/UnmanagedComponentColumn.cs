using System.Runtime.InteropServices;

namespace Bezoro.ECS.Internal;

internal sealed unsafe class UnmanagedComponentColumn : ComponentColumn
{
	private readonly int    _elementSize;
	private readonly IntPtr _alignedPointer;
	private readonly IntPtr _allocatedPointer;
	private          bool   _disposed;

	public UnmanagedComponentColumn(Type componentType, int capacity, int alignmentBytes) : base(
		componentType, capacity
	)
	{
		if (!ComponentTypeTraits.IsUnmanaged(componentType))
			throw new ArgumentException("Component type must be unmanaged.", nameof(componentType));

		if (alignmentBytes <= 0)
			throw new ArgumentOutOfRangeException(nameof(alignmentBytes));

		if ((alignmentBytes & alignmentBytes - 1) != 0)
			throw new ArgumentOutOfRangeException(nameof(alignmentBytes), "Alignment must be a power of two.");

		AlignmentBytes = alignmentBytes;
		_elementSize   = Marshal.SizeOf(componentType);
		if (_elementSize <= 0)
			throw new InvalidOperationException("Component size must be positive.");

		var byteLength = checked((nuint)((ulong)_elementSize * (ulong)capacity));
#if NET9_0
		void* native = NativeMemory.AlignedAlloc(byteLength, (nuint)_alignmentBytes);
		if (native is null)
			throw new OutOfMemoryException("Failed to allocate unmanaged component buffer.");

		_allocatedPointer       = (IntPtr)native;
		_alignedPointer         = _allocatedPointer;
		_usesNativeAlignedAlloc = true;
#else
		var allocationLength = checked((nint)byteLength + AlignmentBytes - 1);
		_allocatedPointer = Marshal.AllocHGlobal(allocationLength);
		if (_allocatedPointer == IntPtr.Zero)
			throw new OutOfMemoryException("Failed to allocate unmanaged component buffer.");

		var  baseAddress    = _allocatedPointer.ToInt64();
		long alignedAddress = baseAddress + (AlignmentBytes - 1) & ~((long)AlignmentBytes - 1);
		_alignedPointer        = new(alignedAddress);
		UsesNativeAlignedAlloc = false;
#endif

		new Span<byte>(_alignedPointer.ToPointer(), (int)byteLength).Clear();
	}

	~UnmanagedComponentColumn()
	{
		Dispose(false);
	}

	public override bool IsUnmanaged => true;

	internal bool UsesNativeAlignedAlloc { get; }

	internal int AlignmentBytes { get; }

	internal nuint AlignedAddress => (nuint)_alignedPointer.ToPointer();

	public override object GetValue(int index)
	{
		EnsureNotDisposed();
		ValidateIndex(index);
		return Marshal.PtrToStructure(new(GetPointer(index)), ComponentType)!;
	}

	public override ReadOnlySpan<T> GetReadOnlySpan<T>(int length)
	{
		EnsureNotDisposed();
		ValidateType<T>();
		ValidateRange(0, length);
		var bytes = new ReadOnlySpan<byte>(_alignedPointer.ToPointer(), checked(Capacity * _elementSize));
		return MemoryMarshal.Cast<byte, T>(bytes).Slice(0, length);
	}

	public override Span<T> GetSpan<T>(int length)
	{
		EnsureNotDisposed();
		ValidateType<T>();
		ValidateRange(0, length);
		var bytes = new Span<byte>(_alignedPointer.ToPointer(), checked(Capacity * _elementSize));
		return MemoryMarshal.Cast<byte, T>(bytes).Slice(0, length);
	}

	public override ref T GetReference<T>(int index)
	{
		EnsureNotDisposed();
		ValidateType<T>();
		ValidateIndex(index);
		return ref MemoryMarshal.GetReference(GetSpan<T>(Capacity).Slice(index, 1));
	}

	public override void Clear(int index, int length)
	{
		EnsureNotDisposed();
		ValidateRange(index, length);
		int byteOffset = checked(index * _elementSize);
		int byteLength = checked(length * _elementSize);
		new Span<byte>((byte*)_alignedPointer.ToPointer() + byteOffset, byteLength).Clear();
	}

	public override void CopyElementTo(int sourceIndex, ComponentColumn destination, int destinationIndex)
	{
		EnsureNotDisposed();
		ValidateIndex(sourceIndex);
		if (destination is null) throw new ArgumentNullException(nameof(destination));

		if (destination is UnmanagedComponentColumn unmanaged && unmanaged.ComponentType == ComponentType)
		{
			unmanaged.EnsureNotDisposed();
			byte* source = GetPointer(sourceIndex);
			byte* target = unmanaged.GetPointer(destinationIndex);
			Buffer.MemoryCopy(source, target, _elementSize, _elementSize);
			return;
		}

		destination.SetValue(destinationIndex, GetValue(sourceIndex));
	}

	public override void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	public override void SetValue(int index, object value)
	{
		EnsureNotDisposed();
		ValidateIndex(index);
		if (value is null) throw new ArgumentNullException(nameof(value));
		if (value.GetType() != ComponentType)
			throw new ArgumentException($"Expected value of type {ComponentType.FullName}.", nameof(value));

		Marshal.StructureToPtr(value, new(GetPointer(index)), false);
	}

	private byte* GetPointer(int index)
	{
		ValidateIndex(index);
		return (byte*)_alignedPointer.ToPointer() + index * _elementSize;
	}

	private void Dispose(bool disposing)
	{
		if (_disposed) return;

		_disposed = true;
		if (_allocatedPointer == IntPtr.Zero)
			return;

#if NET9_0
		if (_usesNativeAlignedAlloc)
		{
			NativeMemory.AlignedFree(_allocatedPointer.ToPointer());
			return;
		}
#endif
		Marshal.FreeHGlobal(_allocatedPointer);
	}

	private void EnsureNotDisposed()
	{
		if (_disposed)
			throw new ObjectDisposedException(nameof(UnmanagedComponentColumn));
	}
}
