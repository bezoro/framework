using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Bezoro.ECS.Internal;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Services;

/// <summary>
///     Handles binary serialization and deserialization of <see cref="World" /> state snapshots.
/// </summary>
internal static class WorldSerializer
{
	private const           int                   SNAPSHOT_FORMAT_VERSION = 1;
	private const           int                   MAX_SNAPSHOT_BYTES = 64 * 1024 * 1024;
	private const           int                   MAX_ARCHETYPE_COUNT = 100_000;
	private const           int                   MAX_COMPONENT_TYPES_PER_ARCHETYPE = 4_096;
	private const           int                   MAX_RELATIONSHIPS_PER_ARCHETYPE = 4_096;
	private const           int                   MAX_ENTITIES_PER_ARCHETYPE = 1_000_000;
	private const           int                   MAX_RESOURCE_COUNT = 65_536;
	private const           int                   MAX_PAYLOAD_LENGTH_BYTES = 4 * 1024 * 1024;
	private static readonly byte[]                SnapshotMagic          = [(byte)'B', (byte)'Z', (byte)'E', (byte)'C'];
	private static readonly JsonSerializerOptions SnapshotJsonOptions    = new() { IncludeFields = true };

	/// <summary>
	///     Serializes the entire world state into a portable binary snapshot.
	/// </summary>
	/// <param name="world">The world to serialize.</param>
	/// <returns>A byte array containing the serialized snapshot.</returns>
	internal static byte[] Serialize(World world)
	{
		using var stream = new MemoryStream();
		using var writer = new BinaryWriter(stream, Encoding.UTF8, true);

		writer.Write(SnapshotMagic);
		writer.Write(SNAPSHOT_FORMAT_VERSION);

		var archetypes       = world.Archetypes;
		var archetypesToWrite = new List<Archetype>();
		for (var a = 0; a < archetypes.Count; a++)
		{
			var archetype   = archetypes[a];
			var entityCount = 0;
			for (var c = 0; c < archetype.Chunks.Count; c++)
				entityCount += archetype.Chunks[c].Count;

			if (entityCount > 0)
				archetypesToWrite.Add(archetype);
		}

		writer.Write(archetypesToWrite.Count);
		for (var a = 0; a < archetypesToWrite.Count; a++)
		{
			var archetype = archetypesToWrite[a];

			var serializedTypeIds = new List<int>();
			for (var i = 0; i < archetype.TypeIds.Length; i++)
			{
				int typeId = archetype.TypeIds[i];
				if (world.ComponentTypeRegistry.IsRelationship(typeId))
					continue;

				serializedTypeIds.Add(typeId);
			}

			writer.Write(serializedTypeIds.Count);
			for (var i = 0; i < serializedTypeIds.Count; i++)
			{
				int typeId = serializedTypeIds[i];
				var type   = world.ComponentTypeRegistry.GetType(typeId);
				writer.Write(type.AssemblyQualifiedName!);
				writer.Write(ComputeLayoutHash(type));
				writer.Write((byte)GetSnapshotPayloadKind(type));
			}

			var relationshipDescriptors = new List<RelationshipInfo>();
			for (var i = 0; i < archetype.TypeIds.Length; i++)
			{
				int typeId = archetype.TypeIds[i];
				if (!world.ComponentTypeRegistry.IsRelationship(typeId))
					continue;

				if (!world.ComponentTypeRegistry.TryGetRelationshipInfo(typeId, out var relationship))
					throw new InvalidOperationException(
						"Relationship type information is missing for snapshot serialization."
					);

				relationshipDescriptors.Add(relationship);
			}

			writer.Write(relationshipDescriptors.Count);
			for (var i = 0; i < relationshipDescriptors.Count; i++)
			{
				var relationship = relationshipDescriptors[i];
				writer.Write(relationship.RelationType.AssemblyQualifiedName!);
				writer.Write(relationship.Target.Id);
				writer.Write(relationship.Target.Version);
				// Snapshot v1 reserves the target world id field. Entity handles are now {id, version}.
				writer.Write(0);
			}

			var rows = new List<(Chunk Chunk, int Row)>();
			for (var c = 0; c < archetype.Chunks.Count; c++)
			{
				var chunk = archetype.Chunks[c];
				for (var row = 0; row < chunk.Count; row++)
					rows.Add((chunk, row));
			}

			writer.Write(rows.Count);
			for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
			{
				var entry  = rows[rowIndex];
				var entity = entry.Chunk.Entities[entry.Row];
				writer.Write(entity.Id);
				writer.Write(entity.Version);
			}

			for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
			{
				var entry = rows[rowIndex];
				for (var typeIndex = 0; typeIndex < serializedTypeIds.Count; typeIndex++)
				{
					int    typeId         = serializedTypeIds[typeIndex];
					int    componentIndex = archetype.GetTypeIndex(typeId);
					var    type           = world.ComponentTypeRegistry.GetType(typeId);
					var    payloadKind    = GetSnapshotPayloadKind(type);
					object value          = entry.Chunk.GetValue(componentIndex, entry.Row);
					byte[] payload        = SerializeSnapshotValue(type, value, payloadKind);
					writer.Write(payload.Length);
					writer.Write(payload);
				}
			}
		}

		var resources = world.Resources;
		writer.Write(resources.Count);
		foreach ((var resourceType, object? resourceBox) in resources)
		{
			var payloadKind = GetSnapshotPayloadKind(resourceType);
			var valueField = resourceBox.GetType().GetField("Value", BindingFlags.Instance | BindingFlags.Public) ??
							 throw new InvalidOperationException("Resource box does not expose a value field.");

			object value = valueField.GetValue(resourceBox) ??
						   throw new InvalidOperationException("Resource value cannot be null.");

			byte[] payload = SerializeSnapshotValue(resourceType, value, payloadKind);

			writer.Write(resourceType.AssemblyQualifiedName!);
			writer.Write(ComputeLayoutHash(resourceType));
			writer.Write((byte)payloadKind);
			writer.Write(payload.Length);
			writer.Write(payload);
		}

		writer.Flush();
		return stream.ToArray();
	}

