using System.Reflection;
using System.Runtime.CompilerServices;

namespace Bezoro.ECS.Internal;

internal static class ComponentTypeTraits
{
	private static readonly ConditionalWeakTable<Type, CacheEntry> IsUnmanagedCache = new();
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

	private static bool ComputeIsUnmanaged(Type type)
	{
		if (type.IsPointer)
			return true;

		if (!type.IsValueType)
			return false;

		if (type.IsEnum)
			return IsUnmanaged(Enum.GetUnderlyingType(type));

		if (type == typeof(bool) || type == typeof(char))
			return false;

		if (type.IsPrimitive)
			return true;

		var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		for (var i = 0; i < fields.Length; i++)
		{
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
