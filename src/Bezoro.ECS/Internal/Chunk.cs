using Bezoro.ECS.Types;

namespace Bezoro.ECS.Internal;

internal sealed class Chunk
{
	public Chunk(Type[] componentTypes, int capacity)
	{
		Entities          = new Entity[capacity];
		Columns           = new ComponentColumn[componentTypes.Length];
		ComponentVersions = new uint[componentTypes.Length];

		for (var i = 0; i < componentTypes.Length; i++)
			Columns[i] = ComponentColumnFactory.Create(componentTypes[i], capacity);
	}

	public ComponentColumn[] Columns { get; }

	public Entity[] Entities { get; }

	public uint[] ComponentVersions { get; }

	public int Count { get; set; }

	public bool IsUnmanagedColumn(int componentIndex)
	{
		ValidateColumnIndex(componentIndex);
		return Columns[componentIndex].IsUnmanaged;
	}

	public object GetValue(int componentIndex, int row)
	{
		ValidateColumnIndex(componentIndex);
		return Columns[componentIndex].GetValue(row);
	}

	public ReadOnlySpan<T> GetReadOnlySpan<T>(int componentIndex, int count) where T : struct
	{
		ValidateColumnIndex(componentIndex);
		return Columns[componentIndex].GetReadOnlySpan<T>(count);
	}

	public Span<T> GetSpan<T>(int componentIndex, int count) where T : struct
	{
		ValidateColumnIndex(componentIndex);
		return Columns[componentIndex].GetSpan<T>(count);
	}

	public ref T GetReference<T>(int componentIndex, int index) where T : struct
	{
		ValidateColumnIndex(componentIndex);
		return ref Columns[componentIndex].GetReference<T>(index);
	}

	public void ClearSlot(int slot)
	{
		for (var i = 0; i < Columns.Length; i++)
			Columns[i].Clear(slot, 1);
	}

	public void CopyValueTo(
		int   sourceColumnIndex,
		int   sourceRow,
		Chunk destination,
		int   destinationColumnIndex,
		int   destinationRow)
	{
		if (destination is null) throw new ArgumentNullException(nameof(destination));

		ValidateColumnIndex(sourceColumnIndex);
		destination.ValidateColumnIndex(destinationColumnIndex);
		Columns[sourceColumnIndex].CopyElementTo(
			sourceRow, destination.Columns[destinationColumnIndex], destinationRow
		);
	}

	public void DisposeColumns()
	{
		for (var i = 0; i < Columns.Length; i++)
			Columns[i].Dispose();
	}

	public void MarkChanged(int componentIndex, uint version)
	{
		if (componentIndex < 0 || componentIndex >= ComponentVersions.Length)
			return;

		ComponentVersions[componentIndex] = version;
	}

	public void SetValue(int componentIndex, int row, object value)
	{
		ValidateColumnIndex(componentIndex);
		Columns[componentIndex].SetValue(row, value);
	}

	private void ValidateColumnIndex(int componentIndex)
	{
		if (componentIndex < 0 || componentIndex >= Columns.Length)
			throw new ArgumentOutOfRangeException(nameof(componentIndex));
	}
}
