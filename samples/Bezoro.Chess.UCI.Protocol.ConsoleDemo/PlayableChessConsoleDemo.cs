using System.Collections.Immutable;
using System.Globalization;
using Bezoro.Chess.UCI.Protocol.API;
using Bezoro.Chess.UCI.Protocol.API.Types;

namespace Bezoro.Chess.UCI.Protocol.ConsoleDemo;

internal static class PlayableChessConsoleDemo
{
	private const int ADVANTAGE_BAR_HEIGHT       = 8;
	private const int ADVANTAGE_BAR_WIDTH        = 3;
	private const int ADVANTAGE_EVAL_TIME_MS     = 250;
	private const int ENGINE_MOVE_TIME_MS        = 1_000;
	private const int MOVE_LIST_ANALYSIS_TIME_MS = 3_000;
	private const int MOVE_LIST_FALLBACK_TIME_MS = 250;

	public static async Task<int> RunAsync(string[] args)
	{
		Console.Title = "Bezoro Chess UCI Protocol Console Demo";
		Console.WriteLine("Bezoro Chess UCI Protocol Console Demo");
		Console.WriteLine("--------------------------------------");
		Console.WriteLine("Play against a UCI engine using UCI move notation such as e2e4 or a7a8q.");
		Console.WriteLine("Type 'moves' to list legal moves. Type 'quit' to exit.");
		Console.WriteLine();

		string enginePath = PromptEnginePath(args);

		var options = new UciClientOptions
		{
			UciHandshakeTimeout             = TimeSpan.FromSeconds(5),
			ReadyTimeout                    = TimeSpan.FromSeconds(10),
			DisplayBoardFenTimeout          = TimeSpan.FromSeconds(3),
			DisplayBoardCheckersGracePeriod = TimeSpan.FromSeconds(1),
			DefaultSearchTimeout            = TimeSpan.FromSeconds(20),
			MoveTimeBuffer                  = TimeSpan.FromSeconds(1)
		};

		await using var playingClient  = new UciEngineClient(enginePath, options: options);
		await using var analysisClient = new UciEngineClient(enginePath, options: options);
		await using var moveListClient = new UciEngineClient(enginePath, options: options);

		playingClient.StderrReceived  += static line => Console.Error.WriteLine($"play stderr: {line}");
		analysisClient.StderrReceived += static line => Console.Error.WriteLine($"analysis stderr: {line}");
		moveListClient.StderrReceived += static line => Console.Error.WriteLine($"moves stderr: {line}");

		try
		{
			await playingClient.StartAsync(CancellationToken.None);
			await analysisClient.StartAsync(CancellationToken.None);
			await moveListClient.StartAsync(CancellationToken.None);
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"Failed to start engine: {ex.Message}");
			return 1;
		}

		Console.WriteLine($"{playingClient.EngineInfo.Name} by {playingClient.EngineInfo.Author}");
		Console.WriteLine($"Engine executable: {enginePath}");
		Console.WriteLine();

		if (playingClient.TryGetStrengthLimitRange(out int minElo, out int maxElo))
		{
			int elo = PromptElo(minElo, maxElo);
			await playingClient.SetStrengthLimitAsync(elo, CancellationToken.None);
			Console.WriteLine($"Engine strength limited to {elo} Elo.");
			Console.WriteLine("Analysis remains at full engine strength.");
		}
		else
		{
			Console.WriteLine("Engine does not advertise adjustable Elo strength. Using its default strength.");
		}

		char playerColor = PromptPlayerColor();
		Console.WriteLine($"You are playing {(playerColor == 'w' ? "White" : "Black")}.");
		Console.WriteLine($"Engine move time: {ENGINE_MOVE_TIME_MS} ms per move.");
		Console.WriteLine();

