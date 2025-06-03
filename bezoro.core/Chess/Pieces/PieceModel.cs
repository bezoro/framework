using System.Collections.Generic;

namespace Bezoro.Core.Chess
{
	public abstract class PieceModel : IChessPieceModel
	{
		protected PieceModel(PlayerColor color, IMoveGenerator moveGenerator)
		{
			Color          = color;
			_moveGenerator = moveGenerator;
		}

		private readonly IMoveGenerator _moveGenerator;

		public PlayerColor Color { get; }

		public PlayerColor Opposite => Color switch
		{
			PlayerColor.White => PlayerColor.Black,
			PlayerColor.Black => PlayerColor.White,
			_                 => PlayerColor.None
		};

		public bool HasMoved { get; private set; }

	#region Interface Implementations

		public IEnumerable<Move> GetPseudoLegalMoves(IChessBoardModel board) =>
			_moveGenerator.Generate(board, this);

		public void MarkMoved() =>
			HasMoved = true;

		public virtual void ResetMoved() =>
			HasMoved = false;

	#endregion
	}

	public interface IMoveGenerator
	{
		IEnumerable<Move> Generate(IChessBoardModel board, PieceModel piece);
	}
}
