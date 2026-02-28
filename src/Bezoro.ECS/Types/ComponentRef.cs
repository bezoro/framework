using System;
using System.Runtime.InteropServices;

namespace Bezoro.ECS.Types;

/// <summary>
///     Wraps a mutable component reference returned from a non-throwing ECS API.
/// </summary>
/// <typeparam name="T">Component type.</typeparam>
public readonly ref struct ComponentRef<T> where T : struct
{
	private readonly Span<T> _value;

	internal ComponentRef(ref T value)
	{
		_value = MemoryMarshal.CreateSpan(ref value, 1);
	}

	/// <summary>
	///     Gets the mutable component reference.
	/// </summary>
	public ref T Value => ref MemoryMarshal.GetReference(_value);
}
