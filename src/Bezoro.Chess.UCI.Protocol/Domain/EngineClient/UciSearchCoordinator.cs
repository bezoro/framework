using System.Threading;
using System.Threading.Tasks;
using Bezoro.Chess.UCI.Protocol.Domain.Common.Constants;

namespace Bezoro.Chess.UCI.Protocol.Domain.EngineClient;

/// <summary>
///     Coordinates UCI search sessions, handling lifecycle, captured output lines, and bestmove completion.
/// </summary>
internal sealed class UciSearchCoordinator(
	IUciTransport               transport,
	Action<EngineActivity>      setActivity,
	UciClientOptions            options
)
{
	private readonly Action<EngineActivity> _setActivity =
		setActivity ?? throw new ArgumentNullException(nameof(setActivity));
	private readonly UciClientOptions _options = options;

	private readonly IUciTransport _transport = transport ?? throw new ArgumentNullException(nameof(transport));

	private volatile SearchSession? _activeSession;

	public Task<SearchResult> ExecuteSearchAsync(SearchParameters parameters, CancellationToken ct) =>
		ExecuteSearchInternalAsync(parameters, ct);

	public void HandleBestMove(UciBestMoveMessage message)
	{
		var session = _activeSession;

		if (session is { })
		{
			session.CompleteBestMove(message);
			Interlocked.CompareExchange(ref _activeSession, null, session);
		}

		_setActivity(EngineActivity.Idle);
	}

	public void HandleInfoLine(string line, PrincipalVariation? principalVariation = null)
	{
		if (principalVariation.HasValue)
			_activeSession?.RecordInfo(principalVariation.Value);
	}

	public void HandleTransportTerminated()
	{
		var session = Interlocked.Exchange(ref _activeSession, null);
		session?.BestMoveCompletion.TrySetCanceled();
	}

	private TimeSpan ComputeTimeout(SearchParameters parameters)
	{
		if (parameters.MoveTimeMs is { } mt)
		{
			double buffered = mt + _options.MoveTimeBuffer.TotalMilliseconds;
			double minimum  = _options.MinimumMoveTimeTimeout.TotalMilliseconds;
			if (buffered < minimum) buffered = minimum;

			double capped = Math.Min(buffered, TimeSpan.MaxValue.TotalMilliseconds);
			return TimeSpan.FromMilliseconds(capped);
		}

		if (parameters.Infinite) return _options.InfiniteSearchTimeout;
		if (parameters.Depth is not { } depth)
			return parameters.Nodes is { }
					   ? _options.NodeSearchTimeout
					   : _options.DefaultSearchTimeout;

		long seconds = Math.Clamp(
			depth * (long)_options.DepthTimeoutSecondsPerPly,
			(long)_options.MinimumDepthSearchTimeout.TotalSeconds,
			(long)_options.MaximumDepthSearchTimeout.TotalSeconds
		);
		return TimeSpan.FromSeconds(seconds);
	}

	private async Task DrainSearchAfterStopAsync(SearchSession session, Task<UciBestMoveMessage> bestTask)
	{
		try
		{
			_ = await AwaitBestMoveGracefullyAsync(session, bestTask, CancellationToken.None).ConfigureAwait(false);
		}
		catch (TimeoutException)
		{
			// Cancellation still needs to unwind even if the engine never reports bestmove after stop.
		}
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

		var     timeout        = ComputeTimeout(parameters);
		UciBestMoveMessage? bestMove = null;
		var     callerCanceled = false;

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
			{
				callerCanceled = true;
				await IssueStopBestEffortAsync().ConfigureAwait(false);
				await DrainSearchAfterStopAsync(session, bestTask).ConfigureAwait(false);
			}

			else if (completed == bestTask)
			{
				bestMove = await bestTask.ConfigureAwait(false);
			}
			else
			{
				await IssueStopBestEffortAsync().ConfigureAwait(false);
				try
				{
					bestMove = await AwaitBestMoveGracefullyAsync(session, bestTask, ct).ConfigureAwait(false);
				}
				catch (TimeoutException)
				{
					bestMove = null;
				}
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

		if (callerCanceled)
			throw new OperationCanceledException(ct);

		if (bestMove is { } resolvedBestMove)
			return session.BuildResult(resolvedBestMove);

		throw new InvalidOperationException(
			"Engine search completed with malformed output: missing or invalid 'bestmove' line."
		);
	}

	private async Task<UciBestMoveMessage?> AwaitBestMoveGracefullyAsync(
		SearchSession     session,
		Task<UciBestMoveMessage> bestTask,
		CancellationToken ct)
	{
		try
		{
			var delay = _options.SearchStopGracePeriod > TimeSpan.Zero
							? _options.SearchStopGracePeriod
							: TimeSpan.FromSeconds(2);
			var grace = await Task.WhenAny(bestTask, Task.Delay(delay, ct)).ConfigureAwait(false);

			ct.ThrowIfCancellationRequested();

			if (grace == bestTask)
				return await bestTask.ConfigureAwait(false);

			UciBestMoveMessage? captured = session.BestMove;
			if (!captured.HasValue)
				throw new TimeoutException("Engine search timed out without emitting 'bestmove'.");

			return captured;
		}
		catch (OperationCanceledException) when (!ct.IsCancellationRequested)
		{
			UciBestMoveMessage? captured = session.BestMove;
			if (!captured.HasValue) throw;

			return captured;
		}
	}
}
