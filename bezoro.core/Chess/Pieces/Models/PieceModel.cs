using System.Collections.Generic;
using Bezoro.Core.Chess.Abstractions.Interfaces;
using Bezoro.Core.Chess.Common.Enums;
using Bezoro.Core.Chess.Common.Extensions;
using Bezoro.Core.Chess.Game.Models;
using Bezoro.Core.Chess.Moves.Models;

namespace Bezoro.Core.Chess.Pieces.Models
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

		public IEnumerable<Move> GetPseudoLegalMoves(GameModel game) =>
			_pseudoMoveGenerator.Generate(game, this);

		public void MarkMoved() =>
			HasMoved = true;

		public virtual void ResetMoved() =>
			HasMoved = false;

		public ChessPieceType GetPieceType() =>
			PieceModelExtensions.GetPieceType(this);

	#endregion
	}

	public interface IPseudoMoveGenerator
	{
		/// <summary>Return all moves that are geometrically possible for <paramref name="piece" />.</summary>
		IEnumerable<Move> Generate(GameModel game, IChessPieceModel piece);
	}
}
