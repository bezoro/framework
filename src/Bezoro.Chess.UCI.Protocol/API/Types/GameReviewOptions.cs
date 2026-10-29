namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>Immutable search, concurrency, and resource budgets with optional player ratings for a game review.</summary>
public sealed class GameReviewOptions
{
	private readonly int _maxParallelPositions = 1;
	private readonly int _threadsPerEngine = 1;
	private readonly int _hashMegabytesPerEngine = 16;
	private readonly int _maxGameMoves = 4096;
	/// <summary>Creates a review configuration with an explicit depth, node, or move-time budget.</summary>
	/// <param name="search">Unrestricted, finite search parameters used for every nonterminal position.</param>
	/// <param name="whiteRating">Optional white-player rating for a custom expected-points model.</param>
	/// <param name="blackRating">Optional black-player rating for a custom expected-points model.</param>
	public GameReviewOptions(SearchParameters search, int? whiteRating = null, int? blackRating = null)
	{
		search.Validate();
		if (search.Infinite || search.Ponder || search.HasSearchMoves || search.Mate.HasValue ||
			search.WhiteTimeMs.HasValue || search.BlackTimeMs.HasValue || search.MovesToGo.HasValue ||
			search.WhiteIncrementMs.HasValue || search.BlackIncrementMs.HasValue ||
			!(search.Depth.HasValue || search.Nodes.HasValue || search.MoveTimeMs.HasValue))
			throw new ArgumentException("Review searches require an unrestricted depth, node, or move-time budget.", nameof(search));
		if (whiteRating is < 0)
			throw new ArgumentOutOfRangeException(nameof(whiteRating));
		if (blackRating is < 0)
			throw new ArgumentOutOfRangeException(nameof(blackRating));
		Search = search;
		WhiteRating = whiteRating;
		BlackRating = blackRating;
	}

	/// <summary>Gets the finite budget applied to each engine search.</summary>
	public SearchParameters Search { get; }
	/// <summary>Gets the optional white-player rating.</summary>
	public int? WhiteRating { get; }
	/// <summary>Gets the optional black-player rating.</summary>
	public int? BlackRating { get; }

	/// <summary>Gets the maximum number of simultaneous position searches, additionally bounded by the supplied client pool. Default is one.</summary>
	public int MaxParallelPositions
	{
		get => _maxParallelPositions;
		init => _maxParallelPositions = RequirePositive(value, nameof(MaxParallelPositions));
	}

	/// <summary>Gets the thread count requested through the engine's advertised Threads option. Default is one.</summary>
	/// <remarks>Budget CPU use as MaxParallelPositions times ThreadsPerEngine. The reviewer does not create engine processes.</remarks>
	public int ThreadsPerEngine
	{
		get => _threadsPerEngine;
		init => _threadsPerEngine = RequirePositive(value, nameof(ThreadsPerEngine));
	}

	/// <summary>Gets megabytes requested through the engine's advertised Hash option. Default is sixteen per active engine.</summary>
	/// <remarks>This bounds the requested transposition-table allocation, not the engine process's total memory.</remarks>
	public int HashMegabytesPerEngine
	{
		get => _hashMegabytesPerEngine;
		init => _hashMegabytesPerEngine = RequirePositive(value, nameof(HashMegabytesPerEngine));
	}

	/// <summary>Gets the maximum accepted played halfmoves, bounding history and result storage. Default is 4096.</summary>
	public int MaxGameMoves
	{
		get => _maxGameMoves;
		init => _maxGameMoves = RequirePositive(value, nameof(MaxGameMoves));
	}

	private static int RequirePositive(int value, string name) => value > 0
		? value : throw new ArgumentOutOfRangeException(name, "Review resource limits must be positive.");
}
