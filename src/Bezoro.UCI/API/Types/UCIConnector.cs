using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.Core.Common.Extensions;
using Bezoro.UCI.Domain;
using Bezoro.UCI.Domain.Commands;
using Bezoro.UCI.Domain.Common.Constants;

namespace Bezoro.UCI.API.Types;

/// <summary>
///     Represents a connection to a UCI-compliant chess engine.
///     This class handles process management, command serialization, and asynchronous communication.
///     It exposes a safe, ergonomic, and consumer-friendly public API on top of the internal engine wrapper.
/// </summary>
public sealed class UciConnector : IAsyncDisposable
{
	private readonly Process?  _engineProcess;
	private readonly UciEngine _engine;

	private volatile int _isDisposed;
	private volatile int _isDisposing;

	/// <summary>
	///     Initializes a new instance of the <see cref="UciConnector" /> class.
	/// </summary>
	/// <param name="enginePath">The full file path to the UCI engine executable.</param>
	/// <exception cref="ArgumentException">Thrown when the path is null or whitespace.</exception>
	/// <exception cref="FileNotFoundException">Thrown when the executable cannot be found.</exception>
	public UciConnector(string enginePath)
	{
		if (string.IsNullOrWhiteSpace(enginePath))
			throw new ArgumentException("Engine path must be provided.", nameof(enginePath));

		EnginePath = enginePath;
		_engineProcess = new()
		{
			StartInfo = new()
			{
				FileName               = EnginePath,
				RedirectStandardInput  = true,
				RedirectStandardOutput = true,
				UseShellExecute        = false,
				CreateNoWindow         = true
			},
			EnableRaisingEvents = true
		};

		_engine = new(_engineProcess);
		Logger.LogSuccess("Engine Process Created", this, LogCategory.UCI);
	}

	/// <summary>Indicates whether this connector is disposed (or in the middle of disposing).</summary>
	public bool IsDisposed => _isDisposed.IsPositive() || _isDisposing.IsPositive();

	/// <summary>Indicates whether the underlying engine handshake completed and I/O is available.</summary>
	public bool IsStarted => _engine.IsStarted;

	/// <summary>Full path to the engine executable.</summary>
	public string EnginePath { get; }

	/// <summary>
	///     Sends an "isready" probe and waits until the engine reports ready.
	///     Requires the engine to be started.
	/// </summary>
	public async Task IsReadyAsync(CancellationToken ct)
	{
		ThrowIfDisposed();

		await _engine.WaitReadyAsync(ct).ConfigureAwait(false);
	}

	/// <summary>Sets the engine position using a FEN and an optional move list.</summary>
	public async Task MakeMoveAsync(string moveNotation, CancellationToken ct)
	{
		moveNotation.ThrowIfNull().ThrowIfEmpty();
		ThrowIfDisposed();

		var command = new PositionCommand(await _engine.GetCurrentFenAsync(ct), [moveNotation]);
		await _engine.SetPositionAsync(command, ct).ConfigureAwait(false);
	}

	/// <summary>Signals a new game and clears engine-side and local caches.</summary>
	public async Task NewGameAsync(CancellationToken ct)
	{
		ThrowIfDisposed();

		await _engine.NewGameAsync(ct).ConfigureAwait(false);
	}

	/// <summary>Notifies the engine that the side to move has played the pondered move.</summary>
	public async Task PonderHitAsync(CancellationToken ct)
	{
		ThrowIfDisposed();

		await _engine.PonderhitAsync(ct).ConfigureAwait(false);
	}

	/// <summary>
	///     Asks the engine to stop searching. If the engine was not started, this is a no-op.
	/// </summary>
	public async Task QuitEngineAsync(CancellationToken ct)
	{
		ThrowIfDisposed();
		if (!IsStarted) return; // idempotent / no-op if not started

		await _engine.QuitEngineAsync(ct).ConfigureAwait(false);
	}

