using Bezoro.Chess.Domain.Extensions;
using Bezoro.Chess.Domain.Types.Structs;
using Bezoro.Core.Common.Helpers;

namespace Bezoro.Chess.API.ViewModels
{
	public readonly struct BoardViewModel
	{
		public BoardViewModel(PieceViewModel[] pieces)
		{
			Pieces = pieces;
		}

		public PieceViewModel[] Pieces { get; }

		internal void FromBoardPiecesToViewModelPieces(Piece[,] board)
		{
			Piece[] from2Dto1D = ArrayConverter.From2Dto1D(board);
			for (var i = 0 ; i < from2Dto1D.Length ; i++)
			{
				Piece piece = from2Dto1D[i];
				Pieces[i] = new PieceViewModel(piece.Type.ToAPI(), piece.Color.ToAPI());
			}
		}
	}
}
