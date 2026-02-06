using System.Reflection;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Internal;

internal static class GeneratedSystemMetadataResolver
{
	private const string MetadataTypeName = "Bezoro.ECS.Generated.GeneratedSystemMetadata";
	private static readonly object Sync = new();
	private static readonly Dictionary<Assembly, IReadOnlyDictionary<Type, SystemMetadata>?> Cache = new();

	public static bool TryGet(Type systemType, out SystemMetadata metadata)
	{
		if (systemType is null) throw new ArgumentNullException(nameof(systemType));

		var map = GetOrCreateMap(systemType.Assembly);
		if (map is not null && map.TryGetValue(systemType, out metadata))
			return true;

		metadata = default;
		return false;
	}

	private static IReadOnlyDictionary<Type, SystemMetadata>? GetOrCreateMap(Assembly assembly)
	{
		lock (Sync)
		{
			if (Cache.TryGetValue(assembly, out var existing))
				return existing;

			var created = CreateMap(assembly);
			Cache[assembly] = created;
			return created;
		}
	}

	private static IReadOnlyDictionary<Type, SystemMetadata>? CreateMap(Assembly assembly)
	{
		var metadataType = assembly.GetType(MetadataTypeName, throwOnError: false, ignoreCase: false);
		if (metadataType is null)
			return null;

		var property = metadataType.GetProperty("All", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		if (property is null)
			return null;

		if (property.GetValue(null) is not SystemMetadata[] metadataItems)
			return null;

		var map = new Dictionary<Type, SystemMetadata>();
		for (var i = 0; i < metadataItems.Length; i++)
		{
			var metadata = metadataItems[i];
			if (metadata.SystemType is null) continue;
			map[metadata.SystemType] = metadata;
		}

		return map;
	}
}
