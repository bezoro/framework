using System;
using Bezoro.Chess.Domain.Board;

namespace Bezoro.Chess.Application.Abstractions.ViewModels
{
	public enum MoveHighlightType
	{
		Legal,  // A valid, legal move
		Illegal // A pseudo-legal move that is invalid (e.g., leaves king in check)
	}

	public readonly struct MoveHighlightViewModel : IEquatable<MoveHighlightViewModel>
	{
		public MoveHighlightType HighlightType { get; }

		public Position Position { get; }

		#region Equality

		public static bool operator ==(MoveHighlightViewModel left, MoveHighlightViewModel right) => left.Equals(right);

		public static bool operator !=(MoveHighlightViewModel left, MoveHighlightViewModel right) =>
			!left.Equals(right);

		public bool Equals(MoveHighlightViewModel other) =>
			Position.Equals(other.Position) && HighlightType == other.HighlightType;

		public override bool Equals(object? obj) =>
			obj is MoveHighlightViewModel other && Equals(other);

		public override int GetHashCode() =>
			HashCode.Combine(Position, (int)HighlightType);

		#endregion

		public MoveHighlightViewModel(Position position, MoveHighlightType highlightType)
		{
			Position      = position;
			HighlightType = highlightType;
		}
	}
}
