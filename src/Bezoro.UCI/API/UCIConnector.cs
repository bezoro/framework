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
	private readonly Process   _engineProcess;
	private readonly string    _enginePath;
	private readonly UCIEngine _engine;

	private volatile bool _isDisposed;
	private volatile bool _isDisposing;

	private bool                      _started;
	private FenInfo?                  _currentFenCache;
	private GOResult?                 _goResultCache;
	private List<MoveClassification>? _currentPositionMovesCache;

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
		{
			throw new ArgumentException("Engine path must be provided.", nameof(enginePath));
		}

		_enginePath = enginePath;
		_engineProcess = new Process
		{
			StartInfo = new ProcessStartInfo
			{
				FileName               = _enginePath,
				RedirectStandardInput  = true,
				RedirectStandardOutput = true,
				UseShellExecute        = false,
				CreateNoWindow         = true
			},
			EnableRaisingEvents = true
		};

		_engine = new UCIEngine(_engineProcess);
		Logger.LogSuccess($"Engine Process Created", this, LogCategory.UCI);
	}

	public async Task QuitEngineAsync()
	{
		if (_isDisposed)
		{
			return;
		}

		try
		{
			if (!_isDisposing)
			{
				await _engine.WriteLineAsync("quit");
			}
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
		string command, string token, CancellationToken ct = default)
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
		string fen, IEnumerable<string>? moves = null, CancellationToken ct = default)
	{
		ThrowIfDisposed();

		var command = $"position fen {fen}";
		if (moves != null)
		{
			command += $" moves {string.Join(" ", moves)}";
		}

		await _engine.WriteLineAsync(command, ct);
		PositionSetSuccessfully?.Invoke();
		ClearPositionCaches();
		Logger.LogSuccess($"Position Set Successfully {command.Bold()}", this, LogCategory.UCI);
	}

	public async Task StartEngineAsync()
	{
		ThrowIfDisposed();

		_engine.Start();
		await _engine.WriteLineAsync("uci");
		await _engine.WaitForTokenAsync("uciok");
		await _engine.WriteLineAsync("isready");
		await _engine.WaitForTokenAsync("readyok");
		_started = true;
		EngineStarted?.Invoke();
		Logger.LogSuccess($"Engine Process Started", this, LogCategory.UCI);
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
		if (_goResultCache.HasValue)
		{
			Logger.LogInfo($"Returning cached GO result: {_goResultCache}", this, LogCategory.UCI);
			;
			return _goResultCache.Value;
		}

		List<string> lines = await _engine.SendCommandAndReadOutputAsync("go depth " + depth, ct);

		var  bestMove   = string.Empty;
		var  ponderMove = string.Empty;
		int? scoreMate  = 0;

		foreach (string line in lines)
		{
			// Parse best move information
			var bestMoveResult = ParseBestMove(line);
			if (!string.IsNullOrEmpty(bestMoveResult.best))
			{
				bestMove   = bestMoveResult.best;
				ponderMove = bestMoveResult.ponder;
			}

			int? currentScoreMate = ParseScoreMate(line);
			if (currentScoreMate != null)
			{
				scoreMate = currentScoreMate;
			}
		}

		return _goResultCache = new GOResult(bestMove, ponderMove, scoreMate);
	}

	public async Task<IEnumerable<MoveClassification>> GetLegalMovesForSquareWithDetailsAsync(
		string square, CancellationToken ct = default)
	{
		ThrowIfDisposed();

		if (string.IsNullOrWhiteSpace(square))
		{
			throw new ArgumentException("Square cannot be null or empty", nameof(square));
		}

		// Get all moves for current position (cached if same position)
		List<MoveClassification> allMoves = await GetCurrentPositionMovesAsync(ct);

		return allMoves.Where(move => move.From.Equals(square, StringComparison.OrdinalIgnoreCase));
	}

	/// <summary>
	///     Gets all legal moves from the current position.
	/// </summary>
	/// <param name="ct">A token to cancel the operation.</param>
	public async Task<LegalMovesResult> GetLegalMovesAsync(CancellationToken ct = default)
	{
		var legalMoves = new List<string>();

		List<string> lines =
			await _engine.SendCommandAndReadOutputAsync(UCIConstants.GoPerftDepth1Command, ct);

		foreach (string line in lines)
		{
			var match = UCIConstants.MoveRegex.Match(line);

			if (match.Success)
			{
				legalMoves.Add(match.Groups[1].Value);
			}
		}

		return new LegalMovesResult(legalMoves);
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
		List<MoveClassification> classifiedMoves =
			legalMovesResult.LegalMoves.Select(move => MoveClassifier.ClassifyMove(move, boardState)).ToList();

		Logger.LogInfo($"Legal Moves -> {classifiedMoves}", this, LogCategory.UCI);
		Logger.LogInfo("GettingAllLegalMoves...Done",       this, LogCategory.UCI);

		return classifiedMoves;
	}

	/// <summary>
	///     Disposes the connector and stops the engine.
	/// </summary>
	public async ValueTask DisposeAsync()
	{
		if (_isDisposed) return;

		_isDisposing = true;

		try
		{
			if (_started)
				await QuitEngineAsync();

			await _engine.DisposeAsync();
		}
		finally
		{
			_isDisposed = true;
			Logger.LogSuccess($"Engine Process Disposed", this, LogCategory.UCI);
		}
	}

	public void NewGame()
	{
		_engine.WriteLineAsync("ucinewgame");
		Logger.LogSuccess($"New Game", this, LogCategory.UCI);
	}

	private static (string best, string ponder) ParseBestMove(string line)
	{
		if (!line.Contains("bestmove", StringComparison.OrdinalIgnoreCase))
		{
			return (string.Empty, string.Empty);
		}

		string[]? parts = line.Split(' ');
		if (parts.Length < 2)
		{
			return (string.Empty, string.Empty);
		}

		string bestMove   = parts[1];
		var    ponderMove = string.Empty;

		// Check if there's a ponder move
		if (parts.Length >= 4 && parts[2] == "ponder")
		{
			ponderMove = parts[3];
		}

		if (bestMove == "(none)")
		{
			bestMove = string.Empty;
		}

		return (bestMove, ponderMove);
	}

	private static int? ParseScoreMate(string line)
	{
		if (string.IsNullOrWhiteSpace(line))
		{
			return null;
		}

		string[]? tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

		for (var i = 0 ; i < tokens.Length - 1 ; i++)
		{
			if (tokens[i] == "score" && i + 2 < tokens.Length && tokens[i + 1] == "mate")
			{
				if (int.TryParse(tokens[i + 2], out int mateValue))
				{
					return mateValue;
				}
			}
		}

		return null;
	}

	private static string ParseCheckersInfo(string line)
	{
		var checkers = string.Empty;
		if (line.Contains("checkers", StringComparison.OrdinalIgnoreCase))
		{
			string[] parts = line.Split(' ');
			checkers = parts[1];
		}

		return checkers;
	}

	private async Task<FenInfo> RenewFenCache(CancellationToken ct = default)
	{
		List<string> lines    = await _engine.SendCommandAndReadOutputAsync("d", ct, [ "checkers" ]);
		var          fen      = "";
		var          checkers = "";

		foreach (string line in lines)
		{
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
		if (_currentPositionMovesCache != null)
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
		List<MoveClassification> classifiedMoves = legalMovesResult.LegalMoves
																   .Select(
																	   move => MoveClassifier.ClassifyMove(
																		   move,
																		   boardState)).ToList();

		_currentPositionMovesCache = classifiedMoves;

		Logger.LogInfo($"Cached {classifiedMoves.Count} legal moves for position", this, LogCategory.UCI);

		return classifiedMoves;
	}

	private void ClearPositionCaches()
	{
		_currentPositionMovesCache = null;
		_currentFenCache           = null;
		_goResultCache             = null;
	}

	private void ThrowIfDisposed()
	{
		if (_isDisposed || _isDisposing)
		{
			throw new ObjectDisposedException(
				nameof(UCIConnector),
				"Cannot use a disposed UCIConnector. Make sure you haven't called DisposeAsync()");
		}
	}
}

public readonly record struct FenInfo(
	string PiecePlacement,  char ActiveColor,   string CastlingRights,
	string EnPassantTarget, int  HalfmoveClock, int    FullmoveNumber, string Fen, string[] FenParts, string Checkers);

public readonly record struct GOResult(string BestMove, string PonderMove, int? ScoreMate);

public readonly record struct LegalMovesResult(IEnumerable<string> LegalMoves);
