using System;
using System.Collections.Generic;
using Bezoro.Chess.Domain.Types.Structs;

namespace Bezoro.Chess.API.ViewModels
{
	public readonly struct GameStatusViewModel : IEquatable<GameStatusViewModel>
	{
		public bool                          IsInCheck           { get; }
		public IReadOnlyList<PieceViewModel> CapturedBlackPieces { get; }
		public IReadOnlyList<PieceViewModel> CapturedWhitePieces { get; }
		public PieceColor                    CurrentTurn         { get; }
		public string                        GameResult          { get; }

		public GameStatusViewModel(
			PieceColor currentTurn,
			string gameResult,
			bool isInCheck,
			IReadOnlyList<PieceViewModel> capturedWhitePieces,
			IReadOnlyList<PieceViewModel> capturedBlackPieces)
		{
			CurrentTurn         = currentTurn;
			GameResult          = gameResult;
			IsInCheck           = isInCheck;
			CapturedWhitePieces = capturedWhitePieces;
			CapturedBlackPieces = capturedBlackPieces;
		}

		#region Equalilty

		public bool Equals(GameStatusViewModel other) =>
			IsInCheck == other.IsInCheck                          &&
			CapturedBlackPieces.Equals(other.CapturedBlackPieces) &&
			CapturedWhitePieces.Equals(other.CapturedWhitePieces) &&
			CurrentTurn == other.CurrentTurn                      &&
			GameResult  == other.GameResult;

		public override bool Equals(object? obj) =>
			obj is GameStatusViewModel other && Equals(other);

		public override int GetHashCode() =>
			HashCode.Combine(IsInCheck, CapturedBlackPieces, CapturedWhitePieces, (int)CurrentTurn, GameResult);

		public static bool operator ==(GameStatusViewModel left, GameStatusViewModel right) => left.Equals(right);

		public static bool operator !=(GameStatusViewModel left, GameStatusViewModel right) => !left.Equals(right);

		#endregion
	}
}
