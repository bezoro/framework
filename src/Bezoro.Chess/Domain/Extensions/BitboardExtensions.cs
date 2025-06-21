using System.Collections.Generic;
using Bezoro.Chess.API.Extensions;
using Bezoro.Chess.API.ViewModels;
using Bezoro.Chess.Domain.Types.Records;

namespace Bezoro.Chess.Domain.Extensions
{
	internal static class BitboardExtensions
	{
		public static BoardViewModel ToViewModel(this Board board)
		{
			IEnumerable<PieceViewModel> pieces = board.GetPieces().ToViewModel();
			var                         vm     = new BoardViewModel(pieces);
			return vm;
		}
	}
}
