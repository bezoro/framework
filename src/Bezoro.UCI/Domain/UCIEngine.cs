using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Bezoro.Core.Common.Extensions;
using Bezoro.UCI.API.Types;
using Bezoro.UCI.Domain.Commands;
using Bezoro.UCI.Domain.Common.Constants;

namespace Bezoro.UCI.Domain;

/// <summary>
///     High-performance, thread–safe wrapper around a UCI engine process.
/// </summary>
internal sealed class UciEngine(Process process) : IAsyncDisposable
{
	private const int HISTORY_CAPACITY = 8192;
	private static readonly string[] DefaultTerminators =
	[
		"bestmove", "uciok", "readyok", "nodes searched", "checkers"
	];

	private readonly ConcurrentDictionary<PendingRequest, byte> _pending         = new();
	private readonly ConcurrentDictionary<uint, SearchResult>   _searchInfoCache = new();
	private readonly ConcurrentQueue<string>                    _outputHistory   = new();

	private readonly List<Move> _currentLegalMovesCache = [];

	private readonly Process _proc = process ?? throw new ArgumentNullException(nameof(process));

	private readonly SemaphoreSlim _commandLock    = new(1, 1);
	private readonly SemaphoreSlim _stdinWriteLock = new(1, 1);

	private Fen? _currentFenCache;

	private volatile int        _isDisposed;
	private          int        _outputHistorySize;
	private          List<Move> _currentSquareMovesCache = [];

	private StreamWriter? _stdin;

	private uint _currentMultiPv;

	public bool IsStarted { get; private set; }

	public async IAsyncEnumerable<string> SendCommandAndReadStreamAsync(
		string                                     command,
		[EnumeratorCancellation] CancellationToken ct    = default,
		string[]?                                  until = null)
	{
		var resp = await SendCommandAsync(command, ct, until).ConfigureAwait(false);

		await foreach (string? line in resp.Lines.WithCancellation(ct).ConfigureAwait(false)) yield return line;
	}

	public MoveScore? TryGetMoveScoreFromHistory(string notation)
	{
		ThrowIfDisposed();
		notation.ThrowIfNull().ThrowIfEmpty().Length.ThrowIfLessThan(4);

		var    parsed = ParsedMove.FromNotation(notation);
		string target = parsed.Notation;

		string[] lines = _outputHistory.ToArray();

		for (int i = lines.Length - 1; i >= 0; i--)
		{
			string line = lines[i];

			PrincipalVariation.TryParse(line, out var variation);
			if (variation.RawPv.IsNullOrEmpty()) continue;

			if (!(variation.RawPv.StartsWith(target, StringComparison.OrdinalIgnoreCase) ||
				  variation.RawPv.Contains($" {target}", StringComparison.OrdinalIgnoreCase)))
				continue;

			if (MoveScore.TryParse(line, out var moveScore) && moveScore.HasValue)
				return moveScore.Value;
		}

		return null;
	}

	/// <summary>
	///     Signals a brand new game to the engine and clears local history so stale tokens won't match.
	///     Always call this when starting a fresh game if the same engine process is reused.
	/// </summary>
	public async Task NewGameAsync(CancellationToken ct = default)
	{
		ThrowIfDisposed();

		await WriteLineSafeAsync(UciConstants.UCI_NEW_GAME_COMMAND, ct).ConfigureAwait(false);
		await WaitReadyAsync(ct).ConfigureAwait(false);

		// Clear our stored caches and history
		_outputHistory.Clear();
		_outputHistorySize = 0;
		_currentFenCache   = null;
		_currentLegalMovesCache.Clear();
		_currentSquareMovesCache.Clear();
		_searchInfoCache.Clear();
		Logger.LogSuccess($"New Game", this, LogCategory.UCI);
	}

	public async Task PonderhitAsync(CancellationToken ct = default)
	{
		ThrowIfDisposed();
		await WriteLineSafeAsync("ponderhit", ct).ConfigureAwait(false);
		Logger.LogInfo($"Ponder Hit", this, LogCategory.UCI);
	}

	public async Task QuitEngineAsync(CancellationToken ct)
	{
		if (_isDisposed.IsPositive()) return;

		try
		{
			await WriteLineSafeAsync(UciConstants.STOP_COMMAND, ct).ConfigureAwait(false);
		}
		catch (ObjectDisposedException)
		{
			// Expected during disposal
		}

		IsStarted = false;
		Logger.LogSuccess($"Engine Marked As Stopped (process kept alive)", this, LogCategory.UCI);
	}

