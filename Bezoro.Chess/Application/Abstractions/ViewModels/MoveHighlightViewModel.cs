using Bezoro.Chess.Domain.Board;

namespace Bezoro.Chess.Application.Abstractions.ViewModels
{
	public enum MoveHighlightType
	{
		Legal,  // A valid, legal move
		Illegal // A pseudo-legal move that is invalid (e.g., leaves king in check)
	}

	public record MoveHighlightViewModel(Position Position, MoveHighlightType HighlightType);
}
