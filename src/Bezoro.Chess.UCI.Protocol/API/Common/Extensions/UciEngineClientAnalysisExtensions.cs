using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Bezoro.Chess.UCI.Protocol.API.Common.Extensions;

/// <summary>
///     Extension methods that layer reusable move and position analysis workflows on top of <see cref="UciEngineClient" />.
/// </summary>
public static class UciEngineClientAnalysisExtensions
{
	/// <summary>
	///     Evaluates the starting position and returns a player-relative centipawn baseline.
	/// </summary>
	/// <param name="client">Client used for the evaluation search.</param>
	/// <param name="playerColor">Player side: <c>w</c> or <c>b</c>.</param>
	/// <param name="moveTimeMs">Search time in milliseconds.</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>Baseline centipawn score from the player's perspective.</returns>
	public static async Task<int> EvaluateBaselineCpAsync(
		this UciEngineClient client,
		char                 playerColor,
		int                  moveTimeMs = 250,
		CancellationToken    ct         = default)
	{
		if (client is null) throw new ArgumentNullException(nameof(client));

		await client.SetPositionAsync(Fen.Default, [], ct).ConfigureAwait(false);
		var evaluation = await client.GoAsync(new() { MoveTimeMs = moveTimeMs }, ct).ConfigureAwait(false);

		if (evaluation.MateScore is { })
			return 0;

		return GetPlayerPerspectiveCp(evaluation.BestCpScore, Fen.Default.ActiveColor, playerColor);
	}

	/// <summary>
	///     Evaluates the current position and returns a player-relative advantage summary.
	/// </summary>
	/// <param name="client">Client used for the evaluation search.</param>
	/// <param name="sideToMove">Side to move for the current position: <c>w</c> or <c>b</c>.</param>
	/// <param name="playerColor">Player side: <c>w</c> or <c>b</c>.</param>
	/// <param name="baselineCp">Optional centipawn baseline to subtract after perspective normalization.</param>
	/// <param name="moveTimeMs">Search time in milliseconds.</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>Normalized position advantage.</returns>
	public static async Task<PositionAdvantage> EvaluateAdvantageAsync(
		this UciEngineClient client,
		char                 sideToMove,
		char                 playerColor,
		int                  baselineCp = 0,
		int                  moveTimeMs = 250,
		CancellationToken    ct         = default)
	{
		if (client is null) throw new ArgumentNullException(nameof(client));

		var evaluation = await client.GoAsync(new() { MoveTimeMs = moveTimeMs }, ct).ConfigureAwait(false);
		return PositionAdvantage.FromEngineScore(
			evaluation.BestCpScore,
			evaluation.MateScore,
			sideToMove,
			playerColor,
			baselineCp
		);
	}

	/// <summary>
	///     Evaluates legal moves for the current position, using MultiPV when the engine supports it and falling back to
	///     single-move searches when necessary.
	/// </summary>
	/// <param name="client">Client used for the analysis searches.</param>
	/// <param name="sideToMove">Side to move for the current position: <c>w</c> or <c>b</c>.</param>
	/// <param name="playerColor">Player side: <c>w</c> or <c>b</c>.</param>
	/// <param name="legalMoves">Legal moves in lowercase UCI notation.</param>
	/// <param name="currentScore">Current position score used to compute move deltas.</param>
	/// <param name="baselineCp">Optional centipawn baseline to subtract after perspective normalization.</param>
	/// <param name="multiPvMoveTimeMs">Search time in milliseconds when running a MultiPV search.</param>
	/// <param name="fallbackMoveTimeMs">Per-move search time in milliseconds when falling back to single-move searches.</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>Move evaluations sorted by descending preference.</returns>
	public static async Task<ImmutableArray<MoveEvaluation>> AnalyzeLegalMovesAsync(
		this UciEngineClient    client,
		char                    sideToMove,
		char                    playerColor,
		IEnumerable<string>     legalMoves,
		PositionScore           currentScore,
		int                     baselineCp         = 0,
		int                     multiPvMoveTimeMs  = 3_000,
		int                     fallbackMoveTimeMs = 250,
		CancellationToken       ct                 = default)
	{
		if (client is null) throw new ArgumentNullException(nameof(client));
		if (legalMoves is null) throw new ArgumentNullException(nameof(legalMoves));

		ImmutableArray<string> legalMovesSnapshot = legalMoves.NormalizeUciMoves();
		if (legalMovesSnapshot.IsDefaultOrEmpty)
			return [];

		if (!TryGetMultiPvOption(client, out var multiPvOption))
		{
			return await EvaluateMovesIndividuallyAsync(
				client,
				sideToMove,
				playerColor,
				legalMovesSnapshot,
				baselineCp,
				currentScore,
				fallbackMoveTimeMs,
				ct
			).ConfigureAwait(false);
		}

		int requestedMultiPv = legalMovesSnapshot.Length;
		if (multiPvOption.Max is int maxMultiPv)
			requestedMultiPv = Math.Min(requestedMultiPv, maxMultiPv);

		requestedMultiPv = Math.Max(1, requestedMultiPv);

		string restoreValue = string.IsNullOrWhiteSpace(multiPvOption.DefaultValue)
			? "1"
			: multiPvOption.DefaultValue;

		await client.SetOptionAsync(
			multiPvOption.Name,
			requestedMultiPv.ToString(CultureInfo.InvariantCulture),
			ct
		).ConfigureAwait(false);

		try
		{
			var result = await client.GoAsync(new() { MoveTimeMs = multiPvMoveTimeMs }, ct).ConfigureAwait(false);
			return await BuildMoveEvaluationsFromMultiPvAsync(
				client,
				result,
				sideToMove,
				playerColor,
				legalMovesSnapshot,
				baselineCp,
				currentScore,
				fallbackMoveTimeMs,
				ct
			).ConfigureAwait(false);
		}
		finally
		{
			await client.SetOptionAsync(multiPvOption.Name, restoreValue, CancellationToken.None).ConfigureAwait(false);
		}
	}

