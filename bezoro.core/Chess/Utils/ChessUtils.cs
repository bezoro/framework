using System;

namespace Bezoro.Core.Chess.Utils
{
	public class ChessUtils
	{
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
	}
}
