namespace Bezoro.Core.ECS
{
	public readonly struct Entity : IEntity
	{
		public int Id { get; }

		internal Entity(int id)
		{
			Id = id;
		}
	}
}
