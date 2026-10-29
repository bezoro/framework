namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>Immutable search budget and optional player ratings for a game review.</summary>
public sealed class GameReviewOptions
{
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
}
