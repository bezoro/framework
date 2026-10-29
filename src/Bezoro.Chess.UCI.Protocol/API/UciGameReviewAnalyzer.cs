using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.Chess.UCI.Protocol.Internal;

namespace Bezoro.Chess.UCI.Protocol.API;

/// <summary>Reviews played games using N+1 position searches and locally adjudicated terminal positions.</summary>
/// <remarks>
/// The caller owns and starts the dedicated client. Use one analyzer per client and do not use the client for other work during a review.
/// Reviews on this analyzer are serialized. Engine options enable full strength, one PV, and WDL when advertised.
/// The default probability model uses engine self-play WDL, not human winning chances or proprietary CAPS2.
/// </remarks>
public sealed class UciGameReviewAnalyzer
{
	private readonly UciEngineClient _client;
	private readonly GameReviewOptions _options;
	private readonly IExpectedPointsModel _model;
	private readonly IMoveReviewAnnotationPolicy? _annotationPolicy;
	private readonly SemaphoreSlim _gate = new(1, 1);

	/// <summary>Creates a reviewer using a dedicated, already started UCI client.</summary>
	/// <param name="client">Caller-owned dedicated engine client.</param>
	/// <param name="options">Finite search budget and optional player ratings.</param>
	/// <param name="expectedPointsModel">Probability model, or the engine-WDL model when omitted.</param>
	/// <param name="annotationPolicy">Optional application policy for special annotations.</param>
	public UciGameReviewAnalyzer(
		UciEngineClient client,
		GameReviewOptions options,
		IExpectedPointsModel? expectedPointsModel = null,
		IMoveReviewAnnotationPolicy? annotationPolicy = null)
	{
		_client = client ?? throw new ArgumentNullException(nameof(client));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_model = expectedPointsModel ?? EngineWdlExpectedPointsModel.Instance;
		_annotationPolicy = annotationPolicy;
	}

