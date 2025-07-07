using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.UCI.API.Types;
using Bezoro.UCI.Domain.Constants;
using Bezoro.UCI.Domain.Exceptions;
using Bezoro.UCI.Domain.Helpers;

namespace Bezoro.UCI.Domain
{
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
		///     Constructs a full FEN string by deriving castling rights from the piece placement.
		/// </summary>
		/// <param name="piecePlacement">The FEN piece placement string (e.g., "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR").</param>
		/// <param name="activeColor">The color of the player to move ('w' or 'b').</param>
		/// <param name="enPassantTarget">The en passant target square in algebraic notation (e.g., "e3"), or "-" if none.</param>
		/// <param name="halfmoveClock">The number of half-moves since the last capture or pawn advance.</param>
		/// <param name="fullmoveNumber">The number of the full move. It starts at 1 and is incremented after Black's move.</param>
		/// <returns>A complete and valid FEN string.</returns>
		public string BuildFENFromParts(
			string piecePlacement, char activeColor, string enPassantTarget = "-", int halfmoveClock = 0,
			int fullmoveNumber = 1)
		{
			BoardStateParser.ValidatePiecePlacement(piecePlacement);
			BoardStateParser.ValidateActiveColor(activeColor);
			BoardStateParser.ValidateEnPassant(enPassantTarget, activeColor);
			BoardStateParser.ValidateHalfmoveClock(halfmoveClock.ToString());
			BoardStateParser.ValidateFullmoveNumber(fullmoveNumber.ToString());

			string castlingRights = DeriveCastlingRightsFromPiecePlacementFEN(piecePlacement);
			return
				$"{piecePlacement} {activeColor} {castlingRights} {enPassantTarget} {halfmoveClock} {fullmoveNumber}";
		}

		/// <summary>
		///     Derives the castling availability string from the piece placement part of a FEN.
		///     This method checks if the kings and rooks are on their starting squares.
		/// </summary>
		/// <param name="piecePlacement">The FEN piece placement string (e.g., "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR").</param>
		/// <returns>The castling availability string (e.g., "KQkq", "-", "Kq").</returns>
		public string DeriveCastlingRightsFromPiecePlacementFEN(string piecePlacement)
		{
			string[]? ranks = piecePlacement.Split('/');
			if (ranks.Length != 8)
			{
				// Return no rights for invalid piece placement string
				return "-";
			}

			// Create an 8x8 board representation for easier lookup
			var board = new char[8, 8]; // [rank, file]
			for (var i = 0 ; i < 8 ; i++)
			{
				var fileIndex = 0;
				foreach (char c in ranks[i]) // ranks[0] is rank 8, ranks[7] is rank 1
				{
					if (fileIndex >= 8)
					{
						break;
					}

					if (char.IsDigit(c))
					{
						fileIndex += (int)char.GetNumericValue(c);
					}
					else
					{
						board[i, fileIndex] = c;
						fileIndex++;
					}
				}
			}

			var castlingRights = new StringBuilder();

			// Check White's castling rights (King on e1)
			if (board[7, 4] == 'K')
			{
				if (board[7, 7] == 'R')
				{
					castlingRights.Append('K'); // Rook on h1
				}

				if (board[7, 0] == 'R')
				{
					castlingRights.Append('Q'); // Rook on a1
				}
			}

			// Check Black's castling rights (King on e8)
			if (board[0, 4] == 'k')
			{
				if (board[0, 7] == 'r')
				{
					castlingRights.Append('k'); // Rook on h8
				}

				if (board[0, 0] == 'r')
				{
					castlingRights.Append('q'); // Rook on a8
				}
			}

			return castlingRights.Length == 0 ? "-" : castlingRights.ToString();
		}

