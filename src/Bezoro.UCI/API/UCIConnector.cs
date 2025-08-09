using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.Core.Common.Extensions;
using Bezoro.UCI.API.Types;
using Bezoro.UCI.Domain.Constants;
using Bezoro.UCI.Domain.Helpers;

namespace Bezoro.UCI.API;

/// <summary>
///     Represents a connection to a UCI-compliant chess engine.
///     This class handles process management, command serialization, and asynchronous communication.
///     It is designed to be thread-safe and robust using the Command pattern.
/// </summary>
public sealed class UCIConnector : IAsyncDisposable
{
	private readonly ConcurrentDictionary<int, GOResult?> _goResultCache = new();
	private readonly Process?                             _engineProcess;
	private readonly string                               _enginePath;
	private readonly UCIEngine                            _engine;

	private bool     _started;
	private FenInfo? _currentFenCache;

	private volatile int                       _isDisposed;
	private volatile int                       _isDisposing;
	private          List<MoveClassification>? _currentPositionMovesCache = new();

	public event Action<(string best, string ponder)>? BestMoveFound;
	public event Action?                               EngineStarted;
	public event Action?                               EngineStopped;
	public event Action?                               PositionSetSuccessfully;

	/// <summary>
	///     Initializes a new instance of the <see cref="UCIConnector" /> class.
	/// </summary>
	/// <param name="enginePath">The file path to the UCI engine executable.</param>
	public UCIConnector(string enginePath)
	{
		if (string.IsNullOrWhiteSpace(enginePath))
			throw new ArgumentException("Engine path must be provided.", nameof(enginePath));

		_enginePath = enginePath;
		_engineProcess = new()
		{
			StartInfo = new()
			{
				FileName               = _enginePath,
				RedirectStandardInput  = true,
				RedirectStandardOutput = true,
				UseShellExecute        = false,
				CreateNoWindow         = true
			},
			EnableRaisingEvents = true
		};

		_engine = new(_engineProcess);
		Logger.LogSuccess($"Engine Process Created", this, LogCategory.UCI);
	}

	public async Task PonderHit(CancellationToken ct = default)
	{
		ThrowIfDisposed();
		await _engine.WriteLineAsync("ponderhit", ct);
	}

	public async Task QuitEngineAsync()
	{
		if (_isDisposed.IsPositive()) return;

		try
		{
			if (!_isDisposing.IsPositive())
				await _engine.WriteLineAsync("quit");
		}
		catch (ObjectDisposedException)
		{
			// Expected during disposal
		}

		_started = false;
		EngineStopped?.Invoke();
		Logger.LogSuccess($"Engine Process Stopped", this, LogCategory.UCI);
	}

	public async Task SendTextCommandAndWaitForTokenAsync(
		string            command,
		string            token,
		CancellationToken ct = default)
	{
		ThrowIfDisposed();
		await _engine.WriteLineAsync(command, ct);
		await _engine.WaitForTokenAsync(token, ct);
	}

	public async Task SendTextCommandAsync(string command, CancellationToken ct = default)
	{
		ThrowIfDisposed();
		await _engine.SendCommandAndReadOutputAsync(command, ct);
	}

	public async Task SetDefaultPositionAsync()
	{
		await SetPositionAsync(UCIConstants.StandardFEN);
	}

	public async Task SetOptionAsync(string name, string value, CancellationToken ct = default)
	{
		ThrowIfDisposed();
		await _engine.WriteLineAsync($"setoption name {name} value {value}", ct);
		Logger.LogSuccess($"Option Set Successfully {name.Bold()} {value.Bold()}", this, LogCategory.UCI);
	}

	public async Task SetPositionAsync(
		string               fen,
		IEnumerable<string>? moves = null,
		CancellationToken    ct    = default)
	{
		ThrowIfDisposed();

		var command                = $"position fen {fen}";
		if (moves != null) command += $" moves {string.Join(" ", moves)}";

		await _engine.WriteLineAsync(command, ct);
		PositionSetSuccessfully?.Invoke();
		ClearPositionCaches();
		Logger.LogSuccess($"Position Set Successfully {command.Bold()}", this, LogCategory.UCI);
	}