	public async Task SetOptionAsync(string name, int value, CancellationToken ct = default)
	{
		ThrowIfDisposed();
		await WriteLineSafeAsync($"{UciConstants.SET_OPTION_COMMAND} {name} value {value}", ct).ConfigureAwait(false);
		if (name == "MultiPV") _currentMultiPv = (uint)value;
		Logger.LogSuccess($"Option Set Successfully {name.Bold()} {value.ToString().Bold()}", this, LogCategory.UCI);
	}

	public async Task SetPositionAsync(PositionCommand command, CancellationToken ct)
	{
		ThrowIfDisposed();

		await WriteLineSafeAsync(command, ct).ConfigureAwait(false);

		ClearPositionCaches();
		Logger.LogSuccess($"Position Set Successfully {command.ToString().Bold()}", this, LogCategory.UCI);
		return;


		void ClearPositionCaches()
		{
			_currentSquareMovesCache.Clear();
			_currentLegalMovesCache.Clear();
			_searchInfoCache.Clear();
			_currentFenCache = null;
		}
	}

	public async Task StartEngineAsync()
	{
		ThrowIfDisposed();
		Start();
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

		await WriteCommandToStreamAsyncInternal(UciConstants.UCI_COMMAND, cts.Token).ConfigureAwait(false);
		await WaitForToken(UciConstants.UCI_OK_RESPONSE, cts.Token).ConfigureAwait(false);
		await WriteCommandToStreamAsyncInternal(UciConstants.IS_READY_COMMAND, cts.Token).ConfigureAwait(false);
		await WaitForToken(UciConstants.READY_OK_RESPONSE, cts.Token).ConfigureAwait(false);

		IsStarted = true;
		Logger.LogSuccess($"Engine Process Started", this, LogCategory.UCI);
	}


	/// <summary>
	///     Waits until a line received from the engine contains the specified token
	///     (case-insensitive).  No command is sent – the caller is expected to have
	///     already issued the request that will eventually produce the token
	///     (“uciok”, “readyok”, custom search id, …).
	/// </summary>
	/// <param name="token">Substring that must appear in a single output line.</param>
	/// <param name="ct">Cancellation token to abort the wait.</param>
	public Task WaitForToken(string token, CancellationToken ct = default)
		=> WaitForTokens([token], ct);

	/// <summary>
	///     Same as <see cref="WaitForToken" />
	///     but waits for any of the supplied tokens.
	/// </summary>
	public Task WaitForTokens(string[] tokens, CancellationToken ct = default)
	{
		ThrowIfDisposed();

		var req = CreatePending(tokens, ct);

		foreach (string line in _outputHistory)
		{
			if (req.TryAccept(line))
				return req.Task;
		}

		Logger.LogInfo($"Waiting for tokens: {string.Join(", ", tokens)}", this, LogCategory.UCI);

		return req.Task;
	}

	public async Task WaitReadyAsync(CancellationToken ct = default)
	{
		ThrowIfDisposed();
		await WriteLineSafeAsync(UciConstants.IS_READY_COMMAND, ct).ConfigureAwait(false);
		await WaitForToken(UciConstants.READY_OK_RESPONSE, ct).ConfigureAwait(false);
		Logger.LogSuccess($"Engine Ready", this, LogCategory.UCI);
	}