	private static MoveEvaluation BuildMoveEvaluation(
		string        move,
		int?          rawCpScore,
		int?          rawMateScore,
		char          sideToMove,
		char          playerColor,
		int           baselineCp,
		PositionScore currentScore)
	{
		var moveScore = PositionScore.FromEngineScore(
			rawCpScore,
			rawMateScore,
			sideToMove,
			playerColor,
			baselineCp
		);

		if (currentScore.Mate is int || moveScore.Mate is int)
			return new(move, moveScore.ToDisplayString(), moveScore.ToSortValue());

		int deltaCp = moveScore.Cp!.Value - currentScore.Cp!.Value;
		string sign = deltaCp >= 0 ? "+" : string.Empty;
		return new(
			move,
			$"{sign}{deltaCp.ToString(CultureInfo.InvariantCulture)} cp",
			deltaCp
		);
	}

	private static async Task<ImmutableArray<MoveEvaluation>> BuildMoveEvaluationsFromMultiPvAsync(
		UciEngineClient        client,
		SearchResult           result,
		char                   sideToMove,
		char                   playerColor,
		ImmutableArray<string> legalMoves,
		int                    baselineCp,
		PositionScore          currentScore,
		int                    fallbackMoveTimeMs,
		CancellationToken      ct)
	{
		var capturedVariations = new Dictionary<string, PrincipalVariation>(StringComparer.Ordinal);

		foreach (var variation in result.PrincipalVariations)
		{
			if (variation.Moves.IsDefaultOrEmpty)
				continue;

			string move = variation.Moves[0];
			if (!legalMoves.ContainsUciMove(move))
				continue;

			if (!capturedVariations.TryGetValue(move, out var existing) ||
				variation.Depth > existing.Depth ||
				variation.Depth == existing.Depth && variation.SelDepth >= existing.SelDepth)
			{
				capturedVariations[move] = variation;
			}
		}

		var evaluations = ImmutableArray.CreateBuilder<MoveEvaluation>(legalMoves.Length);
		foreach (string move in legalMoves)
		{
			if (capturedVariations.TryGetValue(move, out var variation))
			{
				evaluations.Add(
					BuildMoveEvaluation(
						move,
						variation.ScoreCp,
						variation.ScoreMate,
						sideToMove,
						playerColor,
						baselineCp,
						currentScore
					)
				);

				continue;
			}

			evaluations.Add(
				await EvaluateSingleMoveAsync(
					client,
					move,
					sideToMove,
					playerColor,
					baselineCp,
					currentScore,
					fallbackMoveTimeMs,
					ct
				).ConfigureAwait(false)
			);
		}

		var sorted = evaluations.ToImmutable();
		return [.. sorted.OrderByDescending(static evaluation => evaluation.SortValue)];
	}

	private static async Task<ImmutableArray<MoveEvaluation>> EvaluateMovesIndividuallyAsync(
		UciEngineClient        client,
		char                   sideToMove,
		char                   playerColor,
		ImmutableArray<string> legalMoves,
		int                    baselineCp,
		PositionScore          currentScore,
		int                    fallbackMoveTimeMs,
		CancellationToken      ct)
	{
		var evaluations = ImmutableArray.CreateBuilder<MoveEvaluation>(legalMoves.Length);

		foreach (string move in legalMoves)
		{
			evaluations.Add(
				await EvaluateSingleMoveAsync(
					client,
					move,
					sideToMove,
					playerColor,
					baselineCp,
					currentScore,
					fallbackMoveTimeMs,
					ct
				).ConfigureAwait(false)
			);
		}

		var sorted = evaluations.ToImmutable();
		return [.. sorted.OrderByDescending(static evaluation => evaluation.SortValue)];
	}

	private static async Task<MoveEvaluation> EvaluateSingleMoveAsync(
		UciEngineClient   client,
		string            move,
		char              sideToMove,
		char              playerColor,
		int               baselineCp,
		PositionScore     currentScore,
		int               fallbackMoveTimeMs,
		CancellationToken ct)
	{
		var result = await client.GoAsync(
			new()
			{
				MoveTimeMs = fallbackMoveTimeMs,
				SearchMoves = [move]
			},
			ct
		).ConfigureAwait(false);

		return BuildMoveEvaluation(
			move,
			result.BestCpScore,
			result.MateScore,
			sideToMove,
			playerColor,
			baselineCp,
			currentScore
		);
	}

	private static int GetPlayerPerspectiveCp(int? rawCpScore, char sideToMove, char playerColor)
	{
		int perspective = sideToMove == playerColor ? 1 : -1;
		return (rawCpScore ?? 0) * perspective;
	}

	private static bool TryGetMultiPvOption(UciEngineClient client, out UciEngineOption option)
	{
		if (client.TryGetOption("MultiPV", out option) && option.Type == UciOptionType.Spin)
			return true;

		option = default;
		return false;
	}
}
