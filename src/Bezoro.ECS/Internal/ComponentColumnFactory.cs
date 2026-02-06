namespace Bezoro.ECS.Internal;

internal static class ComponentColumnFactory
{
	private const int ALIGNMENT_BYTES = 64;

	public static ComponentColumn Create(Type componentType, int capacity)
	{
		if (componentType is null) throw new ArgumentNullException(nameof(componentType));

		return ComponentTypeTraits.IsUnmanaged(componentType)
				   ? new UnmanagedComponentColumn(componentType, capacity, ALIGNMENT_BYTES)
				   : new ManagedComponentColumn(componentType, capacity);
	}
}