	/// <summary>
	///     Checks whether applying the given move to the current engine position results in stalemate.
	///     Returns true if the position after the move has no legal replies and is not check.
	/// </summary>
	public async Task<bool> WouldMoveLeadToStalemateAsync(string notation, CancellationToken ct = default)
	{
		ThrowIfDisposed();
		notation.ThrowIfNull().ThrowIfEmpty();

		var parsedMove = ParsedMove.FromNotation(notation);

		var originalFen = _currentFenCache ?? await GetCurrentFenAsync(ct).ConfigureAwait(false);

		await WriteLineSafeAsync($"{UciConstants.POSITION_COMMAND} fen {originalFen} moves {parsedMove.Notation}", ct)
			.ConfigureAwait(false);

		try
		{
			var perftLines = await SendCommandAndReadAsync(UciConstants.GO_PERFT_DEPTH1_COMMAND, ct)
								 .ConfigureAwait(false);

			var replyCount = 0;
			foreach (string? line in perftLines)
			{
				var match = UciConstants.MoveRegex.Match(line);
				if (match.Success) replyCount++;
			}

			if (replyCount > 0) return false;

			var diagLines = await SendCommandAndReadAsync(UciConstants.DISPLAY_BOARD_COMMAND, ct).ConfigureAwait(false);

			var inCheck = false;
			foreach (string? line in diagLines)
			{
				if (!line.StartsWith(UciConstants.CHECKERS_RESPONSE, StringComparison.OrdinalIgnoreCase)) continue;

				int colonIdx = line.IndexOf(':');
				string tail = colonIdx >= 0
								  ? line[(colonIdx + 1)..].Trim()
								  : line[UciConstants.CHECKERS_RESPONSE.Length..].Trim();

				if (tail.Length > 0                                          &&
					!tail.Equals("-",    StringComparison.OrdinalIgnoreCase) &&
					!tail.Equals("none", StringComparison.OrdinalIgnoreCase))
					inCheck = true;

				break; // Found the "Checkers" line
			}

			// Stalemate iff: no legal replies AND not in check
			return !inCheck;
		}
		finally
		{
			// Restore original position to avoid side effects
			await WriteLineSafeAsync($"{UciConstants.POSITION_COMMAND} fen {originalFen}", ct).ConfigureAwait(false);
		}
	}

	public async Task<Fen> GetCurrentFenAsync(CancellationToken ct)
	{
		ThrowIfDisposed();

		if (!_currentFenCache.HasValue)
			return await RenewFenCache(ct).ConfigureAwait(false);

		Logger.LogInfo($"Returning cached FEN: {_currentFenCache}", this, LogCategory.UCI);
		return _currentFenCache.Value;
	}

	/// <summary>
	///     Gets all legal moves from the current engine state.
	/// </summary>
	/// <param name="ct">A token to cancel the operation.</param>
	public async Task<IReadOnlyCollection<Move>> GetLegalMovesAsync(CancellationToken ct = default)
	{
		ThrowIfDisposed();

		if (_currentLegalMovesCache.Count > 0)
		{
			Logger.LogInfo($"Returning cached legal moves: {_currentLegalMovesCache.Count}", this, LogCategory.UCI);
			return _currentLegalMovesCache;
		}

		var fen   = await GetCurrentFenAsync(ct).ConfigureAwait(false);
		var board = BoardState.FromFen(fen);
		board.ThrowIfNull();

		var lines = await SendCommandAndReadAsync(UciConstants.GO_PERFT_DEPTH1_COMMAND, ct).ConfigureAwait(false);

		var moves = new List<Move>();
		var seen  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (string? line in lines)
		{
			var match = UciConstants.MoveRegex.Match(line);
			if (!match.Success) continue;

			string moveUci = match.Groups[1].Value.ToLowerInvariant();
			if (!seen.Add(moveUci)) continue;

			char   pieceChar    = MoveToPieceMap.Map(fen, moveUci).Piece;
			string enrichedMove = pieceChar != '\0' ? pieceChar + moveUci : moveUci;
			var    analysis     = await MoveAnalysis.AnalyzeAsync(enrichedMove, board.Value, this);
			var    move         = new Move(enrichedMove, analysis);
			moves.Add(move);
		}

		_currentLegalMovesCache.Clear();
		_currentLegalMovesCache.AddRange(moves);
		Logger.LogSuccess($"Collected legal moves: {_currentLegalMovesCache.Count}", this, LogCategory.UCI);
		return _currentLegalMovesCache;
	}

	public async Task<IReadOnlyCollection<Move>> GetLegalMovesForSquareAsync(
		string            square,
		CancellationToken ct = default)
	{
		square.ThrowIfNull();
		ThrowIfDisposed();

		var allLegalMoves = await GetLegalMovesAsync(ct).ConfigureAwait(false);
		var legalMoves = allLegalMoves.Where(move => move.From.Equals(square, StringComparison.OrdinalIgnoreCase))
									  .ToList();

		_currentSquareMovesCache = legalMoves;
		return legalMoves;
	}

	public async Task<IReadOnlyCollection<string>> SendCommandAndReadAsync(
		string            command,
		CancellationToken ct    = default,
		string[]?         until = null)
	{
		var resp = await SendCommandAsync(command, ct, until).ConfigureAwait(false);
		return await resp.Completed.ConfigureAwait(false);
	}