	public async Task StartEngineAsync()
	{
		ThrowIfDisposed();

		_engine.Start();

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

		await _engine.WriteLineAsync("uci", cts.Token);
		await _engine.WaitForTokenAsync("uciok", cts.Token);
		await _engine.WriteLineAsync("isready", cts.Token);
		await _engine.WaitForTokenAsync("readyok", cts.Token);
		_started = true;
		EngineStarted?.Invoke();
		Logger.LogSuccess($"Engine Process Started", this, LogCategory.UCI);
	}

	public async Task WriteLineAsync(string command, CancellationToken ct = default)
	{
		ThrowIfDisposed();
		await _engine.WriteLineAsync(command, ct);
	}

	public async Task<FenInfo> GetCurrentFenAsync(CancellationToken ct = default)
	{
		ThrowIfDisposed();

		if (_currentFenCache.HasValue)
		{
			Logger.LogInfo($"Returning cached FEN: {_currentFenCache}", this, LogCategory.UCI);
			;
			return _currentFenCache.Value;
		}

		return await RenewFenCache(ct);
	}

	public async Task<GOResult?> GO(int depth = 5, CancellationToken ct = default)
	{
		if (_goResultCache.TryGetValue(depth, out var cached) && cached.HasValue)
		{
			Logger.LogInfo($"Returning cached GO result: {cached}", this, LogCategory.UCI);
			return cached;
		}

		var lines  = await _engine.SendCommandAndReadOutputAsync("go depth " + depth, ct);
		var result = ParseLinesAndBuildGOResult(lines);
		return _goResultCache[depth] = result;
	}

	public async Task<GOResult?> GoPerftOne(CancellationToken ct = default)
	{
		if (_goResultCache.TryGetValue(1, out var cached) && cached.HasValue)
		{
			Logger.LogInfo($"Returning cached GO result: {cached}", this, LogCategory.UCI);
			return cached;
		}

		var lines  = await _engine.SendCommandAndReadOutputAsync(UCIConstants.GoPerftDepth1Command, ct);
		var result = ParseLinesAndBuildGOResult(lines);
		return _goResultCache[1] = result;
	}

	public async Task<GOResult?> StartSearchForSecondsAsync(int seconds, CancellationToken ct = default)
	{
		int milliSeconds = seconds * 1000;
		var lines        = await _engine.SendCommandAndReadOutputAsync($"go movetime {milliSeconds}", ct);
		return ParseLinesAndBuildGOResult(lines);
	}

	public async Task<ICollection<MoveClassification>> GetLegalMovesForSquareWithDetailsAsync(
		string            square,
		CancellationToken ct = default)
	{
		ThrowIfDisposed();

		if (string.IsNullOrWhiteSpace(square))
			throw new ArgumentException("Square cannot be null or empty", nameof(square));

		// Get all moves for current position (cached if same position)
		var allMoves = await GetCurrentPositionMovesAsync(ct);
		return allMoves.Where(move => move.From.Equals(square, StringComparison.OrdinalIgnoreCase)).ToList();
	}

	/// <summary>
	///     Gets all legal moves from the current position.
	/// </summary>
	/// <param name="ct">A token to cancel the operation.</param>
	public async Task<LegalMovesResult> GetLegalMovesAsync(CancellationToken ct = default)
	{
		var legalMoves = new List<string>();

		var lines =
			await _engine.SendCommandAndReadOutputAsync(UCIConstants.GoPerftDepth1Command, ct);

		foreach (string line in lines)
		{
			var match = UCIConstants.MoveRegex.Match(line);

			if (match.Success) legalMoves.Add(match.Groups[1].Value);
		}

		return new(legalMoves);
	}

