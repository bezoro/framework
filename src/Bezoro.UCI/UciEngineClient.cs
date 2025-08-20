using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.UCI.API.Types;

namespace Bezoro.UCI;

internal sealed class UciEngineClient : IAsyncDisposable
{
	private static readonly Regex UciMoveRegex = new(
		@"^[a-h][1-8][a-h][1-8][qrbn]?$",
		RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private readonly ConcurrentDictionary<Guid, (Func<string, bool> predicate, TaskCompletionSource<string> tcs)>
		_waiters = new();

	private readonly IUciTransport _transport;

	private CancellationTokenSource? _cts;
	private Task?                    _readerTask;

	public event Action<string, string>? BestMoveReceived;

	public event Action<PrincipalVariation>? InfoPvReceived;

	public event Action<string>? LineReceived;

	public UciEngineClient(IUciTransport transport)
	{
		_transport = transport ?? throw new ArgumentNullException(nameof(transport));
	}

	public bool IsHealthy => _transport.IsHealthy;

	public bool IsStarted => _transport.IsStarted;

	public ProcessUciTransport.TransportStatus Status => _transport.Status;

	public async Task GoFireAndForgetAsync(SearchParameters parameters, CancellationToken ct)
	{
		string cmd = BuildGoCommand(parameters);
		await _transport.WriteLineAsync(cmd, ct).ConfigureAwait(false);
	}

	public async Task IsReadyAsync(CancellationToken ct)
	{
		await _transport.WriteLineAsync("isready", ct).ConfigureAwait(false);
		await WaitForLineAsync(
			l => l.Trim().Equals("readyok", StringComparison.OrdinalIgnoreCase),
			TimeSpan.FromSeconds(5),
			ct).ConfigureAwait(false);
	}

	public async Task SetOptionAsync(string name, string? value, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(name)) return;

		string cmd = value is null ? $"setoption name {name}" : $"setoption name {name} value {value}";
		await _transport.WriteLineAsync(cmd, ct).ConfigureAwait(false);
	}

	public async Task SetPositionAsync(Fen fen, IEnumerable<string>? moves, CancellationToken ct)
	{
		if (!Fen.Validate(fen.Raw))
			throw new ArgumentException("Invalid FEN provided.", nameof(fen));

		string movePart = moves is { } m && m.Any() ? " moves " + string.Join(' ', m) : string.Empty;
		await _transport.WriteLineAsync($"position fen {fen.Raw}{movePart}", ct).ConfigureAwait(false);
	}

	public async Task StartAsync(CancellationToken ct = default)
	{
		await _transport.StartAsync(ct).ConfigureAwait(false);

		_cts        = new();
		_readerTask = Task.Run(ReadLoopAsync);

		await UciInitAsync(ct).ConfigureAwait(false);
	}

	public async Task StopAsync(CancellationToken ct = default)
	{
		try
		{
			_cts?.Cancel();
			if (_readerTask is { })
				try
				{
					await _readerTask.ConfigureAwait(false);
				}
				catch
				{
					/* best-effort */
				}
		}
		finally
		{
			_readerTask = null;
			_cts?.Dispose();
			_cts = null;
		}

		await _transport.StopAsync(ct).ConfigureAwait(false);
	}

	public async Task StopSearchAsync(CancellationToken ct)
	{
		await _transport.WriteLineAsync("stop", ct).ConfigureAwait(false);
	}

	// High-level UCI operations

	public async Task UciInitAsync(CancellationToken ct)
	{
		await _transport.WriteLineAsync("uci", ct).ConfigureAwait(false);
		await WaitForLineAsync(
			l => l.Trim().Equals("uciok", StringComparison.OrdinalIgnoreCase),
			TimeSpan.FromSeconds(5),
			ct).ConfigureAwait(false);

		await IsReadyAsync(ct).ConfigureAwait(false);
	}

	public async Task UciNewGameAsync(CancellationToken ct)
	{
		await _transport.WriteLineAsync("ucinewgame", ct).ConfigureAwait(false);
		await IsReadyAsync(ct).ConfigureAwait(false);
	}

	public async Task<Fen?> GetFenViaDAsync(CancellationToken ct)
	{
		await _transport.WriteLineAsync("d", ct).ConfigureAwait(false);

		string? fenLine = await WaitForLineAsync(
							  l => Fen.TryParseUciOutputLine(l, out _),
							  TimeSpan.FromSeconds(2),
							  ct).ConfigureAwait(false);

		return Fen.TryParseUciOutputLine(fenLine, out var fen) ? fen : null;
	}

	public async Task<IReadOnlyList<string>> GetLegalMovesViaGoPerft1Async(CancellationToken ct)
	{
		var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		void CaptureMoves(string l)
		{
			foreach (string token in l.Split(
						 new[] { ' ', '\t', ',', ';', '|', ':' },
						 StringSplitOptions.RemoveEmptyEntries))
			{
				if (UciMoveRegex.IsMatch(token))
					results.Add(token.ToLowerInvariant());
			}
		}

		LineReceived += CaptureMoves;
		try
		{
			await _transport.WriteLineAsync("goperft 1", ct).ConfigureAwait(false);
			try
			{
				await Task.Delay(300, ct).ConfigureAwait(false);
			}
			catch
			{
				/* ignore */
			}

			if (results.Count == 0 && !ct.IsCancellationRequested)
			{
				await _transport.WriteLineAsync("go perft 1", ct).ConfigureAwait(false);
				try
				{
					await Task.Delay(300, ct).ConfigureAwait(false);
				}
				catch
				{
					/* ignore */
				}
			}
		}
		finally
		{
			LineReceived -= CaptureMoves;
		}

		return results.ToList();
	}

