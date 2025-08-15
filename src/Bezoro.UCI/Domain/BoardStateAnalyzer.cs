using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.UCI.Domain.Common.Constants;
using Bezoro.UCI.Domain.Common.Exceptions;
using Bezoro.UCI.Domain.Common.Helpers;

namespace Bezoro.UCI.Domain;

/// <summary>
///     Analyzes the state of a chess board.
/// </summary>
internal sealed class BoardStateAnalyzer
{
	private readonly EngineCommandSender _commandSender;
	private readonly EngineOutputParser  _outputParser;

	/// <summary>
	///     Initializes a new instance of the <see cref="BoardStateAnalyzer" /> class.
	/// </summary>
	/// <param name="commandSender">The engine command sender.</param>
	/// <param name="outputParser">The engine output parser.</param>
	public BoardStateAnalyzer(EngineCommandSender commandSender, EngineOutputParser outputParser)
	{
		_commandSender = commandSender;
		_outputParser  = outputParser;
	}

	/// <summary>
	///     Determines if the current position is a checkmate.
	///     A checkmate occurs when the king is in check and there are no legal moves available.
	/// </summary>
	/// <param name="ct">A token to cancel the operation.</param>
	/// <returns>True if the position is checkmate, otherwise false.</returns>
	public async Task<bool> IsCheckmateAsync(CancellationToken ct = default)
	{
		var    fenInfo       = await ParseCurrentFenAsync(ct);
		char   oppositeColor = fenInfo.ActiveColor == 'w' ? 'b' : 'w';
		string kingSquare    = await FindKingSquare(fenInfo.ActiveColor, ct);
		bool   isAttacked    = await IsSquareAttackedByAsync(kingSquare, oppositeColor, ct);

		// For it to be checkmate, the king of the active player must be under attack (in check).
		if (!isAttacked) return false;

		// Get all legal moves for the current player.
		var legalMoves = await GetLegalMovesAsync(ct);

		// If the king is in check and there are no legal moves, it's checkmate.
		return legalMoves.Count == 0;
	}

	public async Task<bool> IsKingInCheckAsync(char? colorToCheck = null, CancellationToken ct = default)
	{
		const char WhitePlayer = 'w';
		const char BlackPlayer = 'b';

		var fenInfo = await ParseCurrentFenAsync(ct);

		char   kingColor     = colorToCheck ?? (fenInfo.ActiveColor == WhitePlayer ? BlackPlayer : WhitePlayer);
		string kingSquare    = await FindKingSquare(kingColor, ct);
		char   opponentColor = kingColor == WhitePlayer ? BlackPlayer : WhitePlayer;

		return await IsSquareAttackedByAsync(kingSquare, opponentColor, ct);
	}

	public async Task<bool> IsSquareAttackedByAsync(string square, char playerColor, CancellationToken ct = default)
	{
		if (string.IsNullOrWhiteSpace(square) || !UCIHelper.IsValidAlgebraicNotation(square))
		{
			throw new ArgumentException(
				$"Square '{square}' is not in valid algebraic notation (e.g., 'e4').",
				nameof(square));
		}

		// 1. Get the original position's FEN to understand the current state.
		string   originalFen = await GetCurrentFENAsync(ct);
		string[] fenParts    = originalFen.Split(' ');
		if (fenParts.Length < 2) throw new UCIException("Failed to parse FEN string from engine.");

		// 2. Get legal moves for the specified player.
		var moves = await GetMovesForPlayerAsync(playerColor, ct);

		// 3. Check if any available move targets the given square.
		bool isAttacked = moves.Any(move =>
										move.Length >= 4 &&
										move.Substring(2, 2).Equals(square, StringComparison.OrdinalIgnoreCase)
		);

		return isAttacked;
	}

	/// <summary>
	///     Gets all legal moves from the current position.
	/// </summary>
	/// <param name="ct">A token to cancel the operation.</param>
	public async Task<List<string>> GetLegalMovesAsync(CancellationToken ct = default)
	{
		var moves = new List<string>();

		await _commandSender.SendCommandAsync(UciConstants.GO_PERFT_DEPTH1_COMMAND, false, ct);

		while (true)
		{
			string? line = await _outputParser.ReadLineFromProcessAsync(ct);
			if (line == null || line.Contains("Nodes searched")) break;

			var match = UciConstants.MoveRegex.Match(line);

			if (match.Success) moves.Add(match.Groups[1].Value);
		}

		return moves;
	}

