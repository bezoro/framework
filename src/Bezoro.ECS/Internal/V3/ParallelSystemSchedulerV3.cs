using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Attributes;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Internal.V3;

internal sealed class ParallelSystemSchedulerV3 : IDisposable
{
	private readonly WorldV3           _world;
	private readonly World           _core;
	private readonly int               _maxWorkerCount;
	private readonly List<SystemEntry> _entries = [];
	private readonly List<int[]>       _waves = [];
	private          bool              _disposed;
	private          bool              _planDirty = true;
	private          int               _planRebuildCount;

	public ParallelSystemSchedulerV3(WorldV3 world, World core, int maxWorkerCount)
	{
		_world = world ?? throw new ArgumentNullException(nameof(world));
		_core = core ?? throw new ArgumentNullException(nameof(core));
		_maxWorkerCount = maxWorkerCount;
	}

	public void AddSystem(ISystemV3 system)
	{
		ThrowIfDisposed();
		if (system is null) throw new ArgumentNullException(nameof(system));

		var metadata = BuildAccessMetadata(system.GetType());
		_entries.Add(new(system, metadata, _core.CreateCommandStream()));
		_planDirty = true;
	}

	public SchedulerDiagnosticsV3 GetDiagnostics()
	{
		ThrowIfDisposed();
		var maxWaveWidth = 0;
		for (var i = 0; i < _waves.Count; i++)
		{
			if (_waves[i].Length > maxWaveWidth)
				maxWaveWidth = _waves[i].Length;
		}

		return new(_entries.Count, _waves.Count, maxWaveWidth, _planRebuildCount);
	}

	public void Tick(float deltaTime)
	{
		ThrowIfDisposed();
		if (_planDirty)
			RebuildPlan();

		if (_waves.Count == 0)
			return;

		var options = new ParallelOptions { MaxDegreeOfParallelism = _maxWorkerCount };
		for (var waveIndex = 0; waveIndex < _waves.Count; waveIndex++)
		{
			var wave = _waves[waveIndex];
			try
			{
				Parallel.ForEach(
					wave,
					options,
					systemIndex =>
					{
						var entry = _entries[systemIndex];
						entry.Commands.Reset();
						var context = new SystemContextV3(
							deltaTime,
							_world,
							new(entry.Commands)
						);
						entry.System.Update(in context);
					}
				);
			}
			catch
			{
				ResetWaveStreams(wave);
				throw;
			}

			for (var i = 0; i < wave.Length; i++)
			{
				int systemIndex = wave[i];
				var entry = _entries[systemIndex];
				if (entry.Commands.HasCommands)
					_core.Playback(entry.Commands);
			}
		}
	}

	public void ResetCommandStreams()
	{
		ThrowIfDisposed();
		for (var i = 0; i < _entries.Count; i++)
			_entries[i].Commands.Reset();
	}

	public void Dispose()
	{
		if (_disposed)
			return;

		for (var i = 0; i < _entries.Count; i++)
			_entries[i].Commands.Dispose();

		_entries.Clear();
		_waves.Clear();
		_disposed = true;
	}

	private void RebuildPlan()
	{
		_waves.Clear();
		var waveLists = new List<List<int>>();

		for (var systemIndex = 0; systemIndex < _entries.Count; systemIndex++)
		{
			var candidate = _entries[systemIndex];
			var assigned = false;
			for (var waveIndex = 0; waveIndex < waveLists.Count; waveIndex++)
			{
				var wave = waveLists[waveIndex];
				if (!CanJoinWave(candidate.Metadata, wave))
					continue;

				wave.Add(systemIndex);
				assigned = true;
				break;
			}

			if (!assigned)
				waveLists.Add([systemIndex]);
		}

		for (var i = 0; i < waveLists.Count; i++)
			_waves.Add(waveLists[i].ToArray());

		_planDirty = false;
		_planRebuildCount++;
	}

	private bool CanJoinWave(SystemAccessMetadata candidate, List<int> wave)
	{
		for (var i = 0; i < wave.Count; i++)
		{
			var existing = _entries[wave[i]].Metadata;
			if (Conflicts(candidate, existing))
				return false;
		}

		return true;
	}

	private SystemAccessMetadata BuildAccessMetadata(Type systemType)
	{
		var readTypeIds = new HashSet<int>();
		var writeTypeIds = new HashSet<int>();
		var exclusive = systemType.IsDefined(typeof(ExclusiveAttribute), inherit: true);

		// TODO: Replace reflection-based metadata extraction with source-generated metadata binding.
		var attributes = systemType.GetCustomAttributes(inherit: true);
		foreach (var attribute in attributes)
		{
			var attributeType = attribute.GetType();
			if (!attributeType.IsGenericType)
				continue;

			var genericTypeDefinition = attributeType.GetGenericTypeDefinition();
			if (genericTypeDefinition != typeof(ReadsAttribute<>) &&
				genericTypeDefinition != typeof(WritesAttribute<>))
				continue;

			var componentType = attributeType.GetGenericArguments()[0];
			int typeId = _core.GetOrCreateComponentTypeId(componentType);
			if (genericTypeDefinition == typeof(ReadsAttribute<>))
				readTypeIds.Add(typeId);
			else
				writeTypeIds.Add(typeId);
		}

		return new(ToSortedArray(readTypeIds), ToSortedArray(writeTypeIds), exclusive);
	}

	private static bool Conflicts(SystemAccessMetadata left, SystemAccessMetadata right)
	{
		if (left.IsExclusive || right.IsExclusive)
			return true;

		return HasIntersection(left.Writes, right.Writes) ||
			   HasIntersection(left.Writes, right.Reads) ||
			   HasIntersection(left.Reads, right.Writes);
	}

	private static bool HasIntersection(int[] left, int[] right)
	{
		var i = 0;
		var j = 0;
		while (i < left.Length && j < right.Length)
		{
			int leftValue = left[i];
			int rightValue = right[j];
			if (leftValue == rightValue)
				return true;

			if (leftValue < rightValue)
				i++;
			else
				j++;
		}

		return false;
	}

	private static int[] ToSortedArray(HashSet<int> values)
	{
		if (values.Count == 0)
			return [];

		var result = new int[values.Count];
		var index = 0;
		foreach (int value in values)
			result[index++] = value;

		Array.Sort(result);
		return result;
	}

	private void ResetWaveStreams(int[] wave)
	{
		for (var i = 0; i < wave.Length; i++)
			_entries[wave[i]].Commands.Reset();
	}

	private void ThrowIfDisposed()
	{
		if (_disposed)
			throw new ObjectDisposedException(nameof(ParallelSystemSchedulerV3));
	}

	private sealed class SystemEntry(ISystemV3 system, SystemAccessMetadata metadata, CommandStream commands)
	{
		public ISystemV3 System { get; } = system;
		public SystemAccessMetadata Metadata { get; } = metadata;
		public CommandStream Commands { get; } = commands;
	}
}