	public async Task<IReadOnlyCollection<string>> StopSearchAsync(CancellationToken ct = default)
	{
		ThrowIfDisposed();
		var output = await SendCommandAndReadAsync(
						 UciConstants.STOP_COMMAND,
						 ct,
						 [UciConstants.BEST_MOVE_RESPONSE_PREFIX]).ConfigureAwait(false);

		Logger.LogSuccess($"Search Stopped", this, LogCategory.UCI);
		return output;
	}

	public async Task<MoveScore> CalculateScoreForMoveAsync(
		string            notation,
		CancellationToken ct,
		uint              multiPv        = 8,
		uint              msBudget       = 220,
		PositionCommand?  customPosition = null)
	{
		ThrowIfDisposed();

		var parsedMove = ParsedMove.FromNotation(notation);

		if (customPosition is not null)
			await WriteLineSafeAsync(customPosition, ct).ConfigureAwait(false);

		await WriteLineSafeAsync($"{UciConstants.SET_OPTION_COMMAND} MultiPV value {(int)multiPv}", ct)
			.ConfigureAwait(false);

		var response = await SendCommandAsync(
							   $"{UciConstants.GO_COMMAND} "                     +
							   $"{UciConstants.MOVE_TIME_PARAMETER} {msBudget} " +
							   $"{UciConstants.SEARCH_MOVES_PARAMETER} {parsedMove.Notation}",
							   ct)
						   .ConfigureAwait(false);

		MoveScore score = default;
		await foreach (string? line in response.Lines.WithCancellation(ct).ConfigureAwait(false))
		{
			if (string.IsNullOrEmpty(line)) continue;

			if (!MoveScore.TryParse(line, out var moveScore) || !moveScore.HasValue) continue;

			if (moveScore.Value.ScoreMate.HasValue)
			{
				score = moveScore.Value;
				break;
			}

			if (moveScore.Value.ScoreCp.HasValue &&
				(!score.ScoreCp.HasValue || moveScore.Value.ScoreCp > score.ScoreCp))
				score = moveScore.Value;
		}

		await SetOptionAsync("MultiPV", (int)_currentMultiPv, ct).ConfigureAwait(false);

		return score;
	}


	public async Task<SearchResult> GO(uint depth = 5, CancellationToken ct = default)
	{
		if (_searchInfoCache.TryGetValue(depth, out var cached))
		{
			Logger.LogInfo($"Returning cached GO result: {cached}", this, LogCategory.UCI);
			return cached;
		}

		var result = await ExecuteSearch($"{UciConstants.GO_COMMAND} {UciConstants.DEPTH_PARAMETER} " + depth, ct)
						 .ConfigureAwait(false);

		return _searchInfoCache[depth] = result;
	}

	public async Task<SearchResult> GoPerftOne(CancellationToken ct = default)
	{
		if (_searchInfoCache.TryGetValue(1, out var cached))
		{
			Logger.LogInfo($"Returning cached GoPerftOne result: {cached}", this, LogCategory.UCI);
			return cached;
		}

		var result = await ExecuteSearch(UciConstants.GO_PERFT_DEPTH1_COMMAND, ct).ConfigureAwait(false);

		return _searchInfoCache[1] = result;
	}

	public async Task<SearchResult> StartSearchForSecondsAsync(
		uint              seconds,
		CancellationToken ct = default)
	{
		uint milliSeconds = seconds * 1000;

		var result = await ExecuteSearch(
						 $"{UciConstants.GO_COMMAND} {UciConstants.MOVE_TIME_PARAMETER} {milliSeconds}",
						 ct).ConfigureAwait(false);

		return result;
	}

