using System.Collections.Immutable;
using Bezoro.Chess.UCI.Protocol.API;
using Bezoro.Chess.UCI.Protocol.API.Types;

namespace Bezoro.Chess.UCI.Protocol.ConsoleDemo;

internal static class PlayableChessConsoleDemo
{
	private const int ADVANTAGE_BAR_HEIGHT   = 8;
	private const int ADVANTAGE_BAR_WIDTH    = 3;
	private const int ADVANTAGE_CP_DEAD_ZONE = 30;
	private const int ADVANTAGE_CP_DECISIVE  = 500;
	private const int ADVANTAGE_EVAL_TIME_MS = 250;
	private const int ENGINE_MOVE_TIME_MS    = 1_000;

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

		await using var client = new UciEngineClient(enginePath, options: options);
		client.StderrReceived += static line => Console.Error.WriteLine($"stderr: {line}");

		try
		{
			await client.StartAsync(CancellationToken.None);
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"Failed to start engine: {ex.Message}");
			return 1;
		}

		Console.WriteLine($"{client.EngineInfo.Name} by {client.EngineInfo.Author}");
		Console.WriteLine($"Engine executable: {enginePath}");
		Console.WriteLine();

		if (client.TryGetStrengthLimitRange(out int minElo, out int maxElo))
		{
			int elo = PromptElo(minElo, maxElo);
			await client.SetStrengthLimitAsync(elo, CancellationToken.None);
			Console.WriteLine($"Engine strength limited to {elo} Elo.");
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
			await client.UciNewGameAsync(CancellationToken.None);
			await RunGameLoopAsync(client, playerColor);
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

	private static PositionAdvantage BuildAdvantage(
		int? rawCpScore,
		int? rawMateScore,
		char sideToMove,
		char playerColor)
	{
		int perspective = sideToMove == playerColor ? 1 : -1;

		if (rawMateScore is int mateScore)
		{
			int adjustedMate = mateScore * perspective;
			int plyToMate    = Math.Abs(adjustedMate);
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
									 ? $"Advantage +{mateNormalized:F2} | You mate in {plyToMate}"
									 : $"Advantage {mateNormalized:F2} | Engine mate in {plyToMate}";

			return new(mateNormalized, mateSummary);
		}

		int    adjustedCp = (rawCpScore ?? 0) * perspective;
		int    absoluteCp = Math.Abs(adjustedCp);
		double normalized;

		if (absoluteCp <= ADVANTAGE_CP_DEAD_ZONE)
		{
			normalized = 0;
		}
		else
		{
			double scaled = (absoluteCp - ADVANTAGE_CP_DEAD_ZONE) /
							(double)(ADVANTAGE_CP_DECISIVE - ADVANTAGE_CP_DEAD_ZONE);

			double magnitude = Math.Min(0.95, Math.Pow(Math.Clamp(scaled, 0, 1), 0.8) * 0.95);
			normalized = adjustedCp > 0 ? magnitude : -magnitude;
		}

		string summary = normalized switch
		{
			> 0 => $"Advantage +{normalized:F2} | You {adjustedCp / 100.0:+0.0;-0.0;0.0} pawns",
			< 0 => $"Advantage {normalized:F2} | Engine {Math.Abs(adjustedCp) / 100.0:0.0} pawns",
			_   => "Advantage 0.00 | Even"
		};

		return new(normalized, summary);
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

	private static string PromptHumanMove(ImmutableArray<string> legalMoves)
	{
		while (true)
		{
			Console.Write("Your move: ");
			string move = NormalizeInput(ReadRequiredLine()).ToLowerInvariant();

			if (move is "quit" or "exit")
				return "quit";

			if (move == "moves")
			{
				PrintLegalMoves(legalMoves);
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
				PrintLegalMoves(legalMoves);
				continue;
			}

			return move;
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

		var filledRows = (int)Math.Round((advantage.Normalized + 1.0) / 2.0 * ADVANTAGE_BAR_HEIGHT);
		filledRows = Math.Clamp(filledRows, 0, ADVANTAGE_BAR_HEIGHT);

		for (var row = 0; row < ADVANTAGE_BAR_HEIGHT; row++)
		{
			bool   filled = row >= ADVANTAGE_BAR_HEIGHT - filledRows;
			string fill   = filled ? new('#', ADVANTAGE_BAR_WIDTH) : new string(' ', ADVANTAGE_BAR_WIDTH);
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

	private static async Task RunGameLoopAsync(UciEngineClient client, char playerColor)
	{
		var playedMoves = new List<string>();

		while (true)
		{
			var snapshot   = await LoadSnapshotAsync(client, playerColor, playedMoves);
			var fen        = snapshot.Fen;
			var legalMoves = snapshot.LegalMoves;
			PrintBoard(fen, playerColor, legalMoves.Length, snapshot.Advantage);

			if (legalMoves.Length == 0)
			{
				PrintGameOver(fen, playerColor);
				return;
			}

			if (fen.ActiveColor == playerColor)
			{
				string move = PromptHumanMove(legalMoves);
				if (move == "quit")
				{
					Console.WriteLine("Game aborted by user.");
					return;
				}

				playedMoves.Add(move);
				continue;
			}

			Console.WriteLine("Engine is thinking...");
			var    result = await client.GoAsync(new() { MoveTimeMs = ENGINE_MOVE_TIME_MS }, CancellationToken.None);
			string engineMove = result.BestMove.ToLowerInvariant();

			if (!ContainsMove(legalMoves, engineMove))
				throw new InvalidOperationException(
					$"Engine produced '{result.BestMove}', which is not legal in the current position."
				);

			Console.WriteLine($"Engine plays {engineMove}{FormatEngineLine(result)}");
			playedMoves.Add(engineMove);
		}
	}

	private static async Task<PositionAdvantage> EvaluateAdvantageAsync(
		UciEngineClient client,
		char            sideToMove,
		char            playerColor)
	{
		var evaluation = await client.GoAsync(
							 new() { MoveTimeMs = ADVANTAGE_EVAL_TIME_MS },
							 CancellationToken.None
						 );

		return BuildAdvantage(evaluation.BestCpScore, evaluation.MateScore, sideToMove, playerColor);
	}

	private static async Task<PositionSnapshot> LoadSnapshotAsync(
		UciEngineClient       client,
		char                  playerColor,
		IReadOnlyList<string> moves)
	{
		await client.SetPositionAsync(Fen.Default, moves, CancellationToken.None);

		var fen = await client.TryGetFenViaDisplayBoardAsync(CancellationToken.None);
		if (!fen.HasValue)
			throw new NotSupportedException(
				"This sample requires an engine that supports the non-standard 'd' command and returns a FEN line."
			);

		var legalMoves = NormalizeMoves(await client.GetLegalMovesViaPerftAsync(CancellationToken.None));
		if (moves.Count == 0 && legalMoves.Length == 0)
			throw new NotSupportedException(
				"This sample requires an engine that supports legal-move listing via the non-standard 'go perft 1' command."
			);

		var advantage = legalMoves.Length == 0
							? PositionAdvantage.GameOver()
							: await EvaluateAdvantageAsync(client, fen.Value.ActiveColor, playerColor);

		return new(fen.Value, legalMoves, advantage);
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

	private static void PrintLegalMoves(ImmutableArray<string> legalMoves)
	{
		if (legalMoves.IsDefaultOrEmpty)
		{
			Console.WriteLine("No legal moves are available.");
			return;
		}

		const int PREVIEW_COUNT = 24;
		string    suffix        = legalMoves.Length > PREVIEW_COUNT ? ", ..." : string.Empty;
		Console.WriteLine($"Legal moves: {string.Join(", ", legalMoves.Take(PREVIEW_COUNT))}{suffix}");
	}

	private readonly record struct PositionAdvantage(double Normalized, string Summary)
	{
		public static PositionAdvantage GameOver() => new(0, "Game over");
	}

	private readonly record struct PositionSnapshot(
		Fen                    Fen,
		ImmutableArray<string> LegalMoves,
		PositionAdvantage      Advantage
	);
}