	/// <summary>
	///     Deserializes a binary snapshot into a new <see cref="World" /> instance.
	/// </summary>
	/// <param name="bytes">The binary snapshot data.</param>
	/// <returns>A new world populated from the snapshot.</returns>
	internal static World Deserialize(byte[] bytes)
	{
		if (bytes is null) throw new ArgumentNullException(nameof(bytes));
		if (bytes.Length > MAX_SNAPSHOT_BYTES)
			throw new InvalidOperationException(
				$"Invalid snapshot payload: snapshot size cannot exceed {MAX_SNAPSHOT_BYTES} bytes."
			);

		try
		{
			using var stream = new MemoryStream(bytes, false);
			using var reader = new BinaryReader(stream, Encoding.UTF8, true);

			byte[] magic = ReadBytesExact(reader, SnapshotMagic.Length);
			for (var i = 0; i < SnapshotMagic.Length; i++)
			{
				if (magic[i] != SnapshotMagic[i])
					throw new InvalidOperationException("Invalid snapshot payload: unsupported header.");
			}

			int version = reader.ReadInt32();
			if (version != SNAPSHOT_FORMAT_VERSION)
				throw new InvalidOperationException($"Invalid snapshot payload: unsupported version '{version}'.");

			var world                = new World();
			var entityMap            = new Dictionary<(int Id, int Version), Entity>();
			var pendingRelationships = new List<(Entity Source, Type RelationType, int TargetId, int TargetVersion)>();
			int archetypeCount       = ReadBoundedCount(reader, "archetype count", MAX_ARCHETYPE_COUNT);

			for (var archetypeIndex = 0; archetypeIndex < archetypeCount; archetypeIndex++)
			{
				int componentTypeCount =
					ReadBoundedCount(reader, "component type count", MAX_COMPONENT_TYPES_PER_ARCHETYPE);

				var componentTypes = new Type[componentTypeCount];
				var payloadKinds   = new SnapshotPayloadKind[componentTypeCount];
				for (var typeIndex = 0; typeIndex < componentTypeCount; typeIndex++)
				{
					string typeName = reader.ReadString();
					var type = ResolveTypeFromLoadedAssemblies(typeName) ??
							   throw new InvalidOperationException(
								   $"Snapshot type '{typeName}' could not be resolved."
							   );

					ulong expectedLayoutHash = reader.ReadUInt64();
					ulong actualLayoutHash   = ComputeLayoutHash(type);
					if (expectedLayoutHash != actualLayoutHash)
						throw new InvalidOperationException(
							$"Invalid snapshot payload: layout mismatch for '{type.FullName}'."
						);

					var payloadKind = (SnapshotPayloadKind)reader.ReadByte();
					if (payloadKind != GetSnapshotPayloadKind(type))
						throw new InvalidOperationException(
							$"Invalid snapshot payload: storage kind mismatch for '{type.FullName}'."
						);

					componentTypes[typeIndex] = type;
					payloadKinds[typeIndex]   = payloadKind;
				}

				int relationshipCount =
					ReadBoundedCount(reader, "relationship count", MAX_RELATIONSHIPS_PER_ARCHETYPE);

				var relationshipDescriptors =
					new (Type RelationType, int TargetId, int TargetVersion)[relationshipCount];

				for (var relationshipIndex = 0; relationshipIndex < relationshipCount; relationshipIndex++)
				{
					string relationTypeName = reader.ReadString();
					var relationType = ResolveTypeFromLoadedAssemblies(relationTypeName) ??
									   throw new InvalidOperationException(
										   $"Snapshot relation type '{relationTypeName}' could not be resolved."
									   );

					int targetId      = reader.ReadInt32();
					int targetVersion = reader.ReadInt32();
					_                                          = reader.ReadInt32(); // reserved in snapshot v1
					relationshipDescriptors[relationshipIndex] = (relationType, targetId, targetVersion);
				}

				var archetype = componentTypes.Length == 0
									? world.EmptyArchetype
									: world.GetOrCreateArchetype(componentTypes);

				int entityCount = ReadBoundedCount(reader, "entity count", MAX_ENTITIES_PER_ARCHETYPE);

				var snapshotEntities = new (int Id, int Version)[entityCount];
				for (var entityIndex = 0; entityIndex < entityCount; entityIndex++)
					snapshotEntities[entityIndex] = (reader.ReadInt32(), reader.ReadInt32());

				var entities = new Entity[entityCount];
				for (var entityIndex = 0; entityIndex < entityCount; entityIndex++)
				{
					entities[entityIndex] = world.CreateEntityInternal(archetype);
					var snapshotEntity = snapshotEntities[entityIndex];
					if (!entityMap.TryAdd(snapshotEntity, entities[entityIndex]))
						throw new InvalidOperationException(
							$"Invalid snapshot payload: duplicate entity mapping for id '{snapshotEntity.Id}:{snapshotEntity.Version}'."
						);
				}

				for (var entityIndex = 0; entityIndex < entityCount; entityIndex++)
				{
					var entity = entities[entityIndex];
					for (var typeIndex = 0; typeIndex < componentTypes.Length; typeIndex++)
					{
						int length = ReadBoundedLength(
							reader,
							"component payload length",
							MAX_PAYLOAD_LENGTH_BYTES
						);

						byte[] payload = ReadBytesExact(reader, length);
						var    componentType = componentTypes[typeIndex];
						object value = DeserializeSnapshotValue(componentType, payloadKinds[typeIndex], payload);
						int    typeId = world.GetOrCreateComponentTypeId(componentType);
						if (!world.TryGetComponentArray(
								entity, typeId, out var chunk, out int slot, out int componentIndex
							))
							throw new InvalidOperationException(
								$"Invalid snapshot payload: missing component slot for '{componentType.FullName}'."
							);

						chunk.SetValue(componentIndex, slot, value);
					}

					for (var relationshipIndex =
							 0;
						 relationshipIndex < relationshipDescriptors.Length;
						 relationshipIndex++)
					{
						(var relationType, int targetId, int targetVersion) =
							relationshipDescriptors[relationshipIndex];

						pendingRelationships.Add(
							(entities[entityIndex], RelationType: relationType, TargetId: targetId,
							 TargetVersion: targetVersion)
						);
					}
				}
			}

			int resourceCount = ReadBoundedCount(reader, "resource count", MAX_RESOURCE_COUNT);

			for (var resourceIndex = 0; resourceIndex < resourceCount; resourceIndex++)
			{
				string typeName = reader.ReadString();
				var type = ResolveTypeFromLoadedAssemblies(typeName) ??
						   throw new InvalidOperationException($"Snapshot type '{typeName}' could not be resolved.");

				ulong expectedLayoutHash = reader.ReadUInt64();
				ulong actualLayoutHash   = ComputeLayoutHash(type);
				if (expectedLayoutHash != actualLayoutHash)
					throw new InvalidOperationException(
						$"Invalid snapshot payload: layout mismatch for '{type.FullName}'."
					);

				var payloadKind = (SnapshotPayloadKind)reader.ReadByte();
				if (payloadKind != GetSnapshotPayloadKind(type))
					throw new InvalidOperationException(
						$"Invalid snapshot payload: storage kind mismatch for '{type.FullName}'."
					);

				int length = ReadBoundedLength(
					reader,
					"resource payload length",
					MAX_PAYLOAD_LENGTH_BYTES
				);

				byte[] payload = ReadBytesExact(reader, length);
				object value   = DeserializeSnapshotValue(type, payloadKind, payload);
				world.SetResourceObject(type, value);
			}

			for (var i = 0; i < pendingRelationships.Count; i++)
			{
				(var source, var relationType, int targetId, int targetVersion) = pendingRelationships[i];
				var target = targetId == Entity.Wildcard.Id
								 ? Entity.Wildcard
								 : entityMap.TryGetValue(
									 (targetId, targetVersion), out var mappedTarget
								 )
									 ? mappedTarget
									 : throw new InvalidOperationException(
										   $"Invalid snapshot payload: relationship target '{targetId}:{targetVersion}' was not found."
									   );

				world.AddRelationshipObject(source, relationType, target);
			}

			return world;
		}
		catch (InvalidOperationException)
		{
			throw;
		}
		catch (Exception ex) when (ex is EndOfStreamException or IOException or JsonException or ArgumentException
									     or NotSupportedException or OverflowException)
		{
			throw new InvalidOperationException("Invalid snapshot payload.", ex);
		}
	}