	/// <summary>
	///     Sends a command and returns a response that can be consumed both as a live line stream
	///     and as a bulk collection when the terminator condition is met.
	/// </summary>
	public async Task<UciCommandResponse> SendCommandAsync(
		string            command,
		CancellationToken ct    = default,
		string[]?         until = null)
	{
		ThrowIfDisposed();

		await _commandLock.WaitAsync(ct).ConfigureAwait(false);

		try
		{
			until ??= DefaultTerminators;
			var req = CreatePending(until, ct);

			await WriteLineSafeAsync(command, ct).ConfigureAwait(false);

			Logger.LogInfo(
				$"Sending command: {command} (waiting for: {string.Join(", ", until)})",
				this,
				LogCategory.UCI);

			_ = req.Task.ContinueWith(
				static (_, state) => ((SemaphoreSlim)state!).Release(),
				_commandLock,
				CancellationToken.None,
				TaskContinuationOptions.ExecuteSynchronously,
				TaskScheduler.Default);

			var completed = req.Task.ContinueWith<IReadOnlyCollection<string>>(
				static t => t.Result,
				TaskScheduler.Default);

			return new(req.Stream(ct), completed);
		}
		catch
		{
			_commandLock.Release();
			throw;
		}
	}

	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _isDisposed, 1) == 1) return;

		Exception? firstError        = null;
		var        disposedException = new ObjectDisposedException(nameof(UciEngine));

		// Unsubscribe process events
		try
		{
			_proc.Exited              -= OnProcessExited;
			_proc.EnableRaisingEvents =  false;
		}
		catch (Exception ex)
		{
			firstError ??= ex;
		}

		// Fail any pending requests
		try
		{
			foreach (var request in _pending.Keys)
			{
				if (!_pending.TryRemove(request, out _)) continue;

				request.Fail(disposedException);
			}
		}
		catch (Exception ex)
		{
			firstError ??= ex;
		}

		// Flush and dispose stdin safely
		var stdin = Interlocked.Exchange(ref _stdin, null);
		if (stdin is not null)
		{
			try
			{
				await _stdinWriteLock.WaitAsync().ConfigureAwait(false);
				try
				{
					await stdin.FlushAsync().ConfigureAwait(false);
				}
				finally
				{
					try
					{
						_stdinWriteLock.Release();
					}
					catch
					{
						// ignore
					}
				}

				await stdin.DisposeAsync().ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				firstError ??= ex;
			}
		}

		// Clear buffers and reset sizes
		try
		{
			_outputHistory.Clear();
			_outputHistorySize = 0;
		}
		catch (Exception ex)
		{
			firstError ??= ex;
		}

		// Dispose synchronization primitives
		try
		{
			_commandLock.Dispose();
		}
		catch (Exception ex)
		{
			firstError ??= ex;
		}

		try
		{
			_stdinWriteLock.Dispose();
		}
		catch (Exception ex)
		{
			firstError ??= ex;
		}

		// Log any first error captured during disposal
		if (firstError is not null)
			Logger.LogError(firstError, this, LogCategory.UCI);

		Logger.LogInfo("Disposed", this, LogCategory.UCI);
	}

	/// <summary>
	///     Writes a line directly to the engine process's standard input stream.
	/// </summary>
	/// <param name="command">The UCI command to send to the engine.</param>
	/// <param name="ct">A cancellation token to cancel the write operation.</param>
	/// <exception cref="ObjectDisposedException">Thrown if the engine has been disposed.</exception>
	public async ValueTask WriteLineSafeAsync(string command, CancellationToken ct)
	{
		EnsureStarted();
		ThrowIfDisposed();
		await WriteCommandToStreamAsyncInternal(command, ct);
	}

	private static bool MatchesUciTerminator(string line, string[] tokens)
	{
		string bestMovePrefix   = UciConstants.BEST_MOVE_RESPONSE_PREFIX;
		string uciOkResponse    = UciConstants.UCI_OK_RESPONSE;
		string readyOkResponse  = UciConstants.READY_OK_RESPONSE;
		string checkersResponse = UciConstants.CHECKERS_RESPONSE;

		foreach (string t in tokens)
		{
			if (t == bestMovePrefix || t.Equals(bestMovePrefix, StringComparison.OrdinalIgnoreCase))
			{
				if (line.StartsWith(bestMovePrefix, StringComparison.OrdinalIgnoreCase))
				{
					Logger.LogInfo(
						$"UCI Terminator Match: {line} (matched bestmove)",
						typeof(UciEngine),
						LogCategory.UCI);

					return true;
				}

				continue;
			}

			if (t == uciOkResponse                                            ||
				t == readyOkResponse                                          ||
				t.Equals(uciOkResponse,   StringComparison.OrdinalIgnoreCase) ||
				t.Equals(readyOkResponse, StringComparison.OrdinalIgnoreCase))
			{
				if (line.Equals(t, StringComparison.OrdinalIgnoreCase))
				{
					Logger.LogInfo($"UCI Terminator Match: {line} (matched {t})", typeof(UciEngine), LogCategory.UCI);
					return true;
				}

				continue;
			}

			if (t == checkersResponse || t.Equals(checkersResponse, StringComparison.OrdinalIgnoreCase))
			{
				if (line.StartsWith(checkersResponse, StringComparison.OrdinalIgnoreCase))
				{
					Logger.LogInfo(
						$"UCI Terminator Match: {line} (matched checkers)",
						typeof(UciEngine),
						LogCategory.UCI);

					return true;
				}

				continue;
			}

			if (line.IndexOf(t, StringComparison.OrdinalIgnoreCase) < 0) continue;

			Logger.LogInfo($"UCI Terminator Match: {line} (matched {t})", typeof(UciEngine), LogCategory.UCI);
			return true;
		}

		return false;
	}

	private PendingRequest CreatePending(string[] tokens, CancellationToken ct)
	{
		var normalized = new string[tokens.Length];
		for (var i = 0; i < tokens.Length; i++)
			normalized[i] = tokens[i].ToLowerInvariant();

		var req = new PendingRequest(MatchesUciTerminator, normalized, ct);
		RegisterPending(req);
		return req;
	}

	private async Task ReadErrorLoop()
	{
		try
		{
			using var stderr = _proc.StandardError;

			while (!_isDisposed.IsPositive() && await stderr.ReadLineAsync().ConfigureAwait(false) is { } line)
				Logger.LogError($"[STDERR] {line}", this, LogCategory.UCI);
		}
		catch
		{
			// Ignore errors while draining stderr
		}
	}

	private async Task ReadLoop()
	{
		using var stdout = _proc.StandardOutput;

		while (!_isDisposed.IsPositive() && await stdout.ReadLineAsync().ConfigureAwait(false) is { } line)
		{
			Logger.LogInfo($"[OUTPUT] {line.Bold()}", this, LogCategory.UCI);

			AppendHistory(line);

			foreach (var kvp in _pending)
			{
				var req = kvp.Key;
				if (req.TryAccept(line))
					_pending.TryRemove(req, out _);
			}
		}

		if (!_isDisposed.IsPositive())
		{
			var ex = new EndOfStreamException("Engine process closed its stdout.");
			CompleteOutputAndFailPending(ex);
		}
	}

	private async Task<Fen> RenewFenCache(CancellationToken ct = default)
	{
		ThrowIfDisposed();

		var lines = await SendCommandAndReadAsync("d", ct).ConfigureAwait(false);
		lines.ThrowIfNull();

		Fen? fen = null;
		foreach (string? line in lines)
		{
			if (!Fen.TryParseUciOutputLine(line, out var parsed)) continue;

			fen = parsed;
		}

		fen.ThrowIfNull();

		_currentFenCache = fen;
		return fen.Value;
	}

	private async Task<SearchResult> ExecuteSearch(string goCommand, CancellationToken ct)
	{
		ThrowIfDisposed();
		goCommand.ThrowIfNull();


		var lines = await SendCommandAndReadAsync(goCommand, ct).ConfigureAwait(false);
		lines.ThrowIfNull();

		SearchResult.TryParse(lines, out var result);
		Logger.LogInfo(result, this, LogCategory.UCI);
		return result;
	}

	private async ValueTask WriteCommandToStreamAsyncInternal(string command, CancellationToken ct)
	{
		_stdin.ThrowIfNull();

		await _stdinWriteLock.WaitAsync(ct).ConfigureAwait(false);
		try
		{
			var sw = Volatile.Read(ref _stdin).ThrowIfNull();
			await sw.WriteLineAsync(command).ConfigureAwait(false);
			Logger.LogInfo($"[INPUT] {command.Bold()}", this, LogCategory.UCI);
		}
		finally
		{
			_stdinWriteLock.Release();
		}
	}

	private void AppendHistory(string line)
	{
		_outputHistory.Enqueue(line);
		if (_outputHistorySize < HISTORY_CAPACITY)
			_outputHistorySize++;
		else
			_outputHistory.TryDequeue(out _);
	}

	private void CompleteOutputAndFailPending(Exception ex)
	{
		Logger.LogError($"Engine output closed with error: {ex.Message}", this, LogCategory.UCI);

		foreach (var req in _pending.Keys)
		{
			if (!_pending.TryRemove(req, out _)) continue;

			try
			{
				req.Fail(ex);
				Logger.LogError($"Failing pending request with error: {ex.Message}", this, LogCategory.UCI);
			}
			catch
			{
				// ignore
			}
		}
	}

	private void EnsureStarted()
	{
		if (!IsStarted)
		{
			throw new InvalidOperationException(
				"The UCI engine has not been started. Call StartEngineAsync() before invoking this operation.");
		}
	}

	private void OnProcessExited(object? sender, EventArgs e)
	{
		try
		{
			IsStarted = false;
			var ex = new InvalidOperationException("UCI engine process exited.");
			CompleteOutputAndFailPending(ex);
			Logger.LogError("Engine process exited", this, LogCategory.UCI);
		}
		catch
		{
			// ignore any exceptions thrown during exit handling
		}
	}

	private void RegisterPending(PendingRequest req)
	{
		_pending.TryAdd(req, 0);
		_ = req.Task.ContinueWith(
			_ => _pending.TryRemove(req, out byte _),
			CancellationToken.None,
			TaskContinuationOptions.ExecuteSynchronously,
			TaskScheduler.Default);

		Logger.LogInfo($"Registered pending request: {req}", this, LogCategory.UCI);
	}

	private void Start()
	{
		ThrowIfDisposed();

		_proc.EnableRaisingEvents =  true;
		_proc.Exited              += OnProcessExited;

		_proc.Start();
		_stdin           = _proc.StandardInput;
		_stdin.AutoFlush = true;

		_ = Task.Run(ReadLoop);

		if (_proc.StartInfo.RedirectStandardError) _ = Task.Run(ReadErrorLoop);
		Logger.LogInfo($"Engine Process Starting...", this, LogCategory.UCI);
	}

	private void ThrowIfDisposed()
	{
		if (_isDisposed.IsPositive()) throw new ObjectDisposedException(nameof(UciEngine));
	}

	/// <summary>
	///     Represents the result of a command that can be consumed as a live stream of lines
	///     or awaited for the complete output.
	/// </summary>
	public readonly record struct UciCommandResponse
	{
		internal UciCommandResponse(
			IAsyncEnumerable<string>          lines,
			Task<IReadOnlyCollection<string>> completed)
		{
			Lines     = lines;
			Completed = completed;
		}

		/// <summary>Async stream of lines as they arrive.</summary>
		public IAsyncEnumerable<string> Lines { get; }

		/// <summary>Completes with the full output when the terminator condition is met.</summary>
		public Task<IReadOnlyCollection<string>> Completed { get; }
	}

	private sealed class PendingRequest
	{
		private readonly CancellationTokenRegistration _ctr;
		private readonly Channel<string> _stream = Channel.CreateUnbounded<string>(
			new()
			{
				SingleWriter                  = true,
				SingleReader                  = false,
				AllowSynchronousContinuations = false
			});
		private readonly Func<string, string[], bool> _stop;
		private readonly List<string>                 _lines = [];
		private readonly string[]                     _tokens;
		private readonly TaskCompletionSource<List<string>> _tcs =
			new(TaskCreationOptions.RunContinuationsAsynchronously);

		public PendingRequest(
			Func<string, string[], bool> stop,
			string[]                     tokens,
			CancellationToken            ct)
		{
			_stop   = stop;
			_tokens = tokens;

			_ctr = ct.Register(() =>
			{
				_tcs.TrySetCanceled(ct);

				try
				{
					_stream.Writer.TryComplete(new OperationCanceledException(ct));
				}
				catch
				{
					// ignore
				}
			});

			_ = _tcs.Task.ContinueWith(
				static (_, state) => ((CancellationTokenRegistration)state!).Dispose(),
				_ctr,
				TaskScheduler.Default);
		}

		public Task<List<string>> Task => _tcs.Task;

		public bool TryAccept(string line)
		{
			_lines.Add(line);
			_stream.Writer.TryWrite(line);

			if (!_stop(line, _tokens)) return false;

			_ctr.Dispose();
			_tcs.TrySetResult(_lines);
			_stream.Writer.TryComplete();
			return true;
		}

		public IAsyncEnumerable<string> Stream(CancellationToken ct = default)
			=> _stream.Reader.ReadAllAsync(ct);

		public void Fail(Exception ex)
		{
			_ctr.Dispose();
			_tcs.TrySetException(ex);

			try
			{
				_stream.Writer.TryComplete(ex);
			}
			catch
			{
				// ignore
			}
		}
	}
}
