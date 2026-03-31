using System.Collections.Immutable;
using Bezoro.Chess.UCI.Protocol.API;
using Bezoro.Chess.UCI.Protocol.API.Common.Extensions;
using Bezoro.Chess.UCI.Protocol.API.Types;

namespace Bezoro.Chess.UCI.Protocol.ConsoleDemo;

internal static class PlayableChessConsoleDemo
{
	private const int ADVANTAGE_BAR_HEIGHT       = 8;
	private const int ADVANTAGE_BAR_WIDTH        = 3;
	private const int ENGINE_MOVE_TIME_MS        = 1_000;
	private const int MOVE_LIST_ANALYSIS_TIME_MS = 3_000;
	private const int MOVE_LIST_FALLBACK_TIME_MS = 250;

	public static async Task<int> RunAsync(string[] args)
	{
		Console.Title = "Bezoro Chess UCI Protocol Console Demo";
		Console.WriteLine("Bezoro Chess UCI Protocol Console Demo");
		Console.WriteLine("--------------------------------------");
		Console.WriteLine("Play against a UCI engine using UCI move notation such as e2e4 or a7a8q.");
		Console.WriteLine("Type 'moves' to list legal moves, 'history' to show move eval history, or 'quit' to exit.");
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
		UciPositionAnalysisCoordinator positionAnalysis,
		string                     positionKey)
	{
		var analysis = await positionAnalysis.GetAnalysisAsync(positionKey);
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
		var playedMoves       = new List<string>();
		var moveHistory      = new List<MoveHistoryEntry>();
		var positionAnalysis = new UciPositionAnalysisCoordinator(
			moveListClient,
			MOVE_LIST_ANALYSIS_TIME_MS,
			MOVE_LIST_FALLBACK_TIME_MS
		);

		while (true)
		{
			var snapshot = await LoadSnapshotAsync(playingClient, analysisClient, playedMoves);
			var fen = snapshot.Fen;
			var legalMoves = snapshot.LegalMoves;
			string positionKey = fen.Raw;
			UpdateMoveHistory(moveHistory, playedMoves, positionKey);
			if (legalMoves.Length > 0)
				positionAnalysis.Enqueue(positionKey, playedMoves, fen.ActiveColor, playerColor, legalMoves);

			if (legalMoves.Length == 0)
			{
				PrintBoard(
					fen,
					playerColor,
					legalMoves.Length,
					ResolveAdvantage(positionAnalysis, moveHistory, positionKey, legalMoves.Length)
				);
				positionAnalysis.Cancel();
				PrintGameOver(fen, playerColor);
				return;
			}

			if (fen.ActiveColor == playerColor)
			{
				string move = await PromptHumanMoveAsync(
					positionAnalysis,
					moveHistory,
					positionKey,
					fen,
					playerColor,
					legalMoves
				);

				if (move == "quit")
				{
					positionAnalysis.Cancel();
					Console.WriteLine("Game aborted by user.");
					return;
				}

				playedMoves.Add(move);
				continue;
			}

			PrintBoard(
				fen,
				playerColor,
				legalMoves.Length,
				ResolveAdvantage(positionAnalysis, moveHistory, positionKey, legalMoves.Length)
			);
			Console.WriteLine("Engine is thinking...");
			var result = await playingClient.GoAsync(
							 new() { MoveTimeMs = ENGINE_MOVE_TIME_MS }, CancellationToken.None
						 );

			string engineMove = result.BestMove.ToLowerInvariant();

			if (!legalMoves.ContainsUciMove(engineMove))
				throw new InvalidOperationException(
					$"Engine produced '{result.BestMove}', which is not legal in the current position."
				);

			Console.WriteLine(
				$"Engine plays {engineMove}{result.ToPlayerDisplayString(GetOpponentColor(playerColor), playerColor)}"
			);
			playedMoves.Add(engineMove);
		}
	}

	private static async Task<PositionSnapshot> LoadSnapshotAsync(
		UciEngineClient       playingClient,
		UciEngineClient       analysisClient,
		IReadOnlyList<string> moves)
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

