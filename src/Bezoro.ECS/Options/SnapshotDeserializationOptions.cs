using System.Reflection;

namespace Bezoro.ECS.Options;

/// <summary>
///     Controls which snapshot types are allowed during world deserialization.
/// </summary>
public sealed class SnapshotDeserializationOptions
{
	private readonly Type[] _allowedReferenceResourceTypes = [];

	/// <summary>
	///     Gets the default options.
	/// </summary>
	public static SnapshotDeserializationOptions Default { get; } = new();

	/// <summary>
	///     Gets or initializes the type resolver used for snapshot type names.
	///     When null, the runtime loaded-assembly resolver is used.
	/// </summary>
	public Func<string, Type?>? TypeResolver { get; init; }

	/// <summary>
	///     Gets or initializes an additional type validator predicate.
	///     Returning false rejects the type.
	/// </summary>
	public Func<Type, bool>? TypeValidator { get; init; }

	/// <summary>
	///     Gets or initializes explicit reference-type resource allowlist entries.
	///     Value-type resources are always allowed.
	/// </summary>
	public Type[] AllowedReferenceResourceTypes
	{
		get => _allowedReferenceResourceTypes;
		init => _allowedReferenceResourceTypes = value ??
												 throw new ArgumentNullException(
													 nameof(AllowedReferenceResourceTypes)
												 );
	}

	internal bool IsReferenceResourceTypeAllowed(Type type)
	{
		if (type.IsValueType)
			return true;

		for (var i = 0; i < AllowedReferenceResourceTypes.Length; i++)
		{
			if (AllowedReferenceResourceTypes[i] == type)
				return true;
		}

		return false;
	}

	internal bool IsTypeAllowed(Type type)
	{
		if (!IsRuntimeSafe(type))
			return false;

		return TypeValidator?.Invoke(type) ?? true;
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