	/// <summary>
	///     Gets all legal moves with detailed classification.
	/// </summary>
	/// <param name="ct">A token to cancel the operation.</param>
	public async Task<List<MoveClassification>> GetAllLegalMovesWithDetailsAsync(CancellationToken ct = default)
	{
		Logger.LogInfo("GettingAllLegalMoves...", this, LogCategory.UCI);
		var currentFen       = await GetCurrentFenAsync(ct);
		var legalMovesResult = await GetLegalMovesAsync(ct);
		var boardState       = BoardStateParser.ParseFen(currentFen.Fen);
		var classifiedMoves =
			legalMovesResult.LegalMoves.Select(move => MoveClassifier.ClassifyMove(move, boardState)).ToList();

		Logger.LogInfo($"Legal Moves -> {classifiedMoves}", this, LogCategory.UCI);
		Logger.LogInfo("GettingAllLegalMoves...Done",       this, LogCategory.UCI);

		return classifiedMoves;
	}

	public async ValueTask DisposeAsync()
	{
		// Ensure idempotent disposal
		if (Interlocked.Exchange(ref _isDisposed, 1) == 1) return;

		Interlocked.Exchange(ref _isDisposing, 1);

		try
		{
			// Try to gracefully stop the engine if it was started
			if (_started)
			{
				try
				{
					await QuitEngineAsync();
				}
				catch
				{
					// Swallow exceptions during disposal
				}
			}

			// Ensure the underlying process is not left running
			if (_engineProcess is { HasExited: false })
			{
				try
				{
					_engineProcess.Kill();
				}
				catch
				{
					// Ignore failures while disposing
				}
			}

			// Dispose engine wrapper and process resources
			try
			{
				await _engine.DisposeAsync();
			}
			catch
			{
				// Ignore failures while disposing
			}

			try
			{
				_engineProcess?.Dispose();
			}
			catch
			{
				// Ignore failures while disposing
			}

			// Clear caches and internal state
			_goResultCache.Clear();
			_currentFenCache           = null;
			_currentPositionMovesCache = null;
			_started                   = false;
		}
		finally
		{
			Volatile.Write(ref _isDisposing, 0);
			GC.SuppressFinalize(this);
		}
	}

	public void NewGame()
	{
		_engine.WriteLineAsync("ucinewgame");
		Logger.LogSuccess($"New Game", this, LogCategory.UCI);
	}

	private static (string best, string ponder) ParseBestMove(string line)
	{
		if (!line.Contains("bestmove", StringComparison.OrdinalIgnoreCase)) return (string.Empty, string.Empty);

		string[]? parts = line.Split(' ');
		if (parts.Length < 2) return (string.Empty, string.Empty);

		string bestMove   = parts[1];
		string ponderMove = string.Empty;

		// Check if there's a ponder move
		if (parts.Length >= 4 && parts[2] == "ponder") ponderMove = parts[3];

		if (bestMove == "(none)") bestMove = string.Empty;

		return (bestMove, ponderMove);
	}

	private static GOResult ParseLinesAndBuildGOResult(List<string> lines)
	{
		string? bestMove   = string.Empty;
		string? ponderMove = string.Empty;
		int?    scoreMate  = 0;

		foreach (string line in lines)
		{
			// Parse best move information
			var bestMoveResult = ParseBestMove(line);

			if (!string.IsNullOrEmpty(bestMoveResult.best))
			{
				bestMove   = bestMoveResult.best;
				ponderMove = bestMoveResult.ponder;
			}

			int? currentScoreMate                   = ParseScoreMate(line);
			if (currentScoreMate != null) scoreMate = currentScoreMate;
		}

		var result = new GOResult(bestMove, ponderMove, scoreMate);
		return result;
	}

	private static int? ParseScoreMate(string line)
	{
		if (string.IsNullOrWhiteSpace(line)) return null;

		string[]? tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

		for (var i = 0; i < tokens.Length - 1; i++)
		{
			if (tokens[i] == "score" && i + 2 < tokens.Length && tokens[i + 1] == "mate")
			{
				if (int.TryParse(tokens[i + 2], out int mateValue))
					return mateValue;
			}
		}

		return null;
	}

	private static string ParseCheckersInfo(string line)
	{
		string checkers = string.Empty;

		if (line.Contains("checkers", StringComparison.OrdinalIgnoreCase))
		{
			string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length >= 2)
				checkers = parts[1];
		}

