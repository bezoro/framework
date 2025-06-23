using System.Collections.Generic;

namespace Bezoro.UCI
{
	/// <summary>
	///     Defines a set of parameters for controlling an engine's search with the 'go' command.
	/// </summary>
	public class SearchParameters
	{
		/// <summary> Search indefinitely until a 'stop' command is sent. </summary>
		public bool Infinite { get; set; }
		/// <summary> Restrict search to a list of moves. </summary>
		public IEnumerable<string>? SearchMoves { get; set; }

		/// <summary> Time increment for black in milliseconds. </summary>
		public int? BlackIncrementMs { get; set; }

		/// <summary> Time remaining for black in milliseconds. </summary>
		public int? BlackTimeMs { get; set; }

		/// <summary> Search only to a specific depth. </summary>
		public int? Depth { get; set; }

		/// <summary> Search for a mate in a specific number of moves. </summary>
		public int? Mate { get; set; }

		/// <summary> Search for a fixed amount of time in milliseconds. </summary>
		public int? MoveTimeMs { get; set; }

		/// <summary> Time increment for white in milliseconds. </summary>
		public int? WhiteIncrementMs { get; set; }

		/// <summary> Time remaining for white in milliseconds. </summary>
		public int? WhiteTimeMs { get; set; }

		/// <summary> Search a fixed number of nodes. </summary>
		public long? Nodes { get; set; }
	}
}
