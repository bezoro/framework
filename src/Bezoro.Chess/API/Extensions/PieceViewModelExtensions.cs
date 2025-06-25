using System.Collections.Generic;
using System.Linq;
using Bezoro.Chess.API.ViewModels;
using Bezoro.Chess.Domain.Extensions;
using Bezoro.Chess.Domain.Types.Structs;

namespace Bezoro.Chess.API.Extensions
{
	internal static class PieceViewModelExtensions
	{
		internal static IEnumerable<PieceViewModel> ToViewModel(this IEnumerable<Piece> pieces) =>
			pieces.Select(p => p.ToViewModel());

		internal static Piece ToDomain(this PieceViewModel pieceViewModel) =>
			new(pieceViewModel.Type.ToDomain(), pieceViewModel.Color.ToDomain());

		internal static PieceViewModel ToViewModel(this Piece piece) =>
			new((piece.Type, piece.Color));
	}
}
