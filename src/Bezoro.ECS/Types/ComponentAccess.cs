namespace Bezoro.ECS.Types;

/// <summary>
///     Declares the access pattern a system requires for a component type.
/// </summary>
public readonly struct ComponentAccess
{
	/// <summary>
	///     Initializes a new instance of the <see cref="ComponentAccess" /> struct.
	/// </summary>
	/// <param name="componentType">The component type being accessed.</param>
	/// <param name="mode">The access mode.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="componentType" /> is null.</exception>
	/// <exception cref="ArgumentException">Thrown when <paramref name="componentType" /> is not a component struct.</exception>
	public ComponentAccess(Type componentType, ComponentAccessMode mode)
	{
		if (componentType is null) throw new ArgumentNullException(nameof(componentType));

		if (!componentType.IsValueType)
			throw new ArgumentException(
				"Component types must be value types.", nameof(componentType)
			);

		ComponentType = componentType;
		Mode          = mode;
	}

	/// <summary>
	///     Gets the access mode.
	/// </summary>
	public ComponentAccessMode Mode { get; }

	/// <summary>
	///     Gets the component type being accessed.
	/// </summary>
	public Type ComponentType { get; }

	/// <summary>
	///     Creates a read-only access descriptor for component type <typeparamref name="T" />.
	/// </summary>
	/// <typeparam name="T">The component type.</typeparam>
	/// <returns>A read-only access descriptor.</returns>
	public static ComponentAccess Read<T>() where T : struct =>
		new(typeof(T), ComponentAccessMode.ReadOnly);

	/// <summary>
	///     Creates a read-write access descriptor for component type <typeparamref name="T" />.
	/// </summary>
	/// <typeparam name="T">The component type.</typeparam>
	/// <returns>A read-write access descriptor.</returns>
	public static ComponentAccess Write<T>() where T : struct =>
		new(typeof(T), ComponentAccessMode.ReadWrite);
}
