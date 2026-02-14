using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Bezoro.ECS.Internal;

internal static class ComponentSizeEstimator
{
	private static readonly ConditionalWeakTable<Type, CacheEntry> SizeCache = new();
	private static readonly object                                 Sync      = new();

	public static int GetSizeInBytes(Type type)
	{
		if (type is null) throw new ArgumentNullException(nameof(type));

		lock (Sync)
		{
			if (SizeCache.TryGetValue(type, out var cached))
				return cached.Value;

			int computed = ComputeSizeInBytes(type);
			SizeCache.Add(type, new(computed));
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

		var total = 0;
		var fields = type.GetFields(
			BindingFlags.Instance |
			BindingFlags.Public |
			BindingFlags.NonPublic
		);

		for (var i = 0; i < fields.Length; i++)
		{
			var fieldType = fields[i].FieldType;
			total += fieldType.IsValueType
						 ? GetSizeInBytes(fieldType)
						 : IntPtr.Size;
		}

		return Math.Max(1, total);
	}

	private sealed class CacheEntry(int value)
	{
		public int Value { get; } = value;
	}
}
