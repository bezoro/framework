using System.Reflection;

namespace Bezoro.ECS.Options;

/// <summary>
///     Controls which snapshot types are allowed during world deserialization.
/// </summary>
public sealed class SnapshotDeserializationOptions
{
	private readonly Type[] _allowedComponentTypes = [];
	private readonly Type[] _allowedRelationTypes  = [];
	private readonly Type[] _allowedResourceTypes  = [];

	/// <summary>
	///     Gets the default options. Snapshot types are denied by default.
	/// </summary>
	public static SnapshotDeserializationOptions Default { get; } = new();

	/// <summary>
	///     Gets or initializes whether all component types are accepted.
	/// </summary>
	public bool AllowAllComponentTypes { get; init; }

	/// <summary>
	///     Gets or initializes whether all relation types are accepted.
	/// </summary>
	public bool AllowAllRelationTypes { get; init; }

	/// <summary>
	///     Gets or initializes whether all resource types are accepted.
	/// </summary>
	public bool AllowAllResourceTypes { get; init; }

	/// <summary>
	///     Gets or initializes explicit snapshot component type allowlist entries.
	/// </summary>
	public Type[] AllowedComponentTypes
	{
		get => _allowedComponentTypes;
		init => _allowedComponentTypes = value ?? throw new ArgumentNullException(nameof(AllowedComponentTypes));
	}

	/// <summary>
	///     Gets or initializes explicit snapshot relation type allowlist entries.
	/// </summary>
	public Type[] AllowedRelationTypes
	{
		get => _allowedRelationTypes;
		init => _allowedRelationTypes = value ?? throw new ArgumentNullException(nameof(AllowedRelationTypes));
	}

	/// <summary>
	///     Gets or initializes explicit snapshot resource type allowlist entries.
	/// </summary>
	public Type[] AllowedResourceTypes
	{
		get => _allowedResourceTypes;
		init => _allowedResourceTypes = value ?? throw new ArgumentNullException(nameof(AllowedResourceTypes));
	}

	/// <summary>
	///     Gets or initializes an additional type validator predicate.
	///     Returning false rejects the type.
	/// </summary>
	public Func<Type, bool>? TypeValidator { get; init; }

	internal bool IsComponentTypeAllowListed(Type type) =>
		AllowAllComponentTypes || ContainsType(AllowedComponentTypes, type);

	internal bool IsRelationTypeAllowListed(Type type) =>
		AllowAllRelationTypes || ContainsType(AllowedRelationTypes, type);

	internal bool IsResourceTypeAllowListed(Type type) =>
		AllowAllResourceTypes || ContainsType(AllowedResourceTypes, type);

	internal bool IsTypeAllowed(Type type)
	{
		if (!IsRuntimeSafe(type))
			return false;

		return TypeValidator?.Invoke(type) ?? true;
	}

	private static bool ContainsType(Type[] candidates, Type type)
	{
		for (var i = 0; i < candidates.Length; i++)
		{
			if (candidates[i] == type)
				return true;
		}

		return false;
	}

	private static bool IsRuntimeSafe(Type type)
	{
		if (type.IsPointer || type.IsByRef || type.ContainsGenericParameters || type.IsGenericTypeDefinition)
			return false;

		if (type == typeof(Type) ||
			typeof(MemberInfo).IsAssignableFrom(type) ||
			typeof(Delegate).IsAssignableFrom(type))
			return false;

		return true;
	}
}
