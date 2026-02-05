namespace Bezoro.ECS.Internal;

internal sealed class SystemBatch
{
	public HashSet<int> ReadTypes { get; } = new();

	public HashSet<int>          WriteTypes { get; } = new();
	public List<SystemExecution> Systems    { get; } = [];

	public bool CanAdd(SystemState state)
	{
		for (var i = 0; i < state.WriteIds.Length; i++)
		{
			int typeId = state.WriteIds[i];
			if (WriteTypes.Contains(typeId) || ReadTypes.Contains(typeId)) return false;
		}

		for (var i = 0; i < state.ReadIds.Length; i++)
		{
			int typeId = state.ReadIds[i];
			if (WriteTypes.Contains(typeId)) return false;
		}

		return true;
	}

	public void Add(SystemExecution execution)
	{
		Systems.Add(execution);

		var state = execution.State;

		for (var i = 0; i < state.ReadIds.Length; i++)
			ReadTypes.Add(state.ReadIds[i]);

		for (var i = 0; i < state.WriteIds.Length; i++)
			WriteTypes.Add(state.WriteIds[i]);
	}
}
