using System.Reflection;
using System.Runtime.CompilerServices;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Internal;

internal sealed class GeneratedSystemMetadataResolver
{
	private const    string METADATA_TYPE_NAME = "Bezoro.ECS.Generated.GeneratedSystemMetadata";
	private readonly ConditionalWeakTable<Assembly, CacheEntry> _cache = new();

	public bool TryGet(Type systemType, out SystemMetadata metadata)
	{
		if (systemType is null) throw new ArgumentNullException(nameof(systemType));

		var map = GetOrCreateMap(systemType.Assembly);
		if (map is { } && map.TryGetValue(systemType, out metadata))
			return true;

		metadata = default;
		return false;
	}

	private static IReadOnlyDictionary<Type, SystemMetadata>? CreateMap(Assembly assembly)
	{
		var metadataType = assembly.GetType(METADATA_TYPE_NAME, false, false);
		if (metadataType is null)
			return null;

		var property = metadataType.GetProperty(
			"All", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
		);

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

	private IReadOnlyDictionary<Type, SystemMetadata>? GetOrCreateMap(Assembly assembly)
	{
		return _cache.GetValue(assembly, static key => new CacheEntry(CreateMap(key)))
					 .Map;
	}

	private sealed class CacheEntry(IReadOnlyDictionary<Type, SystemMetadata>? map)
	{
		public IReadOnlyDictionary<Type, SystemMetadata>? Map { get; } = map;
	}
}
