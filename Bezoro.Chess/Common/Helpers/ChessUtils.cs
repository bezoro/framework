using System;
using System.Runtime.CompilerServices;
using Bezoro.Chess.Abstractions.Interfaces;
using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Pieces.Models;

namespace Bezoro.Chess.Common.Helpers
{
	public static class ChessUtils
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static char GetCharFromPiece(IChessPieceModel piece)
		{
			var pieceChar = piece.GetType().Name switch
			{
				nameof(KingModel)   => 'k',
				nameof(QueenModel)  => 'q',
				nameof(RookModel)   => 'r',
				nameof(BishopModel) => 'b',
				nameof(KnightModel) => 'n',
				nameof(PawnModel)   => 'p',
				_                   => throw new ArgumentException($"Invalid piece type: {piece.GetType().Name}")
			};

			return piece.Color == PlayerColor.White ? char.ToUpper(pieceChar) : pieceChar;
		}

		/// <summary>
		///     Converts a FEN (Forsyth–Edwards Notation) character to its corresponding chess piece type.
		/// </summary>
		/// <param name="fenChar">The FEN character representing a chess piece.</param>
		/// <returns>The corresponding ChessPieceType.</returns>
		/// <exception cref="ArgumentException">Thrown when the FEN character is invalid.</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ChessPieceType GetPieceTypeFromChar(char fenChar)
		{
			switch (fenChar)
			{
				case 'k': return ChessPieceType.King;
				case 'q': return ChessPieceType.Queen;
				case 'r': return ChessPieceType.Rook;
				case 'b': return ChessPieceType.Bishop;
				case 'n': return ChessPieceType.Knight;
				case 'p': return ChessPieceType.Pawn;
				default:  throw new ArgumentException($"Invalid FEN piece character: {fenChar}");
			}
		}

		/// <summary>
		///     Creates a chess piece model from a FEN (Forsyth–Edwards Notation) character.
		/// </summary>
		/// <param name="fenChar">The FEN character representing a chess piece.</param>
		/// <returns>A new instance of IChessPieceModel representing the chess piece.</returns>
		/// <exception cref="ArgumentException">Thrown when the FEN character is invalid.</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IChessPieceModel GetPieceFromChar(char fenChar)
		{
			var color     = fenChar.ToPlayerColor();
			var lowerChar = char.ToLower(fenChar);

			return lowerChar switch
			{
				'k' => new KingModel(color),
				'p' => new PawnModel(color),
				'q' => new QueenModel(color),
				'r' => new RookModel(color),
				'b' => new BishopModel(color),
				'n' => new KnightModel(color),
				_   => throw new ArgumentException($"Invalid FEN piece character: {fenChar}")
			};
		}

		/// <summary>
		///     Converts a character to a PlayerColor based on its case.
		/// </summary>
		/// <param name="c">The character to convert.</param>
		/// <returns>White for uppercase characters, Black for lowercase characters.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static PlayerColor ToPlayerColor(this char c) =>
			char.IsUpper(c) ? PlayerColor.White : PlayerColor.Black;
	}
}