		/// <summary>
		///     Finds the king's square on the board.
		/// </summary>
		/// <param name="boardPosition">The board position as a FEN string.</param>
		/// <param name="activeColor">The active color.</param>
		public string FindKingSquare(string boardPosition, char activeColor)
		{
			char kingPiece = activeColor == 'w' ? 'K' : 'k';

			string[] ranks = boardPosition.Split('/');

			for (var rank = 0 ; rank < 8 ; rank++)
			{
				var file = 0;
				foreach (char c in ranks[rank])
				{
					if (char.IsDigit(c))
					{
						file += int.Parse(c.ToString());
					}
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

			throw new UCIException($"Could not find {activeColor} king in position");
		}

		/// <summary>
		///     Gets all legal moves with detailed classification.
		/// </summary>
		/// <param name="cancellationToken">A token to cancel the operation.</param>
		public async Task<List<MoveClassification>> GetAllLegalMovesWithDetailsAsync(
			CancellationToken cancellationToken = default)
		{
			string       currentFen = await GetCurrentFENAsync(cancellationToken);
			List<string> legalMoves = await GetLegalMovesAsync(cancellationToken);
			var          boardState = BoardStateParser.ParseFen(currentFen);
			return legalMoves.Select(move => MoveClassifier.ClassifyMove(move, boardState)).ToList();
		}

		/// <summary>
		///     Gets all legal moves from the current position.
		/// </summary>
		/// <param name="ct">A token to cancel the operation.</param>
		public async Task<List<string>> GetLegalMovesAsync(CancellationToken ct = default)
		{
			var moves = new List<string>();

			await _commandSender.SendCommandAsync(UCIConstants.GoPerftDepth1Command, false, ct);

			while (true)
			{
				string? line = await _outputParser.ReadProcessOutputAsync(ct);
				if (line == null || line.Contains("Nodes searched"))
				{
					break;
				}

				var match = UCIConstants.MoveRegex.Match(line);

				if (match.Success)
				{
					moves.Add(match.Groups[1].Value);
				}
			}

			return moves;
		}

		/// <summary>
		///     Gets all legal moves for a specific player color.
		/// </summary>
		/// <param name="colorToCheck">The color to check.</param>
		/// <param name="activeColor">The active color.</param>
		/// <param name="fenParts">The FEN parts.</param>
		/// <param name="originalFen">The original FEN string.</param>
		/// <param name="cancellationToken">A token to cancel the operation.</param>
		public async Task<List<string>> GetMovesForPlayerAsync(
			char colorToCheck, char activeColor, string[] fenParts, string originalFen,
			CancellationToken cancellationToken)
		{
			if (colorToCheck == activeColor)
			{
				// If we're checking the current player, we don't need to change the engine's state.
				return await GetLegalMovesAsync(cancellationToken);
			}

			// If checking the other player, we temporarily flip the turn in the FEN string.
			// The en-passant square is cleared as it's not valid after a turn flip.
			var tempFen = $"{fenParts[0]} {colorToCheck} {fenParts[2]} - {fenParts[4]} {fenParts[5]}";

			// Set the engine to the temporary position.
			await _commandSender.SendCommandAsync($"{UCIConstants.PositionCommand} fen {tempFen}", true,
				cancellationToken);

			// Get all legal moves for that player.
			List<string> moves = await GetLegalMovesAsync(cancellationToken);

			// IMPORTANT: Restore the engine to its original state to ensure consistency.
			await _commandSender.SendCommandAsync($"{UCIConstants.PositionCommand} fen {originalFen}", true,
				cancellationToken);

			return moves;
		}

		/// <summary>
		///     Gets the current position in FEN notation.
		/// </summary>
		/// <param name="ct">A token to cancel the operation.</param>
		public async Task<string> GetCurrentFENAsync(CancellationToken ct)
		{
			// The 'd' command doesn't have a clear "readyok" end signal, so we read until we find the FEN
			await _commandSender.SendCommandAsync(UCIConstants.DisplayBoardCommand, false, ct);

			while (true)
			{
				string? line = await _outputParser.ReadProcessOutputAsync(ct);
				if (line is null)
				{
					// End of stream reached before finding the FEN.
					break;
				}

				if (line.StartsWith(UCIConstants.FenResponsePrefix, StringComparison.Ordinal))
				{
					return line.Substring(UCIConstants.FenResponsePrefix.Length).Trim();
				}
			}

			throw new UCIException("Engine did not return a FEN string. The 'd' command may not be supported.");
		/// <summary>
		///     Gets all legal moves for a specific player color.
		/// </summary>
		/// <param name="colorToCheck">The color to check.</param>
		/// <param name="activeColor">The active color.</param>
		/// <param name="fenParts">The FEN parts.</param>
		/// <param name="originalFen">The original FEN string.</param>
		/// <param name="ct">A token to cancel the operation.</param>
		internal async Task<List<string>> GetMovesForPlayerAsync(char colorToCheck, CancellationToken ct = default)
		{
			var fenInfo = await ParseCurrentFenAsync(ct);
			if (colorToCheck == fenInfo.ActiveColor)
			{
				// If we're checking the current player, we don't need to change the engine's state.
				return await GetLegalMovesAsync(ct);
			}

			// If checking the other player, we temporarily flip the turn in the FEN string.
			// The en-passant square is cleared as it's not valid after a turn flip.
			string[] fenParts = fenInfo.FenParts;
			var      tempFen  = $"{fenParts[0]} {colorToCheck} {fenParts[2]} - {fenParts[4]} {fenParts[5]}";

			// Set the engine to the temporary position.
			await _commandSender.SendCommandAsync($"{UCIConstants.PositionCommand} fen {tempFen}", true, ct);

			// Get all legal moves for that player.
			List<string> moves = await GetLegalMovesAsync(ct);

			// IMPORTANT: Restore the engine to its original state to ensure consistency.
			await _commandSender.SendCommandAsync($"{UCIConstants.PositionCommand} fen {fenInfo.CurrentFen}", true, ct);

			return moves;
		}

		internal async Task<FenInfo> ParseCurrentFenAsync(CancellationToken ct = default)
		{
			const int MinimumFenPartsRequired = 2;

			string   currentFen = await GetCurrentFENAsync(ct);
			string[] fenParts   = currentFen.Split(' ');

			if (fenParts.Length < MinimumFenPartsRequired)
			{
				throw new UCIException("Invalid FEN string returned from engine");
			}

			return new FenInfo(currentFen, fenParts);
		}

		internal readonly struct FenInfo
		{
			public FenInfo(string currentFen, string[] fenParts)
			{
				CurrentFen      = currentFen;
				FenParts        = fenParts;
				ActiveColor     = fenParts[1][0];
				CastlingRights  = fenParts[2];
				EnPassantTarget = fenParts[3];
				HalfmoveClock   = int.Parse(fenParts[4]);
				FullmoveNumber  = int.Parse(fenParts[5]);
			}

			public char     ActiveColor     { get; }
			public string   CurrentFen      { get; }
			public string[] FenParts        { get; }
			public string   CastlingRights  { get; }
			public string   EnPassantTarget { get; }
			public int      HalfmoveClock   { get; }
			public int      FullmoveNumber  { get; }
		}
	}
}
