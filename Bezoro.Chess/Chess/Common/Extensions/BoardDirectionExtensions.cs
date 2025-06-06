using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Bezoro.Chess.Chess.Common.Enums;

namespace Bezoro.Chess.Chess.Common.Extensions
{
	public static class BoardDirectionExtensions
	{
		private static readonly IReadOnlyDictionary<CardinalDirection, (int dx, int dy)> _OFFSETS =
			new Dictionary<CardinalDirection, (int dx, int dy)>
			{
				[CardinalDirection.North]     = (0, 1),
				[CardinalDirection.South]     = (0, -1),
				[CardinalDirection.East]      = (1, 0),
				[CardinalDirection.West]      = (-1, 0),
				[CardinalDirection.NorthEast] = (1, 1),
				[CardinalDirection.NorthWest] = (-1, 1),
				[CardinalDirection.SouthEast] = (1, -1),
				[CardinalDirection.SouthWest] = (-1, -1)
			};

		/// <summary>
		///     Converts a cardinal direction enum value to corresponding offset coordinates.
		/// </summary>
		/// <param name="d">The cardinal direction.</param>
		/// <returns>A tuple containing the dx and dy offsets for the specified direction.</returns>
		/// <remarks>
		///     The coordinate system has (0,0) at the bottom-left with x increasing eastward and y increasing northward.
		///     North = (0,1), East = (1,0), South = (0,-1), West = (-1,0), and diagonals are combinations.
		/// </remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (int dx, int dy) MapDirectionToOffsets(CardinalDirection d) => d switch
		{
			CardinalDirection.North     => (0, 1),
			CardinalDirection.NorthEast => (1, 1),
			CardinalDirection.East      => (1, 0),
			CardinalDirection.SouthEast => (1, -1),
			CardinalDirection.South     => (0, -1),
			CardinalDirection.SouthWest => (-1, -1),
			CardinalDirection.West      => (-1, 0),
			CardinalDirection.NorthWest => (-1, 1),
			_                           => throw new ArgumentOutOfRangeException(nameof(d), d, null)
		};

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (int dx, int dy) ToOffset(this CardinalDirection dir) => _OFFSETS[dir];
	}
}