	/// <summary>Sets the standard starting position (initial chess position).</summary>
	public async Task SetDefaultPositionAsync(CancellationToken ct)
	{
		ThrowIfDisposed();

		await _engine.SetPositionAsync(new(UciConstants.STANDARD_FEN), ct).ConfigureAwait(false);
	}

	/// <summary>Sets an engine option by name to an integer value.</summary>
	public async Task SetOptionAsync(string name, int value, CancellationToken ct)
	{
		ThrowIfDisposed();
		if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Option name must be provided.", nameof(name));

		await _engine.SetOptionAsync(name, value, ct).ConfigureAwait(false);
	}

	/// <summary>Starts the engine process and performs the UCI handshake.</summary>
	public async Task StartEngineAsync()
	{
		ThrowIfDisposed();
		await _engine.StartEngineAsync().ConfigureAwait(false);
	}

	/// <summary>Stops any ongoing search and waits for a bestmove.</summary>
	public async Task StopSearchAsync(CancellationToken ct)
	{
		ThrowIfDisposed();

		await _engine.StopSearchAsync(ct).ConfigureAwait(false);
	}

	/// <summary>Gets the current FEN string from the engine.</summary>
	public async Task<Fen> GetCurrentFenAsync(CancellationToken ct)
	{
		ThrowIfDisposed();

		return await _engine.GetCurrentFenAsync(ct).ConfigureAwait(false);
	}

	/// <summary>
	///     Gets all legal moves from the current engine state.
	/// </summary>
	/// <param name="ct">A token to cancel the operation.</param>
	public async Task<IReadOnlyCollection<Move>> GetLegalMovesAsync(CancellationToken ct)
	{
		ThrowIfDisposed();

		return await _engine.GetLegalMovesAsync(ct).ConfigureAwait(false);
	}

	/// <summary>Gets legal moves originating from a specific square (e.g., "e2").</summary>
	public async Task<IReadOnlyCollection<Move>> GetLegalMovesForSquareAsync(
		string            square,
		CancellationToken ct)
	{
		ThrowIfDisposed();

		if (string.IsNullOrWhiteSpace(square)) throw new ArgumentException("Square must be provided.", nameof(square));

		return await _engine.GetLegalMovesForSquareAsync(square, ct).ConfigureAwait(false);
	}

	/// <summary>Starts a depth-limited search and returns the parsed result.</summary>
	public async Task<SearchResult> GoAsync(uint depth, CancellationToken ct)
	{
		ThrowIfDisposed();

		return await _engine.GO(depth, ct).ConfigureAwait(false);
	}

	/// <summary>Runs a perft-depth=1 enumeration and returns the parsed result.</summary>
	public async Task<SearchResult> GoPerftOneAsync(CancellationToken ct)
	{
		ThrowIfDisposed();

		return await _engine.GoPerftOne(ct).ConfigureAwait(false);
	}

	/// <summary>Starts a time-limited search and returns when the best move is reported.</summary>
	public async Task<SearchResult> StartSearchForSecondsAsync(
		uint              seconds,
		CancellationToken ct)
	{
		ThrowIfDisposed();

		return await _engine.StartSearchForSecondsAsync(seconds, ct).ConfigureAwait(false);
	}

	public async ValueTask DisposeAsync()
	{
		// Ensure idempotent disposal
		if (Interlocked.Exchange(ref _isDisposed, 1) == 1) return;

		Interlocked.Exchange(ref _isDisposing, 1);

		try
		{
			// Try to gracefully stop the engine if it was started
			if (_engine.IsStarted)
			{
				try
				{
					await QuitEngineAsync(CancellationToken.None);
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
		}
		finally
		{
			Volatile.Write(ref _isDisposing, 0);
			GC.SuppressFinalize(this);
		}
	}

	private void ThrowIfDisposed()
	{
		if (_isDisposed.IsPositive() || _isDisposing.IsPositive())
		{
			throw new ObjectDisposedException(
				nameof(UciConnector),
				"Cannot use a disposed UCIConnector. Make sure you haven't called DisposeAsync()");
		}
	}
}
