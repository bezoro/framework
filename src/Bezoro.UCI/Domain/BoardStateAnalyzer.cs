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
		///     Determines if the current position is a checkmate.
		///     A checkmate occurs when the king is in check and there are no legal moves available.
		/// </summary>
		/// <param name="ct">A token to cancel the operation.</param>
		/// <returns>True if the position is checkmate, otherwise false.</returns>
		public async Task<bool> IsCheckmateAsync(CancellationToken ct = default)
		{
			var    fenInfo    = await ParseCurrentFenAsync(ct);
			string kingSquare = await FindKingSquare(fenInfo.ActiveColor, ct);
			return await IsSquareAttackedByAsync(kingSquare, fenInfo.ActiveColor, ct);

			// Get all legal moves for the current player
			List<string> legalMoves = await GetLegalMovesAsync(ct);

			// If there are legal moves, it's not checkmate
			if (legalMoves.Count > 0)
			{
				return false;
			}

			// No legal moves - determine if it's checkmate or stalemate
			return await IsKingInCheckAsync();
		}

		public async Task<bool> IsKingInCheckAsync(char? colorToCheck = null, CancellationToken ct = default)
		{
			const char WhitePlayer = 'w';
			const char BlackPlayer = 'b';
			var        fenInfo     = await ParseCurrentFenAsync(ct);

			// Use specified color or default to active color
			char   kingColor     = colorToCheck ?? fenInfo.ActiveColor;
			string kingSquare    = await FindKingSquare(kingColor, ct);
			char   opponentColor = kingColor == WhitePlayer ? BlackPlayer : WhitePlayer;

			return await IsSquareAttackedByAsync(kingSquare, opponentColor, ct);
		}

		public async Task<bool> IsSquareAttackedByAsync(string square, char playerColor, CancellationToken ct = default)
		{
			if (string.IsNullOrWhiteSpace(square) || !UCIHelper.IsValidAlgebraicNotation(square))
			{
				throw new ArgumentException($"Square '{square}' is not in valid algebraic notation (e.g., 'e4').",
					nameof(square));
			}

			// 1. Get the original position's FEN to understand the current state.
			string   originalFen = await GetCurrentFENAsync(ct);
			string[] fenParts    = originalFen.Split(' ');
			if (fenParts.Length < 2)
			{
				throw new UCIException("Failed to parse FEN string from engine.");
			}

			// 2. Get legal moves for the specified player.
			List<string> moves = await GetMovesForPlayerAsync(playerColor, ct);

			// 3. Check if any available move targets the given square.
			bool isAttacked = moves.Any(move =>
				move.Length >= 4 &&
				move.Substring(2, 2).Equals(square, StringComparison.OrdinalIgnoreCase)
			);

			return isAttacked;
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
		///     Finds the king's square on the board.
		/// </summary>
		/// <param name="fen">The board position as a FEN string.</param>
		/// <param name="kingColor">The active color.</param>
		public async Task<string> FindKingSquare(char kingColor, CancellationToken ct = default)
		{
			char kingPiece = kingColor == 'w' ? 'K' : 'k';

			var      fen   = await ParseCurrentFenAsync(ct);
			string[] ranks = fen.PiecePlacement.Split('/');

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

			throw new UCIException($"Could not find {kingColor} king in position");
		}

		/// <summary>
		///     Gets the current position in FEN notation.
		/// </summary>
		/// <param name="ct">A token to cancel the operation.</param>
		public async Task<string> GetCurrentFENAsync(CancellationToken ct = default)
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

		/// <summary>
		///     Gets all legal moves for a specific player color.
		/// </summary>
		/// <param name="colorToCheck">The color to check.</param>
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
}