	private static int ReadBoundedCount(BinaryReader reader, string label, int maximum)
	{
		int value = reader.ReadInt32();
		if (value < 0)
			throw new InvalidOperationException($"Invalid snapshot payload: {label} cannot be negative.");

		if (value > maximum)
			throw new InvalidOperationException(
				$"Invalid snapshot payload: {label} cannot exceed {maximum}."
			);

		return value;
	}

	private static int ReadBoundedLength(BinaryReader reader, string label, int maximum)
	{
		int value = reader.ReadInt32();
		if (value < 0)
			throw new InvalidOperationException($"Invalid snapshot payload: {label} cannot be negative.");

		if (value > maximum)
			throw new InvalidOperationException(
				$"Invalid snapshot payload: {label} cannot exceed {maximum}."
			);

		return value;
	}

	private static byte[] ReadBytesExact(BinaryReader reader, int length)
	{
		byte[] bytes = reader.ReadBytes(length);
		if (bytes.Length != length)
			throw new InvalidOperationException("Invalid snapshot payload: unexpected end of data.");

		return bytes;
	}

	private static Assembly? ResolveAssembly(AssemblyName assemblyName)
	{
		var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
		for (var i = 0; i < loadedAssemblies.Length; i++)
		{
			var loadedAssembly = loadedAssemblies[i];
			if (AssemblyName.ReferenceMatchesDefinition(loadedAssembly.GetName(), assemblyName))
				return loadedAssembly;
		}

		return null;
	}

