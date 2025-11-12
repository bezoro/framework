namespace Bezoro.Core.ECS;

/// <summary>
/// Abstraction for any identifiable entity within the ECS model.
/// Implementors must provide a unique, read-only Id property.
/// </summary>
public interface IEntity
{
	/// <summary>
	/// Gets the unique integer identifier for this entity.
	/// </summary>
	int Id { get; }
}
