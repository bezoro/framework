using System.Collections.Generic;
using Bezoro.Core.Chess.Interfaces;

namespace Bezoro.Core.Chess.Pieces
{
	public abstract class PieceModel : IChessPieceModel
	{
		protected PieceModel(PlayerColor color, IPseudoMoveGenerator pseudoMoveGenerator)
		{
			Color                = color;
			_pseudoMoveGenerator = pseudoMoveGenerator;
		}

		private readonly IPseudoMoveGenerator _pseudoMoveGenerator;

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
			_pseudoMoveGenerator.Generate(board, this);

		public void MarkMoved() =>
			HasMoved = true;

		public virtual void ResetMoved() =>
			HasMoved = false;

	#endregion
	}

	public interface IPseudoMoveGenerator
	{
		/// <summary>Return all moves that are geometrically possible for <paramref name="piece" />.</summary>
		IEnumerable<Move> Generate(IChessBoardModel board, PieceModel piece);
	}
}
