using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections.Immutable;

namespace Bezoro.Chess.UCI.Protocol.Domain.EngineClient;

/// <summary>
///     Holds shared data for a search session for the duration of an active "go ..." command.
///     Tracks output lines and bestmove completion.
/// </summary>
internal sealed class SearchSession(bool ponder)
{
	private readonly object _sync = new();
	private readonly List<PrincipalVariation> _principalVariations = [];
	private UciBestMoveMessage? _bestMove;
	private uint _reachedDepth;
	private uint _reachedSelDepth;
	private uint _multiPvValue;
	private uint _totalNodesSearched;
	private uint _totalTbHits;
	private uint _totalSearchTimeMs;

	/// <summary>True if this session was started in pondering mode.</summary>
	public bool Ponder { get; } = ponder;

	/// <summary>Completes when the engine emits "bestmove".</summary>
	public TaskCompletionSource<UciBestMoveMessage> BestMoveCompletion { get; } =
		new(TaskCreationOptions.RunContinuationsAsynchronously);

	/// <summary>
	///     Returns the latest captured bestmove message, if any.
	/// </summary>
	public UciBestMoveMessage? BestMove
	{
		get
		{
			lock (_sync)
			{
				return _bestMove;
			}
		}
	}

	/// <summary>
	///     Records a principal variation emitted during the active search.
	/// </summary>
	public void RecordInfo(PrincipalVariation principalVariation)
	{
		lock (_sync)
		{
			_principalVariations.Add(principalVariation);
			if (principalVariation.Depth > _reachedDepth) _reachedDepth = principalVariation.Depth;
			if (principalVariation.SelDepth > _reachedSelDepth) _reachedSelDepth = principalVariation.SelDepth;
			_multiPvValue       = principalVariation.MultiPv;
			_totalNodesSearched += principalVariation.Nodes;
			_totalTbHits        += principalVariation.TbHits;
			_totalSearchTimeMs  += principalVariation.Time;
		}
	}

	/// <summary>
	///     Completes the best move task with the supplied parsed message.
	/// </summary>
	public void CompleteBestMove(UciBestMoveMessage message)
	{
		lock (_sync)
		{
			_bestMove = message;
		}

		BestMoveCompletion.TrySetResult(message);
	}

	/// <summary>
	///     Builds the final immutable search result from the accumulated state.
	/// </summary>
	public SearchResult BuildResult(UciBestMoveMessage bestMove)
	{
		lock (_sync)
		{
			return new(
				_reachedDepth,
				_reachedSelDepth,
				_multiPvValue,
				_totalNodesSearched,
				_totalTbHits,
				_totalSearchTimeMs,
				[.. _principalVariations],
				bestMove.BestMove,
				bestMove.PonderMove
			);
		}
	}
}
