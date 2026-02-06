using System.Runtime.InteropServices;

namespace Bezoro.ECS.Internal;

internal static class ComponentSizeEstimator
{
	private static readonly object Sync = new();
	private static readonly Dictionary<Type, int> SizeCache = new();

	public static int GetSizeInBytes(Type type)
	{
		if (type is null) throw new ArgumentNullException(nameof(type));

		lock (Sync)
		{
			if (SizeCache.TryGetValue(type, out int cached))
				return cached;

			int computed = ComputeSizeInBytes(type);
			SizeCache[type] = computed;
			return computed;
		}
	}

	private static int ComputeSizeInBytes(Type type)
	{
		if (type.IsPointer)
			return IntPtr.Size;

		if (!type.IsValueType)
			return IntPtr.Size;

		try
		{
			int marshaledSize = Marshal.SizeOf(type);
			if (marshaledSize > 0)
				return marshaledSize;
		}
		catch
		{
			// Fallback below for auto-layout or unsupported marshalling scenarios.
		}

		if (type.IsEnum)
			return Marshal.SizeOf(Enum.GetUnderlyingType(type));

		int total = 0;
		var fields = type.GetFields(
			System.Reflection.BindingFlags.Instance |
			System.Reflection.BindingFlags.Public |
			System.Reflection.BindingFlags.NonPublic);

		for (var i = 0; i < fields.Length; i++)
		{
			Type fieldType = fields[i].FieldType;
			total += fieldType.IsValueType
				? GetSizeInBytes(fieldType)
				: IntPtr.Size;
		}

		return Math.Max(1, total);
	}
}
