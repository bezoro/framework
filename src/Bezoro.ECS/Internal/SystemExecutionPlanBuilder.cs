using Bezoro.ECS.Types;

namespace Bezoro.ECS.Internal;

internal sealed class SystemExecutionPlanBuilder
{
	public Dictionary<SystemLoopPhase, Dictionary<Stage, List<SystemState[]>>> Build(List<SystemState> systems)
	{
		var phaseStagePlans = new Dictionary<SystemLoopPhase, Dictionary<Stage, List<SystemState[]>>>();
		foreach (SystemLoopPhase loopPhase in StageOrder.LoopPhases)
		{
			var stagePlans = new Dictionary<Stage, List<SystemState[]>>();
			foreach (Stage stage in StageOrder.Stages)
			{
				var stageSystems = new List<SystemState>();
				for (var systemIndex = 0; systemIndex < systems.Count; systemIndex++)
				{
					var state = systems[systemIndex];
					if (state.LoopPhase == loopPhase && state.Stage == stage)
						stageSystems.Add(state);
				}

				if (stageSystems.Count > 0)
					stagePlans[stage] = BuildStateBatches(stageSystems);
			}

			if (stagePlans.Count > 0)
				phaseStagePlans[loopPhase] = stagePlans;
		}

		return phaseStagePlans;
	}

	private static List<SystemState[]> BuildStateBatches(List<SystemState> stageSystems)
	{
		int count = stageSystems.Count;
		if (count == 0) return [];

		var indegree = new int[count];
		var edges    = new List<int>[count];
		for (var i = 0; i < count; i++)
			edges[i] = [];

		var indicesBySystemType = BuildSystemTypeIndexMap(stageSystems);
		for (var i = 0; i < count; i++)
		{
			AddOrderingEdges(stageSystems[i], i, indicesBySystemType, edges, indegree);

			for (int j = i + 1; j < count; j++)
			{
				if (Conflicts(stageSystems[i], stageSystems[j]))
					AddEdge(i, j, edges, indegree);
			}
		}

		var batches   = new List<SystemState[]>();
		var processed = new bool[count];
		int remaining = count;

		while (remaining > 0)
		{
			var selectedIndices = new List<int>();
			var selectedStates  = new List<SystemState>();

			for (var i = 0; i < count; i++)
			{
				if (processed[i] || indegree[i] != 0)
					continue;

				if (stageSystems[i].IsExclusive)
				{
					if (selectedIndices.Count > 0)
						continue;

					processed[i] = true;
					selectedIndices.Add(i);
					selectedStates.Add(stageSystems[i]);
					break;
				}

				processed[i] = true;
				selectedIndices.Add(i);
				selectedStates.Add(stageSystems[i]);
			}

			if (selectedIndices.Count == 0)
				throw new InvalidOperationException(
					$"System dependency graph contains a cycle. Stuck systems: {DescribeStuckSystems(stageSystems, processed)}"
				);

			batches.Add([.. selectedStates]);
			remaining -= selectedIndices.Count;

			for (var i = 0; i < selectedIndices.Count; i++)
			{
				var next = edges[selectedIndices[i]];
				for (var j = 0; j < next.Count; j++)
					indegree[next[j]]--;
			}
		}

		return batches;
	}

	private static bool Conflicts(SystemState first, SystemState second)
	{
		if (first.IsExclusive || second.IsExclusive)
			return true;

		return Overlaps(first.WriteIds, second.ReadIds) ||
			   Overlaps(first.WriteIds, second.WriteIds) ||
			   Overlaps(first.ReadIds, second.WriteIds);
	}

	private static bool Overlaps(int[] left, int[] right)
	{
		int i = 0;
		int j = 0;
		while (i < left.Length && j < right.Length)
		{
			if (left[i] == right[j])
				return true;

			if (left[i] < right[j]) i++;
			else j++;
		}

		return false;
	}

	private static Dictionary<Type, List<int>> BuildSystemTypeIndexMap(List<SystemState> stageSystems)
	{
		var map = new Dictionary<Type, List<int>>();
		for (var i = 0; i < stageSystems.Count; i++)
		{
			Type systemType = stageSystems[i].System.GetType();
			if (!map.TryGetValue(systemType, out var indices))
			{
				indices = [];
				map[systemType] = indices;
			}

			indices.Add(i);
		}

		return map;
	}

	private static void AddOrderingEdges(
		SystemState                 state,
		int                         stateIndex,
		Dictionary<Type, List<int>> indicesBySystemType,
		List<int>[]                 edges,
		int[]                       indegree)
	{
		for (var i = 0; i < state.BeforeSystemTypes.Length; i++)
		{
			if (!indicesBySystemType.TryGetValue(state.BeforeSystemTypes[i], out var dependentIndices))
				continue;

			for (var j = 0; j < dependentIndices.Count; j++)
			{
				int dependentIndex = dependentIndices[j];
				if (dependentIndex != stateIndex)
					AddEdge(stateIndex, dependentIndex, edges, indegree);
			}
		}

		for (var i = 0; i < state.AfterSystemTypes.Length; i++)
		{
			if (!indicesBySystemType.TryGetValue(state.AfterSystemTypes[i], out var prerequisiteIndices))
				continue;

			for (var j = 0; j < prerequisiteIndices.Count; j++)
			{
				int prerequisiteIndex = prerequisiteIndices[j];
				if (prerequisiteIndex != stateIndex)
					AddEdge(prerequisiteIndex, stateIndex, edges, indegree);
			}
		}
	}

	private static void AddEdge(int from, int to, List<int>[] edges, int[] indegree)
	{
		var targets = edges[from];
		for (var i = 0; i < targets.Count; i++)
		{
			if (targets[i] == to)
				return;
		}

		targets.Add(to);
		indegree[to]++;
	}

	private static string DescribeStuckSystems(List<SystemState> stageSystems, bool[] processed)
	{
		var stuck = new System.Text.StringBuilder();
		for (var i = 0; i < stageSystems.Count; i++)
		{
			if (processed[i])
				continue;

			if (stuck.Length > 0)
				stuck.Append(", ");

			stuck.Append(stageSystems[i].System.GetType().Name);
		}

		return stuck.ToString();
	}
}
