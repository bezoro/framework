using System.Runtime.InteropServices;

namespace Bezoro.ECS.Internal;

internal unsafe sealed class UnmanagedComponentColumn : ComponentColumn
{
	private readonly IntPtr _alignedPointer;
	private readonly IntPtr _allocatedPointer;
	private readonly int _alignmentBytes;
	private readonly int _elementSize;
	private readonly bool _usesNativeAlignedAlloc;
	private bool _disposed;

	public UnmanagedComponentColumn(Type componentType, int capacity, int alignmentBytes) : base(componentType, capacity)
	{
		if (!ComponentTypeTraits.IsUnmanaged(componentType))
			throw new ArgumentException("Component type must be unmanaged.", nameof(componentType));
		if (alignmentBytes <= 0)
			throw new ArgumentOutOfRangeException(nameof(alignmentBytes));
		if ((alignmentBytes & (alignmentBytes - 1)) != 0)
			throw new ArgumentOutOfRangeException(nameof(alignmentBytes), "Alignment must be a power of two.");

		_alignmentBytes = alignmentBytes;
		_elementSize = Marshal.SizeOf(componentType);
		if (_elementSize <= 0)
			throw new InvalidOperationException("Component size must be positive.");

		var byteLength = checked((nuint)((ulong)_elementSize * (ulong)capacity));
#if NET9_0
		void* native = NativeMemory.AlignedAlloc(byteLength, (nuint)_alignmentBytes);
		if (native is null)
			throw new OutOfMemoryException("Failed to allocate unmanaged component buffer.");

		_allocatedPointer = (IntPtr)native;
		_alignedPointer = _allocatedPointer;
		_usesNativeAlignedAlloc = true;
#else
		var allocationLength = checked((nint)byteLength + _alignmentBytes - 1);
		_allocatedPointer = Marshal.AllocHGlobal(allocationLength);
		if (_allocatedPointer == IntPtr.Zero)
			throw new OutOfMemoryException("Failed to allocate unmanaged component buffer.");

		long baseAddress = _allocatedPointer.ToInt64();
		long alignedAddress = (baseAddress + (_alignmentBytes - 1)) & ~((long)_alignmentBytes - 1);
		_alignedPointer = new IntPtr(alignedAddress);
		_usesNativeAlignedAlloc = false;
#endif

		new Span<byte>(_alignedPointer.ToPointer(), (int)byteLength).Clear();
	}

	public override bool IsUnmanaged => true;

	internal bool UsesNativeAlignedAlloc => _usesNativeAlignedAlloc;

	internal nuint AlignedAddress => (nuint)_alignedPointer.ToPointer();

	internal int AlignmentBytes => _alignmentBytes;

	public override void Clear(int index, int length)
	{
		EnsureNotDisposed();
		ValidateRange(index, length);
		var byteOffset = checked(index * _elementSize);
		var byteLength = checked(length * _elementSize);
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
			var source = GetPointer(sourceIndex);
			var target = unmanaged.GetPointer(destinationIndex);
			Buffer.MemoryCopy(source, target, _elementSize, _elementSize);
			return;
		}

		destination.SetValue(destinationIndex, GetValue(sourceIndex));
	}

	public override object GetValue(int index)
	{
		EnsureNotDisposed();
		ValidateIndex(index);
		return Marshal.PtrToStructure(new IntPtr(GetPointer(index)), ComponentType)!;
	}

	public override void SetValue(int index, object value)
	{
		EnsureNotDisposed();
		ValidateIndex(index);
		if (value is null) throw new ArgumentNullException(nameof(value));
		if (value.GetType() != ComponentType)
			throw new ArgumentException($"Expected value of type {ComponentType.FullName}.", nameof(value));

		Marshal.StructureToPtr(value, new IntPtr(GetPointer(index)), fDeleteOld: false);
	}

	public override ref T GetReference<T>(int index)
	{
		EnsureNotDisposed();
		ValidateType<T>();
		ValidateIndex(index);
		return ref MemoryMarshal.GetReference(GetSpan<T>(Capacity).Slice(index, 1));
	}

	public override Span<T> GetSpan<T>(int length)
	{
		EnsureNotDisposed();
		ValidateType<T>();
		ValidateRange(0, length);
		var bytes = new Span<byte>(_alignedPointer.ToPointer(), checked(Capacity * _elementSize));
		return MemoryMarshal.Cast<byte, T>(bytes).Slice(0, length);
	}

	public override ReadOnlySpan<T> GetReadOnlySpan<T>(int length)
	{
		EnsureNotDisposed();
		ValidateType<T>();
		ValidateRange(0, length);
		var bytes = new ReadOnlySpan<byte>(_alignedPointer.ToPointer(), checked(Capacity * _elementSize));
		return MemoryMarshal.Cast<byte, T>(bytes).Slice(0, length);
	}

	public override void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	~UnmanagedComponentColumn()
	{
		Dispose(disposing: false);
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

	private byte* GetPointer(int index)
	{
		ValidateIndex(index);
		return (byte*)_alignedPointer.ToPointer() + (index * _elementSize);
	}

	private void EnsureNotDisposed()
	{
		if (_disposed)
			throw new ObjectDisposedException(nameof(UnmanagedComponentColumn));
	}
}