		try
		{
			await Task.WhenAll(
				playingClient.UciNewGameAsync(CancellationToken.None),
				analysisClient.UciNewGameAsync(CancellationToken.None),
				moveListClient.UciNewGameAsync(CancellationToken.None)
			);

			await RunGameLoopAsync(playingClient, analysisClient, moveListClient, playerColor);
			return 0;
		}
		catch (NotSupportedException ex)
		{
			Console.Error.WriteLine(ex.Message);
			return 1;
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"Game aborted due to an unexpected error: {ex.Message}");
			return 1;
		}
	}

	private static bool ContainsMove(ImmutableArray<string> legalMoves, string move)
	{
		foreach (string legalMove in legalMoves)
		{
			if (string.Equals(legalMove, move, StringComparison.Ordinal))
				return true;
		}

		return false;
	}

	private static bool TryGetMultiPvOption(UciEngineClient client, out UciEngineOption option)
	{
		if (client.TryGetOption("MultiPV", out option) && option.Type == UciOptionType.Spin)
			return true;

		option = default;
		return false;
	}

	private static char PromptPlayerColor()
	{
		while (true)
		{
			Console.Write("Play as white or black? [w/b]: ");
			string input = NormalizeInput(ReadRequiredLine()).ToLowerInvariant();

			if (input is "w" or "white") return 'w';
			if (input is "b" or "black") return 'b';

			Console.WriteLine("Please enter 'w', 'white', 'b', or 'black'.");
		}
	}

	private static ImmutableArray<string> NormalizeMoves(ImmutableArray<string> legalMoves)
	{
		if (legalMoves.IsDefaultOrEmpty) return ImmutableArray<string>.Empty;

		var builder = ImmutableArray.CreateBuilder<string>(legalMoves.Length);
		foreach (string move in legalMoves)
			builder.Add(move.ToLowerInvariant());

		return builder.ToImmutable();
	}

	private static int GetPlayerPerspectiveCp(int? rawCpScore, char sideToMove, char playerColor)
	{
		int perspective = sideToMove == playerColor ? 1 : -1;
		return (rawCpScore ?? 0) * perspective;
	}

	private static int PromptElo(int minElo, int maxElo)
	{
		while (true)
		{
			Console.Write($"Engine Elo ({minElo}-{maxElo}): ");
			string input = ReadRequiredLine();

			if (!int.TryParse(input, out int elo))
			{
				Console.WriteLine("Please enter a whole number.");
				continue;
			}

			if (elo < minElo || elo > maxElo)
			{
				Console.WriteLine($"Please choose an Elo between {minElo} and {maxElo}.");
				continue;
			}

			return elo;
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
		var moveScore = CreatePositionScore(rawCpScore, rawMateScore, sideToMove, playerColor, baselineCp);
		if (currentScore.Mate is int || moveScore.Mate is int)
			return new(move, moveScore.ToDisplayString(), moveScore.ToSortValue());

		int    deltaCp   = moveScore.Cp!.Value - currentScore.Cp!.Value;
		string sign      = deltaCp >= 0 ? "+" : string.Empty;
		var    displayCp = $"{sign}{deltaCp.ToString(CultureInfo.InvariantCulture)} cp";
		return new(move, displayCp, deltaCp);
	}

	private static PositionAdvantage BuildAdvantage(
		int? rawCpScore,
		int? rawMateScore,
		char sideToMove,
		char playerColor,
		int  baselineCp)
	{
		var score = CreatePositionScore(rawCpScore, rawMateScore, sideToMove, playerColor, baselineCp);

		if (score.Mate is int adjustedMate)
		{
			int plyToMate = Math.Abs(adjustedMate);
			double magnitude = plyToMate switch
			{
				<= 1 => 1.0,
				2    => 0.90,
				3    => 0.82,
				4    => 0.74,
				_    => 0.66
			};

			double mateNormalized = adjustedMate > 0 ? magnitude : -magnitude;
			string mateSummary = adjustedMate > 0
									 ? $"Advantage +{mateNormalized.ToString("0.00", CultureInfo.InvariantCulture)} | You mate in {plyToMate}"
									 : $"Advantage {mateNormalized.ToString("0.00", CultureInfo.InvariantCulture)} | Engine mate in {plyToMate}";

			return new(mateNormalized, mateSummary, score);
		}

		int    adjustedCp = score.Cp ?? 0;
		double normalized = AdvantageScale.NormalizeCp(adjustedCp);

		string summary = normalized switch
		{
			> 0 =>
				$"Advantage +{normalized.ToString("0.00", CultureInfo.InvariantCulture)} | You +{(adjustedCp / 100.0).ToString("0.0", CultureInfo.InvariantCulture)} pawns ({adjustedCp.ToString(CultureInfo.InvariantCulture)} cp)",
			< 0 =>
				$"Advantage {normalized.ToString("0.00", CultureInfo.InvariantCulture)} | Engine +{(Math.Abs(adjustedCp) / 100.0).ToString("0.0", CultureInfo.InvariantCulture)} pawns ({Math.Abs(adjustedCp).ToString(CultureInfo.InvariantCulture)} cp)",
			_ => "Advantage 0.00 | Even (0 cp)"
		};

		return new(normalized, summary, score);
	}

	private static PositionScore CreatePositionScore(
		int? rawCpScore,
		int? rawMateScore,
		char sideToMove,
		char playerColor,
		int  baselineCp)
	{
		int perspective = sideToMove == playerColor ? 1 : -1;

		if (rawMateScore is int mateScore)
			return new(null, mateScore * perspective);

		return new((rawCpScore ?? 0) * perspective - baselineCp, null);
	}

	private static string ExpandRank(string encodedRank)
	{
		var chars = new List<char>(8);

		foreach (char symbol in encodedRank)
		{
			if (char.IsDigit(symbol))
			{
				for (var i = 0; i < symbol - '0'; i++) chars.Add('.');
				continue;
			}

			chars.Add(symbol);
		}

		return new(chars.ToArray());
	}

	private static string FormatEngineLine(SearchResult result)
	{
		var parts = new List<string>();

		if (result.ReachedDepth > 0)
			parts.Add($"depth {result.ReachedDepth}");

		if (result.MateScore is int mateScore)
			parts.Add($"mate {mateScore}");
		else if (result.BestCpScore is int cpScore)
			parts.Add($"eval {cpScore} cp");

		var matchingVariation = result.GetVariationStartingWith(result.BestMove);
		if (matchingVariation is { } bestPv && !bestPv.Moves.IsDefaultOrEmpty)
			parts.Add($"pv {string.Join(' ', bestPv.Moves.Take(6))}");

		return parts.Count == 0 ? string.Empty : $" ({string.Join(", ", parts)})";
	}

	private static string NormalizeInput(string value)
	{
		string trimmed = value.Trim();
		return trimmed.Trim('"');
	}

	private static string PromptEnginePath(string[] args)
	{
		string? candidate = args.Length > 0 ? NormalizeInput(args[0]) : null;

		while (true)
		{
			if (!string.IsNullOrWhiteSpace(candidate))
			{
				string fullPath = Path.GetFullPath(candidate);
				if (File.Exists(fullPath)) return fullPath;

				Console.WriteLine($"Engine executable not found: {fullPath}");
			}

			Console.Write("Engine path: ");
			candidate = NormalizeInput(ReadRequiredLine());
		}
	}

	private static string ReadRequiredLine()
	{
		string? line = Console.ReadLine();
		return line?.Trim() ?? string.Empty;
	}

	private static string[] BuildAdvantageBarLines(PositionAdvantage advantage)
	{
		var lines = new List<string>(ADVANTAGE_BAR_HEIGHT + 4)
		{
			"Advantage",
			"  Engine",
			"  +" + new string('-', ADVANTAGE_BAR_WIDTH) + "+"
		};

		var playerRows = (int)Math.Round((advantage.Normalized + 1.0) / 2.0 * ADVANTAGE_BAR_HEIGHT);
		playerRows = Math.Clamp(playerRows, 0, ADVANTAGE_BAR_HEIGHT);
		int engineRows = ADVANTAGE_BAR_HEIGHT - playerRows;

		for (var row = 0; row < ADVANTAGE_BAR_HEIGHT; row++)
		{
			string fill = row < engineRows
							  ? new('=', ADVANTAGE_BAR_WIDTH)
							  : new string('#', ADVANTAGE_BAR_WIDTH);

			lines.Add($"  |{fill}|");
		}

		lines.Add("  +" + new string('-', ADVANTAGE_BAR_WIDTH) + "+");
		lines.Add("   You");
		lines.Add($"  {advantage.Summary}");

		return lines.ToArray();
	}

	private static string[] BuildBoardLines(Fen fen, char playerColor, int legalMoveCount)
	{
		var lines = new List<string>
		{
			$"Move {fen.FullmoveNumber} | {(fen.ActiveColor == 'w' ? "White" : "Black")} to move | {legalMoveCount} legal moves"
		};

		if (!string.IsNullOrWhiteSpace(fen.Checkers))
			lines.Add("Check.");

		string[] ranks = fen.PiecePlacement.Split('/');
		if (ranks.Length != 8)
		{
			lines.Add($"FEN: {fen.Raw}");
			return lines.ToArray();
		}

		bool whitePerspective = playerColor == 'w';
		lines.Add(whitePerspective ? "  a b c d e f g h" : "  h g f e d c b a");

		if (whitePerspective)
			for (var rankIndex = 0; rankIndex < ranks.Length; rankIndex++)
			{
				int    rankLabel    = 8 - rankIndex;
				string expandedRank = ExpandRank(ranks[rankIndex]);
				var    line         = $"{rankLabel} ";
				for (var fileIndex = 0; fileIndex < expandedRank.Length; fileIndex++)
					line += $"{expandedRank[fileIndex]} ";

				line += rankLabel;
				lines.Add(line);
			}
		else
			for (int rankIndex = ranks.Length - 1; rankIndex >= 0; rankIndex--)
			{
				int rankLabel    = 8 - rankIndex;
				var expandedRank = new string(ExpandRank(ranks[rankIndex]).Reverse().ToArray());

				var line = $"{rankLabel} ";
				for (var fileIndex = 0; fileIndex < expandedRank.Length; fileIndex++)
					line += $"{expandedRank[fileIndex]} ";

				line += rankLabel;
				lines.Add(line);
			}

		lines.Add(whitePerspective ? "  a b c d e f g h" : "  h g f e d c b a");
		return lines.ToArray();
	}

	private static async Task PrintLegalMovesAsync(
		MoveListAnalysisCoordinator moveListAnalysis,
		string                      positionKey)
	{
		var analysis = await moveListAnalysis.GetAnalysisAsync(positionKey);
		if (analysis.Evaluations.IsDefaultOrEmpty)
		{
			Console.WriteLine("No legal moves are available.");
			return;
		}

		Console.WriteLine("Showing cached legal move analysis...");

		foreach (var evaluation in analysis.Evaluations)
			Console.WriteLine($"  {evaluation.Move,-6} {evaluation.Display}");
	}

	private static async Task RunGameLoopAsync(
		UciEngineClient playingClient,
		UciEngineClient analysisClient,
		UciEngineClient moveListClient,
		char            playerColor)
	{
		var playedMoves      = new List<string>();
		int baselineCp       = await EvaluateBaselineCpAsync(playingClient, analysisClient, playerColor);
		var moveListAnalysis = new MoveListAnalysisCoordinator(moveListClient);

		while (true)
		{
			var snapshot = await LoadSnapshotAsync(playingClient, analysisClient, playerColor, playedMoves, baselineCp);
			var fen = snapshot.Fen;
			var legalMoves = snapshot.LegalMoves;
			string positionKey = fen.Raw;
			moveListAnalysis.EnsureStarted(
				positionKey,
				playedMoves,
				fen.ActiveColor,
				playerColor,
				legalMoves,
				baselineCp,
				snapshot.Advantage.Score
			);

			PrintBoard(fen, playerColor, legalMoves.Length, snapshot.Advantage);

			if (legalMoves.Length == 0)
			{
				moveListAnalysis.Cancel();
				PrintGameOver(fen, playerColor);
				return;
			}

			if (fen.ActiveColor == playerColor)
			{
				string move = await PromptHumanMoveAsync(moveListAnalysis, positionKey, legalMoves);

				if (move == "quit")
				{
					moveListAnalysis.Cancel();
					Console.WriteLine("Game aborted by user.");
					return;
				}

				moveListAnalysis.Cancel();
				playedMoves.Add(move);
				continue;
			}

			moveListAnalysis.Cancel();
			Console.WriteLine("Engine is thinking...");
			var result = await playingClient.GoAsync(
							 new() { MoveTimeMs = ENGINE_MOVE_TIME_MS }, CancellationToken.None
						 );

			string engineMove = result.BestMove.ToLowerInvariant();

			if (!ContainsMove(legalMoves, engineMove))
				throw new InvalidOperationException(
					$"Engine produced '{result.BestMove}', which is not legal in the current position."
				);

			Console.WriteLine($"Engine plays {engineMove}{FormatEngineLine(result)}");
			playedMoves.Add(engineMove);
		}
	}

	private static async Task<int> EvaluateBaselineCpAsync(
		UciEngineClient playingClient,
		UciEngineClient analysisClient,
		char            playerColor)
	{
		await Task.WhenAll(
			playingClient.SetPositionAsync(Fen.Default, [], CancellationToken.None),
			analysisClient.SetPositionAsync(Fen.Default, [], CancellationToken.None)
		);

		var evaluation = await analysisClient.GoAsync(
							 new() { MoveTimeMs = ADVANTAGE_EVAL_TIME_MS },
							 CancellationToken.None
						 );

		if (evaluation.MateScore is { })
			return 0;

		return GetPlayerPerspectiveCp(evaluation.BestCpScore, Fen.Default.ActiveColor, playerColor);
	}

	private static async Task<List<MoveEvaluation>> BuildMoveEvaluationsFromMultiPvAsync(
		UciEngineClient        client,
		SearchResult           result,
		char                   sideToMove,
		char                   playerColor,
		ImmutableArray<string> legalMoves,
		int                    baselineCp,
		PositionScore          currentScore,
		CancellationToken      ct)
	{
		var capturedVariations = new Dictionary<string, PrincipalVariation>(StringComparer.Ordinal);

		foreach (var variation in result.PrincipalVariations)
		{
			if (variation.Moves.IsDefaultOrEmpty)
				continue;

			string move = variation.Moves[0];
			if (!ContainsMove(legalMoves, move))
				continue;

			if (!capturedVariations.TryGetValue(move, out var existing) ||
				variation.Depth > existing.Depth ||
				variation.Depth == existing.Depth && variation.SelDepth >= existing.SelDepth)
				capturedVariations[move] = variation;
		}

		var evaluations = new List<MoveEvaluation>(legalMoves.Length);
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

			var fallback = await EvaluateSingleMoveAsync(
							   client, move, sideToMove, playerColor, baselineCp, currentScore, ct
						   );

			evaluations.Add(fallback);
		}

		evaluations.Sort(static (left, right) => right.SortValue.CompareTo(left.SortValue));
		return evaluations;
	}

	private static async Task<List<MoveEvaluation>> EvaluateMovesAsync(
		UciEngineClient        client,
		char                   sideToMove,
		char                   playerColor,
		ImmutableArray<string> legalMoves,
		int                    baselineCp,
		PositionScore          currentScore,
		CancellationToken      ct)
	{
		if (!TryGetMultiPvOption(client, out var multiPvOption))
			return await EvaluateMovesIndividuallyAsync(
					   client, sideToMove, playerColor, legalMoves, baselineCp, currentScore, ct
				   );

		int requestedMultiPv = legalMoves.Length;
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
		);

		try
		{
			var result = await client.GoAsync(
							 new()
							 {
								 MoveTimeMs = MOVE_LIST_ANALYSIS_TIME_MS
							 },
							 ct
						 );

			return await BuildMoveEvaluationsFromMultiPvAsync(
					   client,
					   result,
					   sideToMove,
					   playerColor,
					   legalMoves,
					   baselineCp,
					   currentScore,
					   ct
				   );
		}
		finally
		{
			await client.SetOptionAsync(multiPvOption.Name, restoreValue, CancellationToken.None);
		}
	}

	private static async Task<List<MoveEvaluation>> EvaluateMovesIndividuallyAsync(
		UciEngineClient        client,
		char                   sideToMove,
		char                   playerColor,
		ImmutableArray<string> legalMoves,
		int                    baselineCp,
		PositionScore          currentScore,
		CancellationToken      ct)
	{
		var evaluations = new List<MoveEvaluation>(legalMoves.Length);

		foreach (string move in legalMoves)
		{
			evaluations.Add(
				await EvaluateSingleMoveAsync(
					client, move, sideToMove, playerColor, baselineCp, currentScore, ct
				)
			);
		}

		evaluations.Sort(static (left, right) => right.SortValue.CompareTo(left.SortValue));
		return evaluations;
	}

	private static async Task<MoveEvaluation> EvaluateSingleMoveAsync(
		UciEngineClient   client,
		string            move,
		char              sideToMove,
		char              playerColor,
		int               baselineCp,
		PositionScore     currentScore,
		CancellationToken ct)
	{
		var result = await client.GoAsync(
						 new()
						 {
							 MoveTimeMs  = MOVE_LIST_FALLBACK_TIME_MS,
							 SearchMoves = [move]
						 },
						 ct
					 );

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

	private static async Task<PositionAdvantage> EvaluateAdvantageAsync(
		UciEngineClient client,
		char            sideToMove,
		char            playerColor,
		int             baselineCp)
	{
		var evaluation = await client.GoAsync(
							 new() { MoveTimeMs = ADVANTAGE_EVAL_TIME_MS },
							 CancellationToken.None
						 );

		return BuildAdvantage(evaluation.BestCpScore, evaluation.MateScore, sideToMove, playerColor, baselineCp);
	}

	private static async Task<PositionSnapshot> LoadSnapshotAsync(
		UciEngineClient       playingClient,
		UciEngineClient       analysisClient,
		char                  playerColor,
		IReadOnlyList<string> moves,
		int                   baselineCp)
	{
		await Task.WhenAll(
			playingClient.SetPositionAsync(Fen.Default, moves, CancellationToken.None),
			analysisClient.SetPositionAsync(Fen.Default, moves, CancellationToken.None)
		);

		var fen = await analysisClient.TryGetFenViaDisplayBoardAsync(CancellationToken.None);
		if (!fen.HasValue)
			throw new NotSupportedException(
				"This sample requires an engine that supports the non-standard 'd' command and returns a FEN line."
			);

		var legalMoves = NormalizeMoves(await analysisClient.GetLegalMovesViaPerftAsync(CancellationToken.None));
		if (moves.Count == 0 && legalMoves.Length == 0)
			throw new NotSupportedException(
				"This sample requires an engine that supports legal-move listing via the non-standard 'go perft 1' command."
			);

		var advantage = legalMoves.Length == 0
							? PositionAdvantage.GameOver()
							: moves.Count == 0
								? PositionAdvantage.GameStart()
								: await EvaluateAdvantageAsync(
									  analysisClient, fen.Value.ActiveColor, playerColor, baselineCp
								  );

		return new(fen.Value, legalMoves, advantage);
	}

	private static async Task<string> PromptHumanMoveAsync(
		MoveListAnalysisCoordinator moveListAnalysis,
		string                      positionKey,
		ImmutableArray<string>      legalMoves)
	{
		while (true)
		{
			Console.Write("Your move: ");
			string move = NormalizeInput(ReadRequiredLine()).ToLowerInvariant();

			if (move is "quit" or "exit")
				return "quit";

			if (move == "moves")
			{
				await PrintLegalMovesAsync(moveListAnalysis, positionKey);

				continue;
			}

			if (!UciEngineClient.IsUciMoveString(move))
			{
				Console.WriteLine("Enter a move in UCI notation such as e2e4 or a7a8q.");
				continue;
			}

			if (!ContainsMove(legalMoves, move))
			{
				Console.WriteLine("That move is not legal in the current position.");
				await PrintLegalMovesAsync(moveListAnalysis, positionKey);

				continue;
			}

			return move;
		}
	}

	private static void PrintBoard(
		Fen               fen,
		char              playerColor,
		int               legalMoveCount,
		PositionAdvantage advantage)
	{
		Console.WriteLine();

		string[] boardLines     = BuildBoardLines(fen, playerColor, legalMoveCount);
		string[] advantageLines = BuildAdvantageBarLines(advantage);
		int      lineCount      = Math.Max(boardLines.Length, advantageLines.Length);
		int      boardWidth     = boardLines.Length == 0 ? 0 : boardLines.Max(static line => line.Length);

		for (var i = 0; i < lineCount; i++)
		{
			string boardLine     = i < boardLines.Length ? boardLines[i] : string.Empty;
			string advantageLine = i < advantageLines.Length ? advantageLines[i] : string.Empty;
			Console.WriteLine($"{boardLine.PadRight(boardWidth)}   {advantageLine}");
		}
	}

	private static void PrintGameOver(Fen fen, char playerColor)
	{
		Console.WriteLine();

		if (!string.IsNullOrWhiteSpace(fen.Checkers))
		{
			bool playerLost = fen.ActiveColor == playerColor;
			Console.WriteLine(playerLost ? "Checkmate. The engine wins." : "Checkmate. You win.");
			return;
		}

		Console.WriteLine("Stalemate.");
	}

	private readonly record struct MoveEvaluation(string Move, string Display, double SortValue);

	private sealed class MoveListAnalysisCoordinator(UciEngineClient client)
	{
		private readonly object                        _sync = new();
		private          CancellationTokenSource?      _cts;
		private          MoveListAnalysisResult?       _cachedResult;
		private          string?                       _positionKey;
		private          Task<MoveListAnalysisResult>? _runningTask;

		public async Task<MoveListAnalysisResult> GetAnalysisAsync(string positionKey)
		{
			Task<MoveListAnalysisResult>? runningTask;
			MoveListAnalysisResult?       cachedResult;

			lock (_sync)
			{
				if (!string.Equals(_positionKey, positionKey, StringComparison.Ordinal))
					throw new InvalidOperationException("Move analysis is not available for the current position.");

				cachedResult = _cachedResult;
				runningTask  = _runningTask;
			}

			if (cachedResult.HasValue)
				return cachedResult.Value;

			if (runningTask is null)
				return MoveListAnalysisResult.Empty;

			return await runningTask;
		}

		public void Cancel()
		{
			CancellationTokenSource? ctsToCancel;
			lock (_sync)
			{
				ctsToCancel   = _cts;
				_cts          = null;
				_positionKey  = null;
				_runningTask  = null;
				_cachedResult = null;
			}

			if (ctsToCancel is null)
				return;

			try
			{
				ctsToCancel.Cancel();
			}
			finally
			{
				ctsToCancel.Dispose();
			}
		}

		public void EnsureStarted(
			string                 positionKey,
			IReadOnlyList<string>  moves,
			char                   sideToMove,
			char                   playerColor,
			ImmutableArray<string> legalMoves,
			int                    baselineCp,
			PositionScore          currentScore)
		{
			CancellationTokenSource? ctsToCancel = null;

			lock (_sync)
			{
				if (string.Equals(_positionKey, positionKey, StringComparison.Ordinal) &&
					(_cachedResult.HasValue || _runningTask is { }))
					return;

				ctsToCancel   = _cts;
				_cts          = new();
				_positionKey  = positionKey;
				_cachedResult = null;

				ImmutableArray<string> movesCopy = [.. moves];
				var                    token     = _cts.Token;
				_runningTask = AnalyzeAndCacheAsync(
					positionKey,
					movesCopy,
					sideToMove,
					playerColor,
					legalMoves,
					baselineCp,
					currentScore,
					token
				);
			}

			if (ctsToCancel is null)
				return;

			try
			{
				ctsToCancel.Cancel();
			}
			finally
			{
				ctsToCancel.Dispose();
			}
		}

		private async Task<MoveListAnalysisResult> AnalyzeAndCacheAsync(
			string                 positionKey,
			ImmutableArray<string> moves,
			char                   sideToMove,
			char                   playerColor,
			ImmutableArray<string> legalMoves,
			int                    baselineCp,
			PositionScore          currentScore,
			CancellationToken      ct)
		{
			try
			{
				await client.SetPositionAsync(Fen.Default, moves, ct);
				var evaluations = await EvaluateMovesAsync(
									  client,
									  sideToMove,
									  playerColor,
									  legalMoves,
									  baselineCp,
									  currentScore,
									  ct
								  );

				var result = new MoveListAnalysisResult([.. evaluations]);
				lock (_sync)
				{
					if (string.Equals(_positionKey, positionKey, StringComparison.Ordinal) &&
						_cts is { IsCancellationRequested: false })
					{
						_cachedResult = result;
						_runningTask  = null;
					}
				}

				return result;
			}
			catch (OperationCanceledException) when (ct.IsCancellationRequested)
			{
				return MoveListAnalysisResult.Empty;
			}
		}
	}

	private readonly record struct MoveListAnalysisResult(ImmutableArray<MoveEvaluation> Evaluations)
	{
		public static MoveListAnalysisResult Empty => new([]);
	}

	private readonly record struct PositionAdvantage(double Normalized, string Summary, PositionScore Score)
	{
		public static PositionAdvantage GameOver()  => new(0, "Game over", new(0, null));
		public static PositionAdvantage GameStart() => new(0, "Advantage 0.00 | Even (0 cp)", new(0, null));
	}

	private readonly record struct PositionScore(int? Cp, int? Mate)
	{
		public double ToSortValue()
		{
			if (Mate is int mate)
				return mate > 0 ? 100_000 - Math.Abs(mate) : -100_000 + Math.Abs(mate);

			return Cp ?? 0;
		}

		public string ToDisplayString()
		{
			if (Mate is int mate)
				return mate > 0 ? $"+M{Math.Abs(mate)}" : $"-M{Math.Abs(mate)}";

			int cp = Cp ?? 0;
			return $"{(cp >= 0 ? "+" : string.Empty)}{cp.ToString(CultureInfo.InvariantCulture)} cp";
		}
	}

	private readonly record struct PositionSnapshot(
		Fen                    Fen,
		ImmutableArray<string> LegalMoves,
		PositionAdvantage      Advantage
	);
}