		return new(fen.Value, legalMoves);
	}

	private static async Task<string> PromptHumanMoveAsync(
		UciPositionAnalysisCoordinator positionAnalysis,
		IReadOnlyList<MoveHistoryEntry> moveHistory,
		string                     positionKey,
		Fen                        fen,
		char                       playerColor,
		ImmutableArray<string>     legalMoves)
	{
		if (Console.IsInputRedirected || Console.IsOutputRedirected)
		{
			return await PromptHumanMoveRedirectedAsync(
				positionAnalysis,
				moveHistory,
				positionKey,
				fen,
				playerColor,
				legalMoves
			);
		}

		return await PromptHumanMoveInteractiveAsync(
			positionAnalysis,
			moveHistory,
			positionKey,
			fen,
			playerColor,
			legalMoves
		);
	}

	private static async Task<string> PromptHumanMoveRedirectedAsync(
		UciPositionAnalysisCoordinator positionAnalysis,
		IReadOnlyList<MoveHistoryEntry> moveHistory,
		string                         positionKey,
		Fen                            fen,
		char                           playerColor,
		ImmutableArray<string>         legalMoves)
	{
		while (true)
		{
			PrintBoard(
				fen,
				playerColor,
				legalMoves.Length,
				ResolveAdvantage(positionAnalysis, moveHistory, positionKey, legalMoves.Length)
			);

			Console.Write("Your move: ");
			string move = NormalizeInput(ReadRequiredLine()).ToLowerInvariant();

			if (move is "quit" or "exit")
				return "quit";

			if (move == "moves")
			{
				await PrintLegalMovesAsync(positionAnalysis, positionKey);
				continue;
			}

			if (move == "history")
			{
				PrintMoveHistory(positionAnalysis, moveHistory);
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
				await PrintLegalMovesAsync(positionAnalysis, positionKey);
				continue;
			}

			return move;
		}
	}

	private static async Task<string> PromptHumanMoveInteractiveAsync(
		UciPositionAnalysisCoordinator positionAnalysis,
		IReadOnlyList<MoveHistoryEntry> moveHistory,
		string                         positionKey,
		Fen                            fen,
		char                           playerColor,
		ImmutableArray<string>         legalMoves)
	{
		while (true)
		{
			string move = NormalizeInput(
				await ReadInteractiveInputAsync(
					() => ResolveAdvantage(positionAnalysis, moveHistory, positionKey, legalMoves.Length),
					advantage => BuildPromptFrame(fen, playerColor, legalMoves.Length, advantage)
				)
			).ToLowerInvariant();

			if (move is "quit" or "exit")
				return "quit";

			if (move == "moves")
			{
				await PrintLegalMovesAsync(positionAnalysis, positionKey);
				continue;
			}

			if (move == "history")
			{
				PrintMoveHistory(positionAnalysis, moveHistory);
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
				await PrintLegalMovesAsync(positionAnalysis, positionKey);
				continue;
			}

			return move;
		}
	}

	private static async Task<string> ReadInteractiveInputAsync(
		Func<PositionAdvantage>            getAdvantage,
		Func<PositionAdvantage, string[]> buildFrame)
	{
		var currentInput = string.Empty;
		var lastAdvantage = getAdvantage();
		var frame = buildFrame(lastAdvantage);

		if (!ConsolePromptLayout.CanRenderInPlace(frame.Length, Console.BufferHeight))
		{
			foreach (string line in frame[..^1])
				Console.WriteLine(line);

			Console.Write(frame[^1]);
			return ReadRequiredLine();
		}

		var frameSize = WriteFreshFrame(frame);
		var topRow = frameSize.topRow;

		while (true)
		{
			while (Console.KeyAvailable)
			{
				var key = Console.ReadKey(intercept: true);

				switch (key.Key)
				{
					case ConsoleKey.Enter:
						Console.WriteLine();
						return currentInput;
					case ConsoleKey.Backspace when currentInput.Length > 0:
						currentInput = currentInput[..^1];
						var backspaceFrame = RenderFrame(topRow, frame, currentInput, frameSize.width, frameSize.height);
						frameSize = (topRow, backspaceFrame.width, backspaceFrame.height);
						break;
					default:
						if (!char.IsControl(key.KeyChar))
						{
							currentInput += key.KeyChar;
							var typedFrame = RenderFrame(topRow, frame, currentInput, frameSize.width, frameSize.height);
							frameSize = (topRow, typedFrame.width, typedFrame.height);
						}

						break;
				}
			}

			var latestAdvantage = getAdvantage();
			if (latestAdvantage != lastAdvantage)
			{
				lastAdvantage = latestAdvantage;
				frame = buildFrame(latestAdvantage);
				var updatedFrame = RenderFrame(topRow, frame, currentInput, frameSize.width, frameSize.height);
				frameSize = (topRow, updatedFrame.width, updatedFrame.height);
			}

			await Task.Delay(100);
		}
	}

	private static string[] BuildPromptFrame(
		Fen               fen,
		char              playerColor,
		int               legalMoveCount,
		PositionAdvantage advantage)
	{
		string[] boardLines = BuildBoardLines(fen, playerColor, legalMoveCount, advantage);
		return [.. boardLines, "Your move: "];
	}

	private static string[] BuildBoardLines(
		Fen               fen,
		char              playerColor,
		int               legalMoveCount,
		PositionAdvantage advantage)
	{
		string[] boardLines     = fen.ToDisplayLines(playerColor, legalMoveCount);
		string[] advantageLines = advantage.ToDisplayBarLines(ADVANTAGE_BAR_HEIGHT, ADVANTAGE_BAR_WIDTH);
		int      lineCount      = Math.Max(boardLines.Length, advantageLines.Length);
		int      boardWidth     = boardLines.Length == 0 ? 0 : boardLines.Max(static line => line.Length);
		var      renderedLines  = new string[lineCount + 1];

		renderedLines[0] = string.Empty;
		for (var i = 0; i < lineCount; i++)
		{
			string boardLine     = i < boardLines.Length ? boardLines[i] : string.Empty;
			string advantageLine = i < advantageLines.Length ? advantageLines[i] : string.Empty;
			renderedLines[i + 1] = $"{boardLine.PadRight(boardWidth)}   {advantageLine}";
		}

		return renderedLines;
	}

	private static (int width, int height) RenderFrame(
		int      topRow,
		string[] frame,
		string   currentInput,
		int      previousWidth  = 0,
		int      previousHeight = 0)
	{
		const string promptPrefix = "Your move: ";
		int width = Math.Max(
			previousWidth,
			frame.Select(static line => line.Length).DefaultIfEmpty().Max() + currentInput.Length
		);
		int height = Math.Max(previousHeight, frame.Length);
		topRow = ConsolePromptLayout.GetSafeTopRow(topRow, Console.BufferHeight, height);

		Console.SetCursorPosition(0, topRow);

		for (var i = 0; i < height; i++)
		{
			string line = i < frame.Length ? frame[i] : string.Empty;
			if (i == frame.Length - 1 && i < frame.Length)
				line += currentInput;

			Console.Write(line.PadRight(width));
			if (i < height - 1)
				Console.WriteLine();
		}

		Console.SetCursorPosition(promptPrefix.Length + currentInput.Length, topRow + frame.Length - 1);
		return (width, height);
	}

	private static (int topRow, int width, int height) WriteFreshFrame(string[] frame)
	{
		int width = frame.Select(static line => line.Length).DefaultIfEmpty().Max();

		for (var i = 0; i < frame.Length; i++)
		{
			if (i == frame.Length - 1)
			{
				Console.Write(frame[i]);
			}
			else
			{
				Console.WriteLine(frame[i].PadRight(width));
			}
		}

		int topRow = ConsolePromptLayout.GetTopRowFromBottomRow(Console.CursorTop, Console.BufferHeight, frame.Length);
		return (topRow, width, frame.Length);
	}

	private static void PrintBoard(
		Fen               fen,
		char              playerColor,
		int               legalMoveCount,
		PositionAdvantage advantage)
	{
		foreach (string line in BuildBoardLines(fen, playerColor, legalMoveCount, advantage))
			Console.WriteLine(line);
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

	private static char GetOpponentColor(char playerColor) => playerColor == 'w' ? 'b' : 'w';

	private static void PrintMoveHistory(
		UciPositionAnalysisCoordinator positionAnalysis,
		IReadOnlyList<MoveHistoryEntry> moveHistory)
	{
		string[] lines = MoveHistoryFormatter.BuildLines(
			moveHistory,
			entry => PlayedPositionEvaluationResolver.TryResolveScore(
				entry,
				positionKey => positionAnalysis.TryGetAnalysis(positionKey, out var analysis) ? analysis : null,
				out var score
			)
				? score
				: null
		);

		foreach (string line in lines)
			Console.WriteLine(line);
	}

	private static void UpdateMoveHistory(
		List<MoveHistoryEntry>  moveHistory,
		IReadOnlyList<string>   playedMoves,
		string                  currentPositionKey)
	{
		if (moveHistory.Count == playedMoves.Count)
			return;

		if (moveHistory.Count != playedMoves.Count - 1)
			throw new InvalidOperationException("Move history can only be extended by one move per position snapshot.");

		int moveIndex = moveHistory.Count;
		moveHistory.Add(
			new(
				(moveIndex / 2) + 1,
				moveIndex % 2 == 0 ? 'w' : 'b',
				playedMoves[moveIndex],
				moveIndex == 0 ? Fen.Default.Raw : moveHistory[^1].PositionKey,
				currentPositionKey
			)
		);
	}

	private static PositionAdvantage ResolveAdvantage(
		UciPositionAnalysisCoordinator positionAnalysis,
		IReadOnlyList<MoveHistoryEntry> moveHistory,
		string                         positionKey,
		int                            legalMoveCount)
	{
		return PlayedPositionEvaluationResolver.ResolveCurrentAdvantage(
			positionKey,
			moveHistory,
			legalMoveCount,
			key => positionAnalysis.TryGetAnalysis(key, out var analysis) ? analysis : null
		);
	}

	private readonly record struct PositionSnapshot(
		Fen                    Fen,
		ImmutableArray<string> LegalMoves
	);
}
