using System;
using Bezoro.Chess.Domain.Types.Records;
using Bezoro.Chess.Domain.Types.Structs;

namespace Bezoro.Chess.Domain.Extensions
{
	/// <summary>
	///     Extension helpers that convert <see cref="Move" /> instances to Standard Algebraic Notation (SAN).
	/// </summary>
	internal static class MoveSANExtensions
	{
		/// <summary>
		///     Returns fully qualified SAN (captures, promotions, check, mate, castling).
		/// </summary>
		/// <param name="move">The move to format.</param>
		/// <param name="stateBeforeMove">Game state *before* <paramref name="move" /> is executed.</param>
		public static string ToSAN(this Move move, GameState stateBeforeMove) =>
			throw new NotImplementedException();
	}
}
