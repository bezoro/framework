using System.Collections.Generic;

namespace Bezoro.UCI.API.Types
{
	/// <summary>
	///     Defines a set of parameters for controlling an engine's search with the 'go' command.
	/// </summary>
	public record struct SearchParameters
	{
		/// <summary> Search indefinitely until a 'stop' command is sent. </summary>
		public bool Infinite { get; init; }

		public bool Ponder { get; init; }
		/// <summary> Restrict search to a list of moves. </summary>
		public IEnumerable<string>? SearchMoves { get; init; }

		/// <summary> Time increment for black in milliseconds. </summary>
		public int? BlackIncrementMs { get; init; }

		/// <summary> Time remaining for black in milliseconds. </summary>
		public int? BlackTimeMs { get; init; }

		/// <summary> Search only to a specific depth. </summary>
		public int? Depth { get; init; }

		/// <summary> Search for a mate in a specific number of moves. </summary>
		public int? Mate { get; init; }

		public int? MovesToGo { get; init; }

		/// <summary> Search for a fixed amount of time in milliseconds. </summary>
		public int? MoveTimeMs { get; init; }

		/// <summary> Time increment for white in milliseconds. </summary>
		public int? WhiteIncrementMs { get; init; }

		/// <summary> Time remaining for white in milliseconds. </summary>
		public int? WhiteTimeMs { get; init; }

		/// <summary> Search a fixed number of nodes. </summary>
		public long? Nodes { get; init; }
	}
}
