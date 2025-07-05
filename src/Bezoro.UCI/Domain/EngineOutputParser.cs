using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.UCI.API.Enums;
using Bezoro.UCI.API.Types;
using Bezoro.UCI.Domain.Constants;
using Bezoro.UCI.Domain.Exceptions;
using Bezoro.UCI.Domain.Helpers;

namespace Bezoro.UCI.Domain
{
	/// <summary>
	///     Parses output from the chess engine.
	/// </summary>
	internal sealed class EngineOutputParser
	{
		private readonly EngineProcessManager _processManager;

		/// <summary>
		///     Fired when info output is received from the engine.
		/// </summary>
		public event EventHandler<SearchResult>? InfoReceived;

		/// <summary>
		///     Initializes a new instance of the <see cref="EngineOutputParser" /> class.
		/// </summary>
		/// <param name="processManager">The engine process manager.</param>
		public EngineOutputParser(EngineProcessManager processManager)
		{
			_processManager = processManager;
		}

		/// <summary>
		///     Parses a single line of engine output into a structured format.
		/// </summary>
		public EngineOutput ParseEngineOutput(string line)
		{
			var output = UCIParser.ParseLine(line);
			return output;
		}

		/// <summary>
		///     Reads and parses engine output during a search operation.
		/// </summary>
		public async IAsyncEnumerable<EngineOutput> ReadEngineOutputAsync(
			SearchParameters parameters, [EnumeratorCancellation] CancellationToken ct = default)
		{
			// Create timeout CTS if needed based on search parameters
			using var timeoutCts = GoCommandHelper.CreateTimeoutCtsForSearch(parameters);
			using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
				ct, timeoutCts?.Token ?? CancellationToken.None);

			while (!linkedCts.Token.IsCancellationRequested)
			{
				string? line = await ReadLineWithTimeoutAsync(linkedCts.Token, timeoutCts,
					parameters.MoveTimeMs ?? 5000);

				if (line == null)
				{
					throw new UCIException("Engine process exited unexpectedly during search.");
				}

				Logger.LogInfo($"<<UCI>>{line}");
				var output = ParseEngineOutput(line);
				yield return output;

				if (output.Type == EngineOutputType.BestMove)
				{
					yield break;
				}
			}
		}

		/// <summary>
		///     Reads a line from the process output.
		/// </summary>
		/// <param name="ct">A token to cancel the operation.</param>
		public async Task<string?> ReadProcessOutputAsync(CancellationToken ct)
		{
			string? line = await _processManager.ReadLineAsync(ct);
			if (line != null)
			{
				Logger.LogInfo($"<<UCI>>{line}");
			}

			return line;
		}

		/// <summary>
		///     Processes a single line of engine output.
		/// </summary>
		/// <param name="line">The line to process.</param>
		public void ProcessGenericEngineOutput(string line)
		{
			if (line.StartsWith(UCIConstants.InfoCommand, StringComparison.OrdinalIgnoreCase))
			{
				var output = UCIParser.ParseLine(line);
				if (output.AnalysisInfo.HasValue)
				{
					InfoReceived?.Invoke(this, new SearchResult { AnalysisInfo = { output.AnalysisInfo.Value } });
				}
			}
		}

		private async Task<string?> ReadLineWithTimeoutAsync(
			CancellationToken linkedToken, CancellationTokenSource? timeoutCts, int moveTimeMs)
		{
			try
			{
				return await _processManager.ReadLineAsync(linkedToken);
			}
			catch (OperationCanceledException) when (timeoutCts?.Token.IsCancellationRequested == true)
			{
				throw new TimeoutException($"Search timed out after {moveTimeMs}ms");
			}
		}
	}
}
