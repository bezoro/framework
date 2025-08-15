using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.UCI.API.Enums;
using Bezoro.UCI.API.Types;
using Bezoro.UCI.Domain.Common.Constants;
using Bezoro.UCI.Domain.Common.Extensions;
using Bezoro.UCI.Domain.Common.Helpers;

namespace Bezoro.UCI.Domain;

/// <summary>
///     Handles chess engine search operations.
/// </summary>
internal sealed class SearchService
{
	private readonly EngineCommandSender _commandSender;
	private readonly EngineOutputParser  _outputParser;

	/// <summary>
	///     Initializes a new instance of the <see cref="SearchService" /> class.
	/// </summary>
	/// <param name="commandSender">The engine command sender.</param>
	/// <param name="outputParser">The engine output parser.</param>
	public SearchService(EngineCommandSender commandSender, EngineOutputParser outputParser)
	{
		_commandSender = commandSender;
		_outputParser  = outputParser;
	}

	/// <summary>
	///     Performs an infinite analysis operation.
	/// </summary>
	/// <param name="onAnalysisUpdate">Callback for analysis updates.</param>
	/// <param name="ct">A token to cancel the operation.</param>
	public async Task StartAnalysisAsync(Action<EngineOutput> onAnalysisUpdate, CancellationToken ct = default)
	{
		var parameters = new SearchParameters { Infinite = true };
		await _commandSender.SendCommandAsync(GoCommandHelper.BuildGoCommand(parameters), true, ct);

		await foreach (var output in _outputParser.ReadEngineOutputAsync(parameters, ct))
		{
			if (output is { Type: EngineOutputType.Info, AnalysisInfo: not null })
				onAnalysisUpdate(output);
		}
	}

	/// <summary>
	///     Stops the current search operation.
	/// </summary>
	public async Task StopAnalysisAsync()
	{
		await _commandSender.SendCommandAsync(UciConstants.STOP_COMMAND);
	}

	/// <summary>
	///     Performs a search operation with the specified parameters.
	/// </summary>
	/// <param name="parameters">The search parameters.</param>
	/// <param name="ct">A token to cancel the operation.</param>
	public async Task<SearchResultOld> SearchAsync(SearchParameters parameters, CancellationToken ct = default)
	{
		var result    = new SearchResultOld();
		var stopwatch = Stopwatch.StartNew();

		try
		{
			string goCommand = GoCommandHelper.BuildGoCommand(parameters);
			await _commandSender.SendCommandAsync(goCommand, false, ct);
			await foreach (var output in _outputParser.ReadEngineOutputAsync(parameters, ct))
				result = output.ToSearchResult();
		}
		catch (OperationCanceledException)
		{
			await foreach (var output in _outputParser.ReadEngineOutputAsync(parameters, ct))
				result = output.ToSearchResult();

			result.WasStoppedEarly = true;
		}

		stopwatch.Stop();
		result.SearchTimeMs = stopwatch.ElapsedMilliseconds;
		return result;
	}
}
