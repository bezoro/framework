using Bezoro.Chess.API.ViewModels;
using Bezoro.Chess.Domain.Extensions;
using Bezoro.Chess.Domain.Types.Structs;

namespace Bezoro.Chess.API.Extensions
{
	internal static class PieceViewModelExtensions
	{
		internal static Piece ToDomain(this PieceViewModel pieceViewModel) =>
			new(pieceViewModel.Type.ToDomain(), pieceViewModel.Color.ToDomain());
	}
}
