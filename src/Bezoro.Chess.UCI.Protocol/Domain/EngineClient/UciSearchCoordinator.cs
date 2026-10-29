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
	private int _requiresRestart;

	public Task<SearchResult> ExecuteSearchAsync(SearchParameters parameters, CancellationToken ct) =>
		ExecuteSearchInternalAsync(parameters, ct);

	public void HandleBestMove(UciBestMoveMessage message)
	{
		var session = _activeSession;
		_setActivity(EngineActivity.Idle);
		if (session is null) return;
		session.CompleteBestMove(message);
		if (session.IsFireAndForget)
			Interlocked.CompareExchange(ref _activeSession, null, session);
	}

	public async Task ExecuteFireAndForgetAsync(SearchParameters parameters, CancellationToken ct)
	{
		ct.ThrowIfCancellationRequested();
		EnsureSynchronized();
		string command = UciCommandBuilder.BuildGoCommand(parameters);
		var session = new SearchSession(parameters.Ponder, fireAndForget: true);
		if (Interlocked.CompareExchange(ref _activeSession, session, null) is not null)
			throw new InvalidOperationException("A search is already in progress.");

		try
		{
			_setActivity(parameters.Ponder ? EngineActivity.Pondering : EngineActivity.Searching);
			await _transport.WriteLineAsync(command, ct).ConfigureAwait(false);
		}
		catch
		{
			_setActivity(EngineActivity.Idle);
			Interlocked.CompareExchange(ref _activeSession, null, session);
			throw;
		}
	}

	public async Task StopSearchAsync(CancellationToken ct)
	{
		var session = _activeSession;
		await _transport.WriteLineAsync(UciConstants.Commands.STOP, ct).ConfigureAwait(false);
		if (session is null)
		{
			_setActivity(EngineActivity.Idle);
			return;
		}

		try
		{
			await AwaitBestMoveGracefullyAsync(session, session.BestMoveCompletion.Task, CancellationToken.None).ConfigureAwait(false);
			ct.ThrowIfCancellationRequested();
		}
		catch (TimeoutException)
		{
			Interlocked.Exchange(ref _requiresRestart, 1);
			throw;
		}
		finally
		{
			if (session.IsFireAndForget && ReferenceEquals(_activeSession, session))
			{
				_setActivity(EngineActivity.Idle);
				Interlocked.CompareExchange(ref _activeSession, null, session);
			}
		}
	}

	private void EnsureSynchronized()
	{
		if (Volatile.Read(ref _requiresRestart) != 0)
			throw new InvalidOperationException("The engine did not acknowledge stop with bestmove. Stop and restart the client before searching again.");
	}

	public void HandleInfo(UciInfoPayload info) => _activeSession?.RecordInfo(info);

	public void HandleTransportTerminated()
	{
		Interlocked.Exchange(ref _requiresRestart, 0);
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
			// UCI has no request IDs. A delayed bestmove must never satisfy the next search.
			Interlocked.Exchange(ref _requiresRestart, 1);
		}
	}

	private async Task IssueStopBestEffortAsync()
	{
		try
		{
			using var stopCts = new CancellationTokenSource(_options.SearchStopGracePeriod);
			await _transport.WriteLineAsync(UciConstants.Commands.STOP, stopCts.Token).ConfigureAwait(false);
		}
		catch
		{
			/* best-effort */
		}
	}

	private async Task<SearchResult> ExecuteSearchInternalAsync(SearchParameters parameters, CancellationToken ct)
	{
		ct.ThrowIfCancellationRequested();
		EnsureSynchronized();

		var timeout = ComputeTimeout(parameters);
		string cmd     = UciCommandBuilder.BuildGoCommand(parameters);
		var    session = new SearchSession(parameters.Ponder);

		if (Interlocked.CompareExchange(ref _activeSession, session, null) is { })
		{
			session.BestMoveCompletion.TrySetCanceled();
			throw new InvalidOperationException("A search is already in progress.");
		}

		using var timerCts = new CancellationTokenSource();
		UciBestMoveMessage? bestMove = null;
		var     callerCanceled = false;

		try
		{
			_setActivity(parameters.Ponder ? EngineActivity.Pondering : EngineActivity.Searching);
			await _transport.WriteLineAsync(cmd, ct).ConfigureAwait(false);

			var bestTask = session.BestMoveCompletion.Task;
			var timeoutTask = timeout == Timeout.InfiniteTimeSpan
								  ? Task.Delay(Timeout.Infinite, timerCts.Token)
								  : Task.Delay(timeout, timerCts.Token);

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
					bestMove = await AwaitBestMoveGracefullyAsync(session, bestTask, CancellationToken.None).ConfigureAwait(false);
					callerCanceled = ct.IsCancellationRequested;
				}
				catch (TimeoutException)
				{
					bestMove = null;
					callerCanceled = ct.IsCancellationRequested;
					Interlocked.Exchange(ref _requiresRestart, 1);
				}
			}
		}
		finally
		{
			timerCts.Cancel();
			if (ReferenceEquals(_activeSession, session))
			{
				_setActivity(EngineActivity.Idle);
				Interlocked.CompareExchange(ref _activeSession, null, session);
			}

			if (!session.BestMoveCompletion.Task.IsCompleted)
				session.BestMoveCompletion.TrySetCanceled();
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
			using var graceCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
			var grace = await Task.WhenAny(bestTask, Task.Delay(delay, graceCts.Token)).ConfigureAwait(false);
			graceCts.Cancel();

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
