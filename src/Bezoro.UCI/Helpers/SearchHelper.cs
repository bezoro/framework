using System.Linq;
using System.Text;
using System.Threading;
using Bezoro.UCI.API.Constants;
using Bezoro.UCI.Types;

namespace Bezoro.UCI.Helpers
{
	/// <summary>
	///     Helper class for UCI search-related functionality.
	/// </summary>
	internal static class SearchHelper
	{
		/// <summary>
		///     Builds a UCI 'go' command string from search parameters.
		/// </summary>
		public static string BuildGoCommand(SearchParameters parameters)
		{
			var commandBuilder = new StringBuilder(UCIConstants.GoCommand);

			if (parameters.SearchMoves?.Any() == true)
			{
				commandBuilder.Append($" {UCIConstants.SearchMovesParameter} ")
							  .Append(string.Join(" ", parameters.SearchMoves));
			}

			if (parameters.WhiteTimeMs.HasValue)
			{
				commandBuilder.Append($" {UCIConstants.WhiteTimeParameter} ").Append(parameters.WhiteTimeMs.Value);
			}

			if (parameters.BlackTimeMs.HasValue)
			{
				commandBuilder.Append($" {UCIConstants.BlackTimeParameter} ").Append(parameters.BlackTimeMs.Value);
			}

			if (parameters.WhiteIncrementMs.HasValue)
			{
				commandBuilder.Append($" {UCIConstants.WhiteTimeIncrementParameter} ")
							  .Append(parameters.WhiteIncrementMs.Value);
			}

			if (parameters.BlackIncrementMs.HasValue)
			{
				commandBuilder.Append($" {UCIConstants.BlackTimeIncrementParameter} ")
							  .Append(parameters.BlackIncrementMs.Value);
			}

			if (parameters.Depth.HasValue)
			{
				commandBuilder.Append($" {UCIConstants.DepthParameter} ").Append(parameters.Depth.Value);
			}

			if (parameters.Nodes.HasValue)
			{
				commandBuilder.Append($" {UCIConstants.NodesSearchParameter} ").Append(parameters.Nodes.Value);
			}

			if (parameters.Mate.HasValue)
			{
				commandBuilder.Append($" {UCIConstants.MateSearchParameter} ").Append(parameters.Mate.Value);
			}

			if (parameters.MoveTimeMs.HasValue)
			{
				commandBuilder.Append($" {UCIConstants.MoveTimeParameter} ").Append(parameters.MoveTimeMs.Value);
			}

			if (parameters.Infinite)
			{
				commandBuilder.Append($" {UCIConstants.InfiniteSearchParameter}");
			}

			return commandBuilder.ToString();
		}

		/// <summary>
		///     Creates a timeout cancellation token source based on search parameters.
		/// </summary>
		public static CancellationTokenSource? CreateTimeoutCtsForSearch(SearchParameters parameters)
		{
			if (parameters.Infinite)
			{
				return null;
			}

			// Add a 5-second buffer to the longest possible thinking time to allow for communication overhead.
			int timeoutMilliseconds = (parameters.MoveTimeMs ?? 0) + (parameters.WhiteTimeMs ?? 0) + 5000;
			return new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMilliseconds));
		}

		/// <summary>
		///     Extracts the best move from a UCI engine response.
		/// </summary>
		public static string? ParseBestMoveFromResponse(string bestMoveLine)
		{
			// Expected format is "bestmove <move> [ponder <move>]"
			string[] parts = bestMoveLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			return parts.Length > 1 ? parts[1] : null;
		}
	}
}
