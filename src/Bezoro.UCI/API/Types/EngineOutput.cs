// First, let's create the necessary types for the search results

using System.Collections.Generic;

namespace Bezoro.UCI.Types
{
    /// <summary>
    ///     Represents the complete result of an engine search operation.
    /// </summary>
    public record struct SearchResult
	{
		public SearchResult()
		{
			BestMove        = null;
			PonderMove      = null;
			FinalScore      = null;
			Depth           = null;
			SearchTimeMs    = 0;
			WasStoppedEarly = false;
		}

        /// <summary>
        ///     The best move found by the engine in UCI format (e.g., "e2e4").
        /// </summary>
        public string? BestMove { get; set; }

        /// <summary>
        ///     The suggested ponder move (opponent's expected response).
        /// </summary>
        public string? PonderMove { get; set; }

        /// <summary>
        ///     Collection of all analysis info received during the search.
        /// </summary>
        public List<EngineAnalysisEventArgs> AnalysisInfo { get; } = new();

        /// <summary>
        ///     The final evaluation score from the engine's perspective.
        /// </summary>
        public int? FinalScore { get; set; }

        /// <summary>
        ///     The search depth reached.
        /// </summary>
        public int? Depth { get; set; }

        /// <summary>
        ///     Time taken for the search in milliseconds.
        /// </summary>
        public long SearchTimeMs { get; set; }

        /// <summary>
        ///     Whether the search was stopped before completion.
        /// </summary>
        public bool WasStoppedEarly { get; set; }
	}

    /// <summary>
    ///     Represents different types of engine output during search.
    /// </summary>
    public enum EngineOutputType
	{
		Info,
		BestMove,
		Unknown
	}

    /// <summary>
    ///     Represents a single line of engine output during search.
    /// </summary>
    public class EngineOutput
	{
		public EngineOutputType         Type       { get; set; }
		public string                   RawLine    { get; set; } = string.Empty;
		public EngineAnalysisEventArgs? InfoData   { get; set; }
		public string?                  BestMove   { get; set; }
		public string?                  PonderMove { get; set; }
	}
}
