using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Bezoro.UCI.API.Types;
using Bezoro.UCI.Domain.Constants;

namespace Bezoro.UCI.Domain.Helpers
{
	/// <summary>
	///     Provides helper methods for building and parsing UCI 'go' commands, managing search timeouts,
	///     and handling search-related functionality. This class contains pure functions for working
	///     with search parameters and UCI protocol commands.
	/// </summary>
	internal static class GoCommandHelper
	{
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
		///     Builds a UCI 'go' command string from search parameters.
		/// </summary>
		public static string BuildGoCommand(SearchParameters parameters)
		{
			var commandBuilder = new StringBuilder(UCIConstants.GoCommand);

			AppendSearchMovesIfPresent(commandBuilder, parameters.SearchMoves);

			AppendOptionalParameter(commandBuilder, UCIConstants.WhiteTimeParameter, parameters.WhiteTimeMs);
			AppendOptionalParameter(commandBuilder, UCIConstants.BlackTimeParameter, parameters.BlackTimeMs);
			AppendOptionalParameter(commandBuilder, UCIConstants.WhiteTimeIncrementParameter,
				parameters.WhiteIncrementMs);

			AppendOptionalParameter(commandBuilder, UCIConstants.BlackTimeIncrementParameter,
				parameters.BlackIncrementMs);

			AppendOptionalParameter(commandBuilder, UCIConstants.DepthParameter,       parameters.Depth);
			AppendOptionalParameter(commandBuilder, UCIConstants.NodesSearchParameter, (int?)parameters.Nodes);
			AppendOptionalParameter(commandBuilder, UCIConstants.MateSearchParameter,  parameters.Mate);
			AppendOptionalParameter(commandBuilder, UCIConstants.MoveTimeParameter,    parameters.MoveTimeMs);
			AppendOptionalParameter(commandBuilder, UCIConstants.MovesToGoParameter,   parameters.MovesToGo);

			AppendFlagParameter(commandBuilder, UCIConstants.InfiniteSearchParameter, parameters.Infinite);
			AppendFlagParameter(commandBuilder, UCIConstants.PonderParameter,         parameters.Ponder);

			return commandBuilder.ToString();
		}

		private static void AppendFlagParameter(StringBuilder builder, string parameterName, bool isSet)
		{
			if (isSet)
			{
				builder.Append($" {parameterName}");
			}
		}

		private static void AppendOptionalParameter(StringBuilder builder, string parameterName, int? value)
		{
			if (value.HasValue)
			{
				builder.Append($" {parameterName} ").Append(value.Value);
			}
		}

		private static void AppendSearchMovesIfPresent(StringBuilder builder, IEnumerable<string>? searchMoves)
		{
			if (searchMoves?.Any() == true)
			{
				builder.Append($" {UCIConstants.SearchMovesParameter} ").Append(string.Join(" ", searchMoves));
			}
		}
	}
}