		return checkers;
	}

	private async Task<FenInfo> RenewFenCache(CancellationToken ct = default)
	{
		List<string> lines;

		// First attempt: prefer reading until "checkers" (some engines provide it as the tail of `d` output)
		try
		{
			using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
			using var linked     = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
			lines = await _engine.SendCommandAndReadOutputAsync("d", linked.Token, ["checkers"]);
		}
		catch (OperationCanceledException)
		{
			// Fallback: some engines never print "checkers"; try again, stopping as soon as we see "fen"
			using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
			using var linked     = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
			lines = await _engine.SendCommandAndReadOutputAsync("d", linked.Token, ["fen"]);
		}

		var fen      = "";
		var checkers = "";
		foreach (string line in lines)
		{
			// Capture 'checkers' info when present
			checkers = ParseCheckersInfo(line);

			// locate the word "fen" in any casing
			int idx = line.IndexOf("fen", StringComparison.OrdinalIgnoreCase);
			if (idx < 0) continue;

			// advance past "fen"
			int start = idx + 3;

			// optional colon right after "fen"
			if (start < line.Length && line[start] == ':')
				start++;

			string candidate                               = line[start..].Trim();
			if (!string.IsNullOrWhiteSpace(candidate)) fen = candidate;
		}

		if (fen.IsNullOrEmpty())
			throw new InvalidOperationException("No valid FEN string found in engine output");

		string[]? parts = fen.Split(' ');

		if (parts.Length != 6)
		{
			Logger.LogError($"FEN: {fen}", this, LogCategory.UCI);
			throw new InvalidOperationException(
				$"Invalid FEN format: expected 6 parts, got {parts.Length}. FEN: '{fen}'");
		}

		_currentFenCache = new FenInfo(
			parts[0],            // PiecePlacement
			parts[1][0],         // ActiveColor
			parts[2],            // CastlingRights
			parts[3],            // EnPassantTarget
			int.Parse(parts[4]), // HalfmoveClock
			int.Parse(parts[5]), // FullmoveNumber
			fen,                 // Full FEN string
			parts,               // FEN parts array
			checkers
		);

		return _currentFenCache.Value;
	}

	private async Task<List<MoveClassification>> GetCurrentPositionMovesAsync(CancellationToken ct = default)
	{
		// If we have moves cached for this exact position, return them
		if (!_currentPositionMovesCache.IsNullOrEmpty())
		{
			Logger.LogInfo(
				$"Returning cached moves for position: {_currentPositionMovesCache.PrettyJoin()}",
				this,
				LogCategory.UCI);

			return _currentPositionMovesCache;
		}

		// Calculate moves for new position
		Logger.LogInfo("Calculating legal moves for new position...", this, LogCategory.UCI);

		var currentFen       = await GetCurrentFenAsync(ct);
		var legalMovesResult = await GetLegalMovesAsync(ct);
		var boardState       = BoardStateParser.ParseFen(currentFen.Fen);
		var classifiedMoves = legalMovesResult.LegalMoves
											  .Select(move => MoveClassifier.ClassifyMove(
														  move,
														  boardState)).ToList();

		_currentPositionMovesCache = classifiedMoves;

		Logger.LogInfo($"Cached {classifiedMoves.Count} legal moves for position", this, LogCategory.UCI);

		return classifiedMoves;
	}

	private void ClearPositionCaches()
	{
		_currentPositionMovesCache?.Clear();
		_currentFenCache = null;
		_goResultCache.Clear();
	}

	private void ThrowIfDisposed()
	{
		if (_isDisposed.IsPositive() || _isDisposing.IsPositive())
		{
			throw new ObjectDisposedException(
				nameof(UCIConnector),
				"Cannot use a disposed UCIConnector. Make sure you haven't called DisposeAsync()");
		}
	}
}

public readonly record struct FenInfo(
	string   PiecePlacement,
	char     ActiveColor,
	string   CastlingRights,
	string   EnPassantTarget,
	int      HalfmoveClock,
	int      FullmoveNumber,
	string   Fen,
	string[] FenParts,
	string   Checkers);

public readonly record struct GOResult(string BestMove, string PonderMove, int? ScoreMate);

public readonly record struct LegalMovesResult(IEnumerable<string> LegalMoves);
