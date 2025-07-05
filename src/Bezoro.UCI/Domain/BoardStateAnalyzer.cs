using System.Collections.Generic;
using System.Linq;
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
		}
	}
}
