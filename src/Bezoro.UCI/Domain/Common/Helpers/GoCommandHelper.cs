using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Bezoro.UCI.API.Types;
using Bezoro.UCI.Domain.Common.Constants;

namespace Bezoro.UCI.Domain.Common.Helpers;

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
		if (parameters.Infinite) return null;

		// Add a 5-second buffer to the longest possible thinking time to allow for communication overhead.
		int timeoutMilliseconds = (parameters.MoveTimeMs ?? 0) + (parameters.WhiteTimeMs ?? 0) + 5000;
		return new(TimeSpan.FromMilliseconds(timeoutMilliseconds));
	}

	/// <summary>
	///     Builds a UCI 'go' command string from search parameters.
	/// </summary>
	public static string BuildGoCommand(SearchParameters parameters)
	{
		var commandBuilder = new StringBuilder(UciConstants.GO_COMMAND);

		AppendSearchMovesIfPresent(commandBuilder, parameters.SearchMoves);

		AppendOptionalParameter(commandBuilder, UciConstants.WHITE_TIME_PARAMETER, parameters.WhiteTimeMs);
		AppendOptionalParameter(commandBuilder, UciConstants.BLACK_TIME_PARAMETER, parameters.BlackTimeMs);
		AppendOptionalParameter(
			commandBuilder,
			UciConstants.WHITE_TIME_INCREMENT_PARAMETER,
			parameters.WhiteIncrementMs);

		AppendOptionalParameter(
			commandBuilder,
			UciConstants.BLACK_TIME_INCREMENT_PARAMETER,
			parameters.BlackIncrementMs);

		AppendOptionalParameter(commandBuilder, UciConstants.DEPTH_PARAMETER,        (int)parameters.Depth!);
		AppendOptionalParameter(commandBuilder, UciConstants.NODES_SEARCH_PARAMETER, (int?)parameters.Nodes);
		AppendOptionalParameter(commandBuilder, UciConstants.MATE_SEARCH_PARAMETER,  parameters.Mate);
		AppendOptionalParameter(commandBuilder, UciConstants.MOVE_TIME_PARAMETER,    parameters.MoveTimeMs);
		AppendOptionalParameter(commandBuilder, UciConstants.MOVES_TO_GO_PARAMETER,  parameters.MovesToGo);

		AppendFlagParameter(commandBuilder, UciConstants.INFINITE_SEARCH_PARAMETER, parameters.Infinite);
		AppendFlagParameter(commandBuilder, UciConstants.PONDER_PARAMETER,          parameters.Ponder);

		return commandBuilder.ToString();
	}

	private static void AppendFlagParameter(StringBuilder builder, string parameterName, bool isSet)
	{
		if (isSet) builder.Append($" {parameterName}");
	}

	private static void AppendOptionalParameter(StringBuilder builder, string parameterName, int? value)
	{
		if (value.HasValue) builder.Append($" {parameterName} ").Append(value.Value);
	}

	private static void AppendSearchMovesIfPresent(StringBuilder builder, IEnumerable<string>? searchMoves)
	{
		if (searchMoves?.Any() == true)
			builder.Append($" {UciConstants.SEARCH_MOVES_PARAMETER} ").Append(string.Join(" ", searchMoves));
	}
}
