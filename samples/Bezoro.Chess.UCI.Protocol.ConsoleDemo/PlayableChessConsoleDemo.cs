using System.Collections.Immutable;
using Bezoro.Chess.UCI.Protocol.API;
using Bezoro.Chess.UCI.Protocol.API.Common.Extensions;
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

	private static async Task PrintLegalMovesAsync(
		UciMoveAnalysisCoordinator moveListAnalysis,
		string                     positionKey)
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
		int baselineCp       = await analysisClient.EvaluateBaselineCpAsync(playerColor, ADVANTAGE_EVAL_TIME_MS, CancellationToken.None);
		var moveListAnalysis = new UciMoveAnalysisCoordinator(
			moveListClient,
			MOVE_LIST_ANALYSIS_TIME_MS,
			MOVE_LIST_FALLBACK_TIME_MS
		);

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

			if (!legalMoves.ContainsUciMove(engineMove))
				throw new InvalidOperationException(
					$"Engine produced '{result.BestMove}', which is not legal in the current position."
				);

			Console.WriteLine($"Engine plays {engineMove}{result.ToDisplayString()}");
			playedMoves.Add(engineMove);
		}
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

		var legalMoves = (await analysisClient.GetLegalMovesViaPerftAsync(CancellationToken.None)).NormalizeUciMoves();
		if (moves.Count == 0 && legalMoves.Length == 0)
			throw new NotSupportedException(
				"This sample requires an engine that supports legal-move listing via the non-standard 'go perft 1' command."
			);

		var advantage = legalMoves.Length == 0
							? PositionAdvantage.GameOver()
							: moves.Count == 0
								? PositionAdvantage.GameStart()
								: await analysisClient.EvaluateAdvantageAsync(
									  fen.Value.ActiveColor,
									  playerColor,
									  baselineCp,
									  ADVANTAGE_EVAL_TIME_MS,
									  CancellationToken.None
								  );

		return new(fen.Value, legalMoves, advantage);
	}

	private static async Task<string> PromptHumanMoveAsync(
		UciMoveAnalysisCoordinator moveListAnalysis,
		string                     positionKey,
		ImmutableArray<string>     legalMoves)
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

			if (!legalMoves.ContainsUciMove(move))
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

		string[] boardLines     = fen.ToDisplayLines(playerColor, legalMoveCount);
		string[] advantageLines = advantage.ToDisplayBarLines(ADVANTAGE_BAR_HEIGHT, ADVANTAGE_BAR_WIDTH);
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

	private readonly record struct PositionSnapshot(
		Fen                    Fen,
		ImmutableArray<string> LegalMoves,
		PositionAdvantage      Advantage
	);
}
