using System;
using System.Collections.Generic;

namespace Bezoro.UCI
{
	/// <summary>
	///     Provides data for the InfoReceived event.
	///     Contains parsed information from a UCI 'info' string.
	/// </summary>
	public class EngineAnalysisEventArgs : EventArgs
	{
		/// <summary> Search depth in plies. </summary>
		public int? Depth { get; set; }

		/// <summary> The evaluation is a mate in X moves. </summary>
		public int? Mate { get; set; }

		/// <summary> The evaluation score from the engine's point of view in centipawns. </summary>
		public int? ScoreCp { get; set; }

		/// <summary> The principal variation (the best line of moves found). </summary>
		public IReadOnlyList<string> PrincipalVariation { get; set; }

		/// <summary> The number of nodes searched. </summary>
		public long? Nodes { get; set; }

		/// <summary> The search speed in nodes per second. </summary>
		public long? Nps { get; set; }
		/// <summary> The raw 'info' string from the engine. </summary>
		public string RawInfo { get; set; }
	}
}
