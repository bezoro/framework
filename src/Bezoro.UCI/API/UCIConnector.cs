using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.Core.Common.Extensions;
using Bezoro.UCI.API.Types;
using Bezoro.UCI.Domain;
using Bezoro.UCI.Domain.Common.Constants;

namespace Bezoro.UCI.API;

/// <summary>
///     Represents a connection to a UCI-compliant chess engine.
///     This class handles process management, command serialization, and asynchronous communication.
///     It is designed to be thread-safe and robust using the Command pattern.
/// </summary>
public sealed class UciConnector : IAsyncDisposable
{
	private readonly Process?  _engineProcess;
	private readonly string    _enginePath;
	private readonly UciEngine _engine;

	private volatile int _isDisposed;
	private volatile int _isDisposing;

	/// <summary>
	///     Initializes a new instance of the <see cref="UciConnector" /> class.
	/// </summary>
	/// <param name="enginePath">The file path to the UCI engine executable.</param>
	public UciConnector(string enginePath)
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

	public async Task IsReadyAsync(CancellationToken ct = default)
	{
		ThrowIfDisposed();
		await _engine.WaitReadyAsync(ct).ConfigureAwait(false);
	}

	public async Task NewGameAsync()
	{
		ThrowIfDisposed();
		await _engine.NewGameAsync().ConfigureAwait(false);
	}

	public async Task PonderHit(CancellationToken ct = default)
	{
		ThrowIfDisposed();
		await _engine.PonderhitAsync(ct).ConfigureAwait(false);
	}

	public async Task QuitEngineAsync(CancellationToken ct)
	{
		ThrowIfDisposed();
		await _engine.QuitEngineAsync(ct).ConfigureAwait(false);
	}

	public async Task SetDefaultPositionAsync(CancellationToken ct)
	{
		ThrowIfDisposed();
		await _engine.SetPositionAsync(UciConstants.STANDARD_FEN, ct).ConfigureAwait(false);
	}

	public async Task SetOptionAsync(string name, int value, CancellationToken ct = default)
	{
		ThrowIfDisposed();
		await _engine.SetOptionAsync(name, value, ct).ConfigureAwait(false);
	}

	public async Task SetPositionAsync(
		string               fen,
		IEnumerable<string>? moves = null,
		CancellationToken    ct    = default)
	{
		ThrowIfDisposed();
		await _engine.SetPositionAsync(fen, ct, moves).ConfigureAwait(false);
	}

	public async Task StartEngineAsync()
	{
		ThrowIfDisposed();
		await _engine.StartEngineAsync().ConfigureAwait(false);
	}

	public async Task StopSearchAsync(CancellationToken ct = default)
	{
		ThrowIfDisposed();
		await _engine.StopSearchAsync(ct).ConfigureAwait(false);
	}

	public async Task<Fen> GetCurrentFenAsync(CancellationToken ct = default)
	{
		ThrowIfDisposed();
		return await _engine.GetCurrentFenAsync(ct).ConfigureAwait(false);
	}

	/// <summary>
	///     Gets all legal moves from the current engine state.
	/// </summary>
	/// <param name="ct">A token to cancel the operation.</param>
	public async Task<IReadOnlyCollection<Move>> GetLegalMovesAsync(CancellationToken ct = default)
	{
		ThrowIfDisposed();
		return await _engine.GetLegalMovesAsync(ct).ConfigureAwait(false);
	}

	public async Task<IReadOnlyCollection<Move>> GetLegalMovesForSquareAsync(
		string            square,
		CancellationToken ct = default)
	{
		ThrowIfDisposed();
		return await _engine.GetLegalMovesForSquareAsync(square, ct).ConfigureAwait(false);
	}

	public async Task<SearchResult> GO(uint depth = 5, CancellationToken ct = default)
	{
		ThrowIfDisposed();
		return await _engine.GO(depth, ct).ConfigureAwait(false);
	}

	public async Task<SearchResult> GoPerftOne(CancellationToken ct = default)
	{
		ThrowIfDisposed();
		return await _engine.GoPerftOne(ct).ConfigureAwait(false);
	}

	public async Task<SearchResult> StartSearchForSecondsAsync(
		uint              seconds,
		CancellationToken ct = default)
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
