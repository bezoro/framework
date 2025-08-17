namespace Bezoro.Core.ECS;

public readonly struct Entity : IEntity
{
	internal Entity(int id)
	{
		Id = id;
	}

	public int Id { get; }
}
