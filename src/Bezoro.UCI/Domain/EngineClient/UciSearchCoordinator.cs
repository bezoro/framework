using System.Threading;
using System.Threading.Tasks;
using Bezoro.UCI.API.Types;
using Bezoro.UCI.Domain.Common.Constants;

namespace Bezoro.UCI.Domain.EngineClient;

/// <summary>
///     Coordinates UCI search sessions, handling lifecycle, captured output lines, and bestmove completion.
/// </summary>
internal sealed class UciSearchCoordinator(
	IUciTransport               transport,
	Action<EngineActivity>      setActivity,
	Action<PrincipalVariation>? pvObserver,
	Action<string, string>?     bestMoveObserver
)
{
	private readonly Action<EngineActivity> _setActivity =
		setActivity ?? throw new ArgumentNullException(nameof(setActivity));

	private readonly Func<string, bool> _bestMovePredicate =
		static line => line.StartsWith($"{UciConstants.Prefixes.BEST_MOVE} ", StringComparison.OrdinalIgnoreCase);
	private readonly IUciTransport _transport = transport ?? throw new ArgumentNullException(nameof(transport));

	private volatile SearchSession? _activeSession;

	public Task<SearchResult> ExecuteSearchAsync(SearchParameters parameters, CancellationToken ct) =>
		ExecuteSearchInternalAsync(parameters, ct);

	public void HandleBestMoveLine(string line)
	{
		var session = _activeSession;

		if (session is { })
		{
			session.AddLine(line);
			session.CompleteBestMove(line);
			Interlocked.CompareExchange(ref _activeSession, null, session);
		}

		_setActivity(EngineActivity.Idle);

		if (BestMoveLine.TryParse(line, out var bestMove))
		{
			bestMoveObserver?.Invoke(bestMove.BestMove, bestMove.PonderMove ?? string.Empty);
			return;
		}

		bestMoveObserver?.Invoke(string.Empty, string.Empty);
	}

	public void HandleInfoLine(string line)
	{
		if (PrincipalVariation.TryParse(line, out var pv))
			pvObserver?.Invoke(pv);

		_activeSession?.AddLine(line);
	}

	public void HandleTransportTerminated()
	{
		var session = Interlocked.Exchange(ref _activeSession, null);
		session?.BestMoveCompletion.TrySetCanceled();
	}

	private static TimeSpan ComputeTimeout(SearchParameters parameters)
	{
		if (parameters.MoveTimeMs is { } mt)
		{
			long buffered                = (long)mt + 750;
			if (buffered < 500) buffered = 500;

			double capped = Math.Min(buffered, TimeSpan.MaxValue.TotalMilliseconds);
			return TimeSpan.FromMilliseconds(capped);
		}

		if (parameters.Infinite) return TimeSpan.FromSeconds(120);
		if (parameters.Depth is not { } depth) return TimeSpan.FromSeconds(parameters.Nodes is { } ? 60 : 30);

		long seconds = Math.Clamp(depth * 2L, 10L, 90L);
		return TimeSpan.FromSeconds(seconds);
	}

	private async Task IssueStopBestEffortAsync()
	{
		try
		{
			await _transport.WriteLineAsync(UciConstants.Commands.STOP, CancellationToken.None).ConfigureAwait(false);
		}
		catch
		{
			/* best-effort */
		}
	}

	private async Task<SearchResult> ExecuteSearchInternalAsync(SearchParameters parameters, CancellationToken ct)
	{
		string cmd     = UciCommandBuilder.BuildGoCommand(parameters);
		var    session = new SearchSession(parameters.Ponder);

		if (Interlocked.CompareExchange(ref _activeSession, session, null) is { })
		{
			session.BestMoveCompletion.TrySetCanceled();
			throw new InvalidOperationException("A search is already in progress.");
		}

		var     timeout  = ComputeTimeout(parameters);
		string? bestLine = null;

		try
		{
			await _transport.WriteLineAsync(cmd, ct).ConfigureAwait(false);
			_setActivity(parameters.Ponder ? EngineActivity.Pondering : EngineActivity.Searching);

			var bestTask = session.BestMoveCompletion.Task;
			var timeoutTask = timeout == Timeout.InfiniteTimeSpan
								  ? Task.Delay(Timeout.Infinite, CancellationToken.None)
								  : Task.Delay(timeout);

			var       cancelTcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
			using var reg = ct.Register(static s => ((TaskCompletionSource<object?>)s!).TrySetResult(null), cancelTcs);

			var completed = await Task.WhenAny(bestTask, timeoutTask, cancelTcs.Task).ConfigureAwait(false);

			if (completed == cancelTcs.Task)
				throw new OperationCanceledException(ct);

			if (completed == bestTask)
			{
				bestLine = await bestTask.ConfigureAwait(false);
			}
			else
			{
				await IssueStopBestEffortAsync().ConfigureAwait(false);
				bestLine = await AwaitBestMoveGracefullyAsync(session, bestTask, ct).ConfigureAwait(false);
			}
		}
		finally
		{
			if (ReferenceEquals(_activeSession, session))
				Interlocked.CompareExchange(ref _activeSession, null, session);

			if (!session.BestMoveCompletion.Task.IsCompleted)
				session.BestMoveCompletion.TrySetCanceled();

			_setActivity(EngineActivity.Idle);
		}

		if (bestLine is { })
			session.AddLine(bestLine);

		var snapshot = session.SnapshotLines();
		return SearchResult.TryParse(snapshot, out var result) ? result : default;
	}

	private async Task<string?> AwaitBestMoveGracefullyAsync(
		SearchSession     session,
		Task<string>      bestTask,
		CancellationToken ct)
	{
		try
		{
			var grace = await Task.WhenAny(bestTask, Task.Delay(TimeSpan.FromSeconds(2), ct)).ConfigureAwait(false);

			ct.ThrowIfCancellationRequested();

			if (grace == bestTask)
				return await bestTask.ConfigureAwait(false);

			string? captured = session.FindFirstLine(_bestMovePredicate);
			if (captured is null)
				throw new TimeoutException("Engine search timed out without emitting 'bestmove'.");

			return captured;
		}
		catch (OperationCanceledException) when (!ct.IsCancellationRequested)
		{
			string? captured = session.FindFirstLine(_bestMovePredicate);
			if (captured is null) throw;

			return captured;
		}
	}
}