	public async Task<SearchResult> GoAsync(SearchParameters parameters, CancellationToken ct)
	{
		string cmd   = BuildGoCommand(parameters);
		var    lines = new List<string>(64);

		void Capture(string l)
		{
			if (l.StartsWith("info ",     StringComparison.OrdinalIgnoreCase) ||
				l.StartsWith("bestmove ", StringComparison.OrdinalIgnoreCase))
				lines.Add(l);
		}

		LineReceived += Capture;
		try
		{
			await _transport.WriteLineAsync(cmd, ct).ConfigureAwait(false);
			string? bestLine = await WaitForLineAsync(
								   l => l.StartsWith("bestmove ", StringComparison.OrdinalIgnoreCase),
								   TimeSpan.FromMinutes(5),
								   ct).ConfigureAwait(false);

			if (!lines.Contains(bestLine)) lines.Add(bestLine);

			return SearchResult.TryParse(lines, out var result) ? result : default;
		}
		finally
		{
			LineReceived -= Capture;
		}
	}

	public async ValueTask DisposeAsync()
	{
		try
		{
			await StopAsync(CancellationToken.None).ConfigureAwait(false);
		}
		catch
		{
			/* best-effort */
		}

		if (_transport is IAsyncDisposable ad) await ad.DisposeAsync();
	}

	private static string BuildGoCommand(SearchParameters parameters)
	{
		var parts = new List<string> { "go" };

		if (parameters.Ponder) parts.Add("ponder");
		if (parameters.Infinite) parts.Add("infinite");

		if (parameters.SearchMoves is { } sm)
		{
			var legal = sm.Where(s => UciMoveRegex.IsMatch(s));
			if (legal.Any()) parts.Add("searchmoves " + string.Join(' ', legal));
		}

		if (parameters.Depth.HasValue) parts.Add($"depth {parameters.Depth.Value}");
		if (parameters.Mate.HasValue) parts.Add($"mate {parameters.Mate.Value}");
		if (parameters.MoveTimeMs.HasValue) parts.Add($"movetime {parameters.MoveTimeMs.Value}");
		if (parameters.Nodes.HasValue) parts.Add($"nodes {parameters.Nodes.Value}");
		if (parameters.WhiteTimeMs.HasValue) parts.Add($"wtime {parameters.WhiteTimeMs.Value}");
		if (parameters.BlackTimeMs.HasValue) parts.Add($"btime {parameters.BlackTimeMs.Value}");
		if (parameters.WhiteIncrementMs.HasValue) parts.Add($"winc {parameters.WhiteIncrementMs.Value}");
		if (parameters.BlackIncrementMs.HasValue) parts.Add($"binc {parameters.BlackIncrementMs.Value}");

		return string.Join(' ', parts);
	}

	// Internals

	private async Task ReadLoopAsync()
	{
		var token = _cts?.Token ?? CancellationToken.None;

		try
		{
			await foreach (string line in _transport.ReadLinesAsync(token).ConfigureAwait(false))
			{
				try
				{
					LineReceived?.Invoke(line);

					if (line.StartsWith("info ", StringComparison.OrdinalIgnoreCase) &&
						PrincipalVariation.TryParse(line, out var pv))
						InfoPvReceived?.Invoke(pv);

					if (line.StartsWith("bestmove ", StringComparison.OrdinalIgnoreCase))
					{
						string[]? tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
						string    best   = tokens.Length > 1 ? tokens[1] : string.Empty;
						string    ponder = tokens.Length > 3 && tokens[2] == "ponder" ? tokens[3] : string.Empty;
						BestMoveReceived?.Invoke(best, ponder);
					}

					foreach (var id in _waiters.Keys)
					{
						if (!_waiters.TryGetValue(id, out var w)) continue;

						bool match;
						try
						{
							match = w.predicate(line);
						}
						catch
						{
							match = false;
						}

						if (match)
						{
							_waiters.TryRemove(id, out _);
							w.tcs.TrySetResult(line);
						}
					}
				}
				catch
				{
					// keep loop alive
				}
			}
		}
		catch
		{
			// gracefully end
		}

		// Cancel any pending waiters
		foreach (var kv in _waiters)
		{
			if (_waiters.TryRemove(kv.Key, out var w))
				w.tcs.TrySetCanceled();
		}
	}

	private async Task<string> WaitForLineAsync(Func<string, bool> predicate, TimeSpan timeout, CancellationToken ct)
	{
		var id  = Guid.NewGuid();
		var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
		_waiters.TryAdd(id, (predicate, tcs));

		using var timeoutCts = timeout == Timeout.InfiniteTimeSpan ? null : new CancellationTokenSource(timeout);
		using var linked = timeoutCts is null
							   ? CancellationTokenSource.CreateLinkedTokenSource(ct)
							   : CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

		using var reg = linked.Token.Register(() => tcs.TrySetCanceled());

		try
		{
			return await tcs.Task.ConfigureAwait(false);
		}
		finally
		{
			_waiters.TryRemove(id, out _);
		}
	}
}
