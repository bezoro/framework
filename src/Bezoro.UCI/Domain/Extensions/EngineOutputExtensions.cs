using Bezoro.UCI.API.Types;

namespace Bezoro.UCI.Domain.Extensions
{
	internal static class EngineOutputExtensions
	{
		public static SearchResult ToSearchResult(this EngineOutput engineOutput)
		{
			// Initialize result and map best moves
			var result = new SearchResult
			{
				BestMove   = engineOutput.BestMove,
				PonderMove = engineOutput.PonderMove
			};

			// If there is analysis info, map its data
			if (!engineOutput.AnalysisInfo.HasValue)
			{
				return result;
			}

			var info = engineOutput.AnalysisInfo.Value;

			// Record the depth and performance metrics
			result.Depth        = info.Depth;
			result.SearchTimeMs = info.TimeMs ?? 0;
			result.TotalNodes   = info.Nodes;
			result.AverageNps   = info.Nps;

			// Add the raw analysis info for further inspection
			result.AnalysisInfo?.Add(info);

			// Determine a final score (centipawns or mate)
			if (info.ScoreCp.HasValue)
			{
				result.FinalScore = info.ScoreCp.Value;
			}
			else if (info.Mate.HasValue)
			{
				result.FinalScore = info.Mate.Value;
			}

			return result;
		}
	}
}
