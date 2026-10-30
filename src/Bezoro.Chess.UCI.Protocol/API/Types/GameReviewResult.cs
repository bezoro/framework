using System.Collections.Immutable;

namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>Immutable game review with N played moves and N+1 reused position evaluations.</summary>
/// <param name="InitialFen">Starting position used with the complete move history for each UCI search.</param>
/// <param name="Options">Search budget and ratings used during the review.</param>
/// <param name="ExpectedPointsModel">Name supplied by the expected-points model.</param>
/// <param name="Positions">Position evaluations in chronological order.</param>
/// <param name="Moves">Reviewed moves in chronological order.</param>
/// <param name="WhiteAccuracy">100 times one minus white's mean expected-points loss, absent when white played no moves.</param>
/// <param name="BlackAccuracy">100 times one minus black's mean expected-points loss, absent when black played no moves.</param>
/// <remarks>The accuracy values use this library's explicit formula. They are not Chess.com's proprietary CAPS2 accuracy.</remarks>
public sealed record GameReviewResult(
	Fen InitialFen,
	GameReviewOptions Options,
	string ExpectedPointsModel,
	ImmutableArray<GameReviewEvaluation> Positions,
	ImmutableArray<ReviewedMove> Moves,
	double? WhiteAccuracy,
	double? BlackAccuracy);
