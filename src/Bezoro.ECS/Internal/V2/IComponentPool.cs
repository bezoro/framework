namespace Bezoro.ECS.Internal.V2;

internal interface IComponentPool
{
	int Population { get; }

	bool IsManagedLane { get; }

	bool Has(int entityId);

	void Remove(int entityId);

	void Clear();

	ReadOnlySpan<int> GetDenseEntities();
}
