using System.Collections.Generic;

namespace Bezoro.UCI.Types
{
	/// <summary>
	///     Provides data for the InfoReceived event.
	///     Contains parsed information from a UCI 'info' string.
	/// </summary>
	public class EngineAnalysisEventArgs(string rawInfo) : EventArgs
	{
		/// <summary> The raw 'info' string from the engine. </summary>
		public string RawInfo { get; } = rawInfo;

		/// <summary> Search depth in plies. </summary>
		public int? Depth { get; internal set; }

		/// <summary> The evaluation is a mate in X moves. </summary>
		public int? Mate { get; internal set; }

		/// <summary> The evaluation score from the engine's point of view in centipawns. </summary>
		public int? ScoreCp { get; internal set; }

		/// <summary> The principal variation (the best line of moves found). </summary>
		public IReadOnlyList<string>? PrincipalVariation { get; internal set; }

		/// <summary> The number of nodes searched. </summary>
		public long? Nodes { get; internal set; }

		/// <summary> The search speed in nodes per second. </summary>
		public long? Nps { get; internal set; }

		public override string ToString() =>
			$"Depth: {Depth}, Mate: {Mate}, ScoreCp: {ScoreCp}, Nodes: {Nodes}, Nps: {Nps}";
	}
}