	/// <summary>
	///     Finds the king's square on the board.
	/// </summary>
	/// <param name="fen">The board position as a FEN string.</param>
	/// <param name="kingColor">The active color.</param>
	public async Task<string> FindKingSquare(char kingColor, CancellationToken ct = default)
	{
		char kingPiece = kingColor == 'w' ? 'K' : 'k';

		var      fen   = await ParseCurrentFenAsync(ct);
		string[] ranks = fen.PiecePlacement.Split('/');

		for (var rank = 0; rank < 8; rank++)
		{
			var file = 0;
			foreach (char c in ranks[rank])
			{
				if (char.IsDigit(c))
					file += int.Parse(c.ToString());
				else
				{
					if (c == kingPiece)
					{
						// Convert to algebraic notation
						var fileChar = (char)('a' + file);
						var rankChar = (char)('8' - rank);
						return $"{fileChar}{rankChar}";
					}

					file++;
				}
			}
		}

		throw new UCIException($"Could not find {kingColor} king in position");
	}

	/// <summary>
	///     Gets the current position in FEN notation.
	/// </summary>
	/// <param name="ct">A token to cancel the operation.</param>
	public async Task<string> GetCurrentFENAsync(CancellationToken ct = default)
	{
		// The 'd' command doesn't have a clear "readyok" end signal, so we read until we find the FEN
		await _commandSender.SendCommandAsync(UciConstants.DISPLAY_BOARD_COMMAND, false, ct);

		// Set a timeout for reading the response to prevent stalling.
		using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		using var linkedCts  = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

		try
		{
			while (true)
			{
				string? line = await _outputParser.ReadLineFromProcessAsync(linkedCts.Token);
				if (line is null)
				{
					// End of stream reached before finding the FEN.
					break;
				}

				if (line.StartsWith(UciConstants.FEN_RESPONSE_PREFIX, StringComparison.Ordinal))
					return line[UciConstants.FEN_RESPONSE_PREFIX.Length..].Trim();
			}
		}
		catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
		{
			throw new UCIException(
				"Timed out waiting for FEN string from engine. The 'd' command may not be supported or the engine is unresponsive.");
		}

		throw new UCIException("Engine did not return a FEN string. The 'd' command may not be supported.");
	}

	internal async Task<FenInfo> ParseCurrentFenAsync(CancellationToken ct = default)
	{
		const int MinimumFenPartsRequired = 2;

		string   currentFen = await GetCurrentFENAsync(ct);
		string[] fenParts   = currentFen.Split(' ');

		if (fenParts.Length < MinimumFenPartsRequired)
			throw new UCIException("Invalid FEN string returned from engine");

		return new(currentFen, fenParts);
	}

	/// <summary>
	///     Gets all legal moves for a specific player color.
	/// </summary>
	/// <param name="colorToCheck">The color to check.</param>
	/// <param name="ct">A token to cancel the operation.</param>
	internal async Task<List<string>> GetMovesForPlayerAsync(char colorToCheck, CancellationToken ct = default)
	{
		var fenInfo = await ParseCurrentFenAsync(ct);
		if (colorToCheck == fenInfo.ActiveColor) return await GetLegalMovesAsync(ct);

		string[]     fenParts           = fenInfo.FenParts;
		const string newEnPassantTarget = "-";
		int          newHalfmoveClock   = fenInfo.HalfmoveClock + 1;
		int newFullmoveNumber = fenInfo.ActiveColor == 'b'
									? fenInfo.FullmoveNumber + 1
									: fenInfo.FullmoveNumber;

		string castlingRights = fenParts[2];
		var tempFen =
			$"{fenParts[0]} {colorToCheck} {castlingRights} {newEnPassantTarget} {newHalfmoveClock} {newFullmoveNumber}";

		await _commandSender.SendCommandAsync($"{UciConstants.POSITION_COMMAND} fen {tempFen}", true, ct);
		var moves = await GetLegalMovesAsync(ct);
		await _commandSender.SendCommandAsync($"{UciConstants.POSITION_COMMAND} fen {fenInfo.CurrentFen}", true, ct);

		return moves;
	}

	internal readonly struct FenInfo
	{
		public FenInfo(string currentFen, string[] fenParts)
		{
			CurrentFen      = currentFen;
			FenParts        = fenParts;
			PiecePlacement  = fenParts[0];
			ActiveColor     = fenParts[1][0];
			CastlingRights  = fenParts[2];
			EnPassantTarget = fenParts[3];
			HalfmoveClock   = int.Parse(fenParts[4]);
			FullmoveNumber  = int.Parse(fenParts[5]);
		}

		public char     ActiveColor     { get; }
		public int      FullmoveNumber  { get; }
		public int      HalfmoveClock   { get; }
		public string   CastlingRights  { get; }
		public string   CurrentFen      { get; }
		public string   EnPassantTarget { get; }
		public string   PiecePlacement  { get; }
		public string[] FenParts        { get; }
	}
}