	/// <summary>Validates the entire game, then reviews its positions in chronological order.</summary>
	/// <param name="initialFen">Complete starting FEN. Both kings must be present.</param>
	/// <param name="playedMoves">Complete played history in UCI notation, copied before engine work begins.</param>
	/// <param name="progress">Optional completion progress. Use Progress&lt;T&gt; to marshal callbacks to an engine UI thread.</param>
	/// <param name="ct">Cancellation token for validation, queued reviews, and engine searches.</param>
	/// <returns>Immutable scores, expected-points losses, classifications, and explicitly defined accuracy values.</returns>
	/// <exception cref="NotSupportedException">The default model requires WDL evidence for non-mate engine scores.</exception>
	public async Task<GameReviewResult> AnalyzeAsync(
		Fen initialFen,
		IEnumerable<string> playedMoves,
		IProgress<GameReviewProgress>? progress = null,
		CancellationToken ct = default)
	{
		if (playedMoves is null)
			throw new ArgumentNullException(nameof(playedMoves));
		ct.ThrowIfCancellationRequested();
		ValidateInitialPosition(initialFen);

		var copiedMoves = new List<string>();
		foreach (string move in playedMoves)
		{
			ct.ThrowIfCancellationRequested();
			copiedMoves.Add(LocalPositionRules.NormalizeMove(move));
		}
		string[] moves = copiedMoves.ToArray();
		var positions = new Fen[moves.Length + 1];
		var results = new PlayableMatchResult[positions.Length];
		var classifications = new MoveClassification[moves.Length];
		var repetitions = new Dictionary<string, int>(StringComparer.Ordinal);
		positions[0] = initialFen;
		var legalMoves = LocalPositionRules.GetLegalMoves(initialFen);
		for (var index = 0; index < positions.Length; index++)
		{
			ct.ThrowIfCancellationRequested();
			string key = LocalPositionRules.BuildRepetitionKey(positions[index]);
			repetitions.TryGetValue(key, out int count);
			repetitions[key] = ++count;
			results[index] = GetTerminalResult(positions[index], legalMoves, count);
			if (index == moves.Length)
				break;
			if (results[index].IsTerminal)
				throw new ArgumentException("The supplied game continues after an automatic terminal result.", nameof(playedMoves));
			positions[index + 1] = LocalPositionRules.ApplyAndClassifyMove(positions[index], moves[index],
				out classifications[index], out legalMoves);
		}

		await _gate.WaitAsync(ct).ConfigureAwait(false);
		try
		{
			bool configured = false;
			var evaluations = ImmutableArray.CreateBuilder<GameReviewEvaluation>(positions.Length);
			for (var index = 0; index < positions.Length; index++)
			{
				ct.ThrowIfCancellationRequested();
				if (results[index].IsTerminal)
				{
					var score = results[index].IsDraw ? new PositionScore(0, null) : new PositionScore(null, 0);
					evaluations.Add(new(positions[index], score, string.Empty, null, results[index]));
				}
				else
				{
					if (!configured)
					{
						await ConfigureEngineAsync(ct).ConfigureAwait(false);
						configured = true;
					}
					await _client.SetPositionAsync(initialFen, new ArraySegment<string>(moves, 0, index), ct)
						.ConfigureAwait(false);
					var search = await _client.GoAsync(_options.Search, ct).ConfigureAwait(false);
					var variation = search.BestPv ?? throw new InvalidOperationException("The engine returned no primary variation for review.");
					if (variation.ScoreBound != UciScoreBound.Exact ||
						!variation.ScoreCp.HasValue && !variation.ScoreMate.HasValue || variation.ScoreMate == 0)
						throw new InvalidOperationException("Review requires an exact primary-variation score.");
					if (variation.Moves.IsDefaultOrEmpty ||
						!string.Equals(variation.Moves[0], search.BestMove, StringComparison.OrdinalIgnoreCase))
						throw new InvalidOperationException("The engine's best move does not match its primary variation.");
					// Validate engine output without trusting a protocol-valid coordinate as a legal chess move.
					LocalPositionRules.ApplyMove(positions[index], search.BestMove);
					evaluations.Add(new(positions[index], new PositionScore(
						variation.ScoreMate.HasValue ? null : variation.ScoreCp, variation.ScoreMate),
						search.BestMove.ToLowerInvariant(), variation, default));
				}
				progress?.Report(new(index + 1, positions.Length));
			}

			ct.ThrowIfCancellationRequested();
			var reviewed = ImmutableArray.CreateBuilder<ReviewedMove>(moves.Length);
			double whiteLoss = 0, blackLoss = 0;
			int whiteCount = 0, blackCount = 0;
			for (var index = 0; index < moves.Length; index++)
			{
				ct.ThrowIfCancellationRequested();
				var before = evaluations[index];
				var after = evaluations[index + 1];
				char side = before.Position.ActiveColor;
				int? rating = side == 'w' ? _options.WhiteRating : _options.BlackRating;
				double beforePoints = GetExpectedPoints(before, side, rating);
				double afterPoints = GetExpectedPoints(after, side, rating);
				var scoreAfter = InvertScore(after.Score);
				bool preferred = string.Equals(moves[index], before.BestMove, StringComparison.Ordinal);
				long? cpLoss = before.Score.Cp is int beforeCp && scoreAfter.Cp is int afterCp
					? preferred ? 0 : Math.Max(0L, (long)beforeCp - afterCp) : null;
				// Keep decimal probability thresholds stable when subtracting binary floating-point values.
				double loss = preferred ? 0 : Math.Round(Math.Max(0, beforePoints - afterPoints), 12);
				var move = new ReviewedMove(index + 1, side, moves[index], before, after,
					before.Score, scoreAfter, cpLoss, beforePoints, afterPoints, loss,
					MoveQualityClassifier.Classify(loss), classifications[index]);
				if (_annotationPolicy is not null)
					move = move with { Annotation = _annotationPolicy.Annotate(move, rating) };
				reviewed.Add(move);
				if (side == 'w') { whiteLoss += loss; whiteCount++; }
				else { blackLoss += loss; blackCount++; }
			}

			return new(initialFen, _options, _model.Name, evaluations.MoveToImmutable(), reviewed.MoveToImmutable(),
				whiteCount == 0 ? null : 100 * (1 - whiteLoss / whiteCount),
				blackCount == 0 ? null : 100 * (1 - blackLoss / blackCount));
		}
		finally
		{
			_gate.Release();
		}
	}

