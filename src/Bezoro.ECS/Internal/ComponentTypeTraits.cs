using System.Reflection;

namespace Bezoro.ECS.Internal;

internal static class ComponentTypeTraits
{
	private static readonly object Sync = new();
	private static readonly Dictionary<Type, bool> IsUnmanagedCache = new();

	public static bool IsUnmanaged(Type type)
	{
		if (type is null) throw new ArgumentNullException(nameof(type));

		lock (Sync)
		{
			if (IsUnmanagedCache.TryGetValue(type, out bool cached))
				return cached;

			bool value = ComputeIsUnmanaged(type);
			IsUnmanagedCache[type] = value;
			return value;
		}
	}

	private static bool ComputeIsUnmanaged(Type type)
	{
		if (type.IsPointer)
			return true;

		if (!type.IsValueType)
			return false;

		if (type.IsPrimitive || type.IsEnum)
			return true;

		var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		for (var i = 0; i < fields.Length; i++)
		{
			if (!IsUnmanaged(fields[i].FieldType))
				return false;
		}

		return true;
	}
}
