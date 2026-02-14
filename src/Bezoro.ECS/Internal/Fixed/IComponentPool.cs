namespace Bezoro.ECS.Internal.Fixed;

internal interface IComponentPool
{
	int Population { get; }

	bool IsManagedLane { get; }

	bool Has(int entityId);

	void Remove(int entityId);

	void Clear();

	ReadOnlySpan<int> GetDenseEntities();
}