	private static Type? ResolveType(Assembly? assembly, string typeName, bool ignoreCase)
	{
		if (assembly is not null)
			return assembly.GetType(typeName, false, ignoreCase);

		var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
		for (var i = 0; i < loadedAssemblies.Length; i++)
		{
			var resolved = loadedAssemblies[i].GetType(typeName, false, ignoreCase);
			if (resolved is not null)
				return resolved;
		}

		return null;
	}

	private static Type? ResolveTypeFromLoadedAssemblies(string typeName) =>
		Type.GetType(
			typeName,
			ResolveAssembly,
			ResolveType,
			throwOnError: false,
			ignoreCase: false
		);

	private static byte[] SerializeRawUnmanagedValue(Type type, object value)
	{
		if (!ComponentTypeTraits.IsUnmanaged(type))
			throw new InvalidOperationException($"Type '{type.FullName}' is not unmanaged.");

		int size    = Marshal.SizeOf(type);
		var payload = new byte[size];
		var handle  = GCHandle.Alloc(payload, GCHandleType.Pinned);
		try
		{
			Marshal.StructureToPtr(value, handle.AddrOfPinnedObject(), false);
			return payload;
		}
		finally
		{
			handle.Free();
		}
	}

	private static byte[] SerializeSnapshotValue(Type type, object value, SnapshotPayloadKind payloadKind) =>
		payloadKind switch
		{
			SnapshotPayloadKind.RawUnmanaged => SerializeRawUnmanagedValue(type, value),
			SnapshotPayloadKind.Json         => JsonSerializer.SerializeToUtf8Bytes(value, type, SnapshotJsonOptions),
			_                                => throw new ArgumentOutOfRangeException(nameof(payloadKind))
		};