	private static void ValidateInitialPosition(Fen fen)
	{
		var state = LocalPositionRules.Parse(fen);
		var counts = new int[2];
		var pawns = new int[2];
		int whiteKing = -1, blackKing = -1;
		for (var square = 0; square < state.Board.Length; square++)
		{
			char piece = state.Board[square];
			if (piece == '\0') continue;
			int side = char.IsUpper(piece) ? 0 : 1;
			counts[side]++;
			if (char.ToLowerInvariant(piece) == 'p')
			{
				if (LocalPositionRules.GetRank(square) is 0 or 7)
					throw new ArgumentException("Pawns cannot occupy the first or eighth rank in a playable position.", nameof(fen));
				pawns[side]++;
			}
			if (piece == 'K')
			{
				if (whiteKing >= 0) throw new ArgumentException("Exactly one king per side is required.", nameof(fen));
				whiteKing = square;
			}
			if (piece == 'k')
			{
				if (blackKing >= 0) throw new ArgumentException("Exactly one king per side is required.", nameof(fen));
				blackKing = square;
			}
		}
		if (whiteKing < 0 || blackKing < 0 || counts[0] > 16 || counts[1] > 16 || pawns[0] > 8 || pawns[1] > 8)
			throw new ArgumentException("A playable position requires two kings and at most sixteen pieces and eight pawns per side.", nameof(fen));

		foreach (char right in state.CastlingRights)
		{
			int kingSquare = char.IsUpper(right) ? 4 : 60;
			int rookSquare = right switch { 'K' => 7, 'Q' => 0, 'k' => 63, _ => 56 };
			if (state.Board[kingSquare] != (char.IsUpper(right) ? 'K' : 'k') ||
				state.Board[rookSquare] != (char.IsUpper(right) ? 'R' : 'r'))
				throw new ArgumentException("Castling rights require the king and rook on their original squares.", nameof(fen));
		}
		if (state.EnPassantTarget.Length > 0)
		{
			int target = LocalPositionRules.ParseSquare(state.EnPassantTarget.AsSpan());
			int direction = state.ActiveColor == 'w' ? 8 : -8;
			if (LocalPositionRules.GetRank(target) != (state.ActiveColor == 'w' ? 5 : 2) ||
				state.Board[target] != '\0' ||
				state.Board[target - direction] != (state.ActiveColor == 'w' ? 'p' : 'P') ||
				state.Board[target + direction] != '\0' || state.HalfmoveClock != 0)
				throw new ArgumentException("The en-passant target must describe the preceding double pawn push.", nameof(fen));
		}

		var inactiveSide = Fen.Parse($"{fen.PiecePlacement} {LocalPositionRules.Opposite(fen.ActiveColor)} - - 0 1")!.Value;
		if (LocalPositionRules.IsCurrentPlayerInCheck(inactiveSide))
			throw new ArgumentException("The side that is not moving cannot already be in check. Kings must not be adjacent.", nameof(fen));
	}

	private async Task ConfigureEngineAsync(CancellationToken ct)
	{
		if (_client.TryGetOption("UCI_LimitStrength", out var strength))
			await _client.SetOptionAsync(strength.Name, "false", ct).ConfigureAwait(false);
		if (_client.TryGetOption("Skill Level", out var skill) && skill.Max is int maxSkill)
			await _client.SetOptionAsync(skill.Name, maxSkill.ToString(CultureInfo.InvariantCulture), ct).ConfigureAwait(false);
		if (_client.TryGetOption("MultiPV", out var multiPv))
			await _client.SetOptionAsync(multiPv.Name, "1", ct).ConfigureAwait(false);
		if (_client.TryGetOption("UCI_ShowWDL", out var wdl))
			await _client.SetOptionAsync(wdl.Name, "true", ct).ConfigureAwait(false);
		await _client.UciNewGameAsync(ct).ConfigureAwait(false);
	}

	private double GetExpectedPoints(GameReviewEvaluation evaluation, char side, int? rating)
	{
		if (evaluation.IsTerminal)
			return evaluation.Result.IsDraw ? 0.5 : evaluation.Result.Winner == side ? 1 : 0;
		double points = _model.GetExpectedPoints(evaluation, side, rating);
		if (double.IsNaN(points) || points < 0 || points > 1)
			throw new InvalidOperationException("The expected-points model returned a value outside [0,1].");
		return points;
	}

	private static PositionScore InvertScore(PositionScore score) =>
		new(score.Cp is int cp ? checked(-cp) : null, score.Mate is int mate ? checked(-mate) : null);

	private static PlayableMatchResult GetTerminalResult(Fen fen, ImmutableArray<string> legalMoves, int repetitions)
	{
		if (legalMoves.IsEmpty)
			return LocalPositionRules.IsCurrentPlayerInCheck(fen)
				? new(PlayableMatchResultReason.Checkmate, LocalPositionRules.Opposite(fen.ActiveColor))
				: new(PlayableMatchResultReason.Stalemate, null);
		if (LocalPositionRules.HasInsufficientMaterial(fen))
			return new(PlayableMatchResultReason.InsufficientMaterial, null);
		if (fen.HalfmoveClock >= 150)
			return new(PlayableMatchResultReason.SeventyFiveMoveRule, null);
		return repetitions >= 5 ? new(PlayableMatchResultReason.FivefoldRepetition, null) : default;
	}
}
