using System.Collections.Generic;
using Bezoro.Chess.Domain.Types.Structs;
using Bezoro.Core.Common.Helpers;

namespace Bezoro.Chess.API.ViewModels
{
	public readonly struct BoardViewModel
	{
		public BoardViewModel(IEnumerable<PieceViewModel> pieces)
		{
			Pieces = pieces;
		}

		public IEnumerable<PieceViewModel> Pieces { get; }

		internal BoardViewModel FromBoardPiecesToViewModelPieces(Piece[,] board)
		{
			Piece[] from2Dto1D = ArrayConverter.From2Dto1D(board);
			var     pieces     = new List<PieceViewModel>();
			for (var i = 0 ; i < from2Dto1D.Length ; i++)
			{
				Piece piece = from2Dto1D[i];
				pieces.Add(new PieceViewModel((piece.Type, piece.Color)));
			}

			return new BoardViewModel(pieces);
		}
	}
}