	private static object DeserializeRawUnmanagedValue(Type type, byte[] payload)
	{
		if (!ComponentTypeTraits.IsUnmanaged(type))
			throw new InvalidOperationException($"Type '{type.FullName}' is not unmanaged.");

		int expectedSize = Marshal.SizeOf(type);
		if (payload.Length != expectedSize)
			throw new InvalidOperationException($"Snapshot payload size mismatch for '{type.FullName}'.");

		var handle = GCHandle.Alloc(payload, GCHandleType.Pinned);
		try
		{
			return Marshal.PtrToStructure(handle.AddrOfPinnedObject(), type) ??
				   throw new InvalidOperationException(
					   $"Snapshot value for '{type.FullName}' could not be materialized."
				   );
		}
		finally
		{
			handle.Free();
		}
	}

	private static object DeserializeSnapshotValue(Type type, SnapshotPayloadKind payloadKind, byte[] payload) =>
		payloadKind switch
		{
			SnapshotPayloadKind.RawUnmanaged => DeserializeRawUnmanagedValue(type, payload),
			SnapshotPayloadKind.Json => JsonSerializer.Deserialize(payload, type, SnapshotJsonOptions) ??
										throw new InvalidOperationException(
											$"Snapshot value for '{type.FullName}' could not be deserialized."
										),
			_ => throw new ArgumentOutOfRangeException(nameof(payloadKind))
		};

	private static SnapshotPayloadKind GetSnapshotPayloadKind(Type type) =>
		ComponentTypeTraits.IsUnmanaged(type) ? SnapshotPayloadKind.RawUnmanaged : SnapshotPayloadKind.Json;

	private static ulong ComputeFnv1AHash64(string value)
	{
		const ulong OFFSET = 14695981039346656037;
		const ulong PRIME  = 1099511628211;
		ulong       hash   = OFFSET;

		byte[] bytes = Encoding.UTF8.GetBytes(value);
		for (var i = 0; i < bytes.Length; i++)
		{
			hash ^= bytes[i];
			hash *= PRIME;
		}

		return hash;
	}

	private static ulong ComputeLayoutHash(Type type)
	{
		var builder = new StringBuilder();
		AppendLayout(type, builder, new());
		return ComputeFnv1AHash64(builder.ToString());
	}

	private static void AppendLayout(Type type, StringBuilder builder, HashSet<Type> visited)
	{
		builder.Append("T=").Append(type.AssemblyQualifiedName).Append(';');
		if (!visited.Add(type))
			return;

		if (ComponentTypeTraits.IsUnmanaged(type))
			builder.Append("S=").Append(Marshal.SizeOf(type)).Append(';');

		var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		Array.Sort(fields, static (left, right) => string.CompareOrdinal(left.Name, right.Name));
		for (var i = 0; i < fields.Length; i++)
		{
			var field = fields[i];
			builder.Append("F=").Append(field.Name).Append(':').Append(field.FieldType.AssemblyQualifiedName)
				   .Append(';');

			var fieldType = field.FieldType;
			if (fieldType.IsValueType && !fieldType.IsPrimitive && !fieldType.IsEnum)
				AppendLayout(fieldType, builder, visited);
		}
	}

	private static long GetBytesPerEntity(Type[] componentTypes)
	{
		long bytesPerEntity = ComponentSizeEstimator.GetSizeInBytes(typeof(Entity));
		for (var i = 0; i < componentTypes.Length; i++)
			bytesPerEntity += ComponentSizeEstimator.GetSizeInBytes(componentTypes[i]);

		return bytesPerEntity;
	}

	private enum SnapshotPayloadKind : byte
	{
		RawUnmanaged = 0,
		Json         = 1
	}

	private sealed class SnapshotComponent
	{
		public string Json     { get; set; } = string.Empty;
		public string TypeName { get; set; } = string.Empty;
	}

	private sealed class SnapshotEntity
	{
		public List<SnapshotComponent> Components { get; set; } = [];
	}

	private sealed class SnapshotResource
	{
		public string Json     { get; set; } = string.Empty;
		public string TypeName { get; set; } = string.Empty;
	}

	private sealed class WorldSnapshot
	{
		public List<SnapshotEntity>   Entities  { get; set; } = [];
		public List<SnapshotResource> Resources { get; set; } = [];
	}
}
