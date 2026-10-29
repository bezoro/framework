using System.Reflection;
using System.Runtime.CompilerServices;

namespace Bezoro.ECS.Internal;

internal static class ComponentTypeTraits
{
	private static readonly ConditionalWeakTable<Type, CacheEntry> IsUnmanagedCache = new();
	private static readonly ConditionalWeakTable<Type, CacheEntry> ContainsReferencesCache = new();
	private static readonly object                                 Sync             = new();

	public static bool IsUnmanaged(Type type)
	{
		if (type is null) throw new ArgumentNullException(nameof(type));

		lock (Sync)
		{
			if (IsUnmanagedCache.TryGetValue(type, out var cached))
				return cached.Value;

			bool value = ComputeIsUnmanaged(type);
			IsUnmanagedCache.Add(type, new(value));
			return value;
		}
	}

	public static bool ContainsReferences(Type type)
	{
		if (type is null) throw new ArgumentNullException(nameof(type));
		lock (Sync)
		{
			if (ContainsReferencesCache.TryGetValue(type, out var cached))
				return cached.Value;

			bool value = ComputeContainsReferences(type);
			ContainsReferencesCache.Add(type, new(value));
			return value;
		}
	}

	private static bool ComputeContainsReferences(Type type)
	{
		if (type.IsPointer || type.IsPrimitive || type.IsEnum)
			return false;
		if (!type.IsValueType)
			return true;

		foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
		{
			if (ContainsReferences(field.FieldType))
				return true;
		}

		return false;
	}

	private static bool ComputeIsUnmanaged(Type type)
	{
		if (type.IsPointer)
			return true;

		if (!type.IsValueType)
			return false;

		// These types cannot use the Marshal-based native column representation.
		// Closed generic value types and enums are valid ECS components, but
		// Marshal.SizeOf(Type) does not support them on all supported runtimes.
		if (type.IsEnum || type.IsGenericType || type.IsAutoLayout)
			return false;

		if (type == typeof(bool) || type == typeof(char))
			return false;

		if (type.IsPrimitive)
			return true;

		var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		for (var i = 0; i < fields.Length; i++)
		{
			if (fields[i].IsDefined(typeof(System.Runtime.InteropServices.MarshalAsAttribute), false))
				return false;
			if (!IsUnmanaged(fields[i].FieldType))
				return false;
		}

		return true;
	}

	private sealed class CacheEntry(bool value)
	{
		public bool Value { get; } = value;
	}
}
