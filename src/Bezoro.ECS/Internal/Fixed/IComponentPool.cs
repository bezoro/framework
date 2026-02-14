namespace Bezoro.ECS.Internal.Fixed;

internal interface IComponentPool
{
	bool IsManagedLane { get; }
	int  Population    { get; }

	bool Has(int entityId);

	ReadOnlySpan<int> GetDenseEntities();

	void Clear();

	void Remove(int entityId);
}
