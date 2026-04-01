using System.Collections.Generic;
using System.Threading.Tasks;
using Bezoro.Chess.UCI.API.Types;

namespace Bezoro.Chess.UCI.Internal;

internal sealed class CoordinatorClassificationRuntime(object sync)
{
	private readonly object _sync = sync;

	private TaskCompletionSource<IReadOnlyDictionary<string, Move>>? _completion;
	private int                                                       _generation;

	public int Generation
	{
		get
		{
			lock (_sync)
			{
				return _generation;
			}
		}
	}

	public Task<IReadOnlyDictionary<string, Move>>? CompletionTask
	{
		get
		{
			lock (_sync)
			{
				return _completion?.Task;
			}
		}
	}

	public int BeginRun()
	{
		lock (_sync)
		{
			_generation++;
			_completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
			return _generation;
		}
	}

	public void ApplyClassifiedMove(ref UciState state, Move move, int generation, out UciState? snapshot)
	{
		lock (_sync)
		{
			if (_generation != generation || !state.LegalMoves.Contains(move.Notation))
			{
				snapshot = null;
				return;
			}

			state    = state with { ClassifiedMoves = state.ClassifiedMoves.SetItem(move.Notation, move) };
			snapshot = state;
		}
	}

	public void CompleteRun(UciState state, int generation, out UciState? snapshot)
	{
		TaskCompletionSource<IReadOnlyDictionary<string, Move>>? completion;
		IReadOnlyDictionary<string, Move> result;

		lock (_sync)
		{
			if (_generation != generation)
			{
				snapshot = null;
				return;
			}

			completion = _completion;
			result     = state.ClassifiedMoves;
			snapshot   = state;
		}

		completion?.TrySetResult(result);
	}

	public void FaultRun(int generation, Exception ex)
	{
		TaskCompletionSource<IReadOnlyDictionary<string, Move>>? completion;
		lock (_sync)
		{
			if (_generation != generation)
				return;

			completion = _completion;
		}

		completion?.TrySetException(ex);
	}

	public void CancelRunIfActive(int generation)
	{
		TaskCompletionSource<IReadOnlyDictionary<string, Move>>? completion;
		lock (_sync)
		{
			if (_generation != generation)
				return;

			completion = _completion;
		}

		completion?.TrySetCanceled();
	}

	public void CancelCompletion()
	{
		TaskCompletionSource<IReadOnlyDictionary<string, Move>>? completion;
		lock (_sync)
		{
			_generation++;
			completion  = _completion;
			_completion = null;
		}

		completion?.TrySetCanceled();
	}
}
