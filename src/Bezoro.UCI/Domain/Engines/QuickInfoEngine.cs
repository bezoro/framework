using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.UCI.API.Types;

namespace Bezoro.UCI.Domain.Engines;

internal sealed class QuickInfoEngine : IAsyncDisposable, IDisposable
{
	private readonly ConcurrentDictionary<(string Fen, uint Depth), SearchResult> _evalCache = new();

	// Caching
	private readonly object                      _cacheLock    = new();
	private readonly SemaphoreSlim               _positionLock = new(1, 1);
	private readonly UciEngineClient             _client;
	private          bool                        _disposed;
	private          Fen?                        _currentFenCache;
	private          IReadOnlyCollection<string> _legalMovesCache;

	public QuickInfoEngine(string enginePath, IEnumerable<string>? args = null, string? workingDirectory = null)
	{
		var transport = new ProcessUciTransport(enginePath, args, workingDirectory);
		_client = new(transport);
	}

	public bool IsHealthy => _client.IsHealthy;
	public bool IsStarted => _client.IsStarted;

	public EngineActivity Activity => _client.Activity;

	public ProcessUciTransport.TransportStatus Status => _client.Status;

	public async Task NewGameAsync(CancellationToken ct = default)
	{
		await _positionLock.WaitAsync(ct).ConfigureAwait(false);
		try
		{
			await _client.UciNewGameAsync(ct).ConfigureAwait(false);
			lock (_cacheLock)
			{
				ClearPositionDependentCaches_NoLock();
			}
		}
		finally
		{
			_positionLock.Release();
		}
	}

	public async Task SetPositionAsync(Fen fen, IEnumerable<string>? moves = null, CancellationToken ct = default)
	{
		await _positionLock.WaitAsync(ct).ConfigureAwait(false);
		try
		{
			await _client.SetPositionAsync(fen, moves, ct).ConfigureAwait(false);

			lock (_cacheLock)
			{
				// Position changed; invalidate caches
				ClearPositionDependentCaches_NoLock();

				// If no moves were applied, the engine's position equals provided fen; cache it
				if (moves == null || !moves.Any()) _currentFenCache = fen;
			}
		}
		catch
		{
			// On failure/cancellation, conservatively invalidate caches
			lock (_cacheLock)
			{
				ClearPositionDependentCaches_NoLock();
			}

			throw;
		}
		finally
		{
			_positionLock.Release();
		}
	}

	public async Task StartAsync(CancellationToken ct = default)
	{
		await _positionLock.WaitAsync(ct).ConfigureAwait(false);
		try
		{
			await _client.StartAsync(ct).ConfigureAwait(false);
			lock (_cacheLock)
			{
				ClearPositionDependentCaches_NoLock();
			}
		}
		finally
		{
			_positionLock.Release();
		}
	}

	public async Task StopAsync(CancellationToken ct = default)
	{
		await _positionLock.WaitAsync(ct).ConfigureAwait(false);
		try
		{
			await _client.StopAsync(ct).ConfigureAwait(false);
			lock (_cacheLock)
			{
				ClearPositionDependentCaches_NoLock();
			}
		}
		finally
		{
			_positionLock.Release();
		}
	}

	public async Task<Fen?> GetCurrentFenAsync(CancellationToken ct = default)
	{
		await _positionLock.WaitAsync(ct).ConfigureAwait(false);
		try
		{
			lock (_cacheLock)
			{
				if (_currentFenCache is { }) return _currentFenCache;
			}

			var fen = await _client.GetFenViaDAsync(ct).ConfigureAwait(false);
			lock (_cacheLock)
			{
				_currentFenCache = fen;
			}

			return fen;
		}
		finally
		{
			_positionLock.Release();
		}
	}

	public async Task<IReadOnlyCollection<string>> GetLegalMovesAsync(CancellationToken ct = default)
	{
		await _positionLock.WaitAsync(ct).ConfigureAwait(false);
		try
		{
			lock (_cacheLock)
			{
				if (_legalMovesCache is { }) return _legalMovesCache;
			}

			var moves = await _client.GetLegalMovesViaGoPerft1Async(ct).ConfigureAwait(false);
			lock (_cacheLock)
			{
				_legalMovesCache = moves;
			}

			return moves;
		}
		finally
		{
			_positionLock.Release();
		}
	}

	public async Task<SearchResult> QuickEvalAsync(Fen fen, uint depth = 6, CancellationToken ct = default)
	{
		await _positionLock.WaitAsync(ct).ConfigureAwait(false);
		try
		{
			var key = (fen.ToString(), depth);
			if (_evalCache.TryGetValue(key, out var cached)) return cached;

			await _client.SetPositionAsync(fen, null, ct).ConfigureAwait(false);
			var result = await _client.GoAsync(new() { Depth = depth }, ct).ConfigureAwait(false);

			_evalCache.TryAdd(key, result);

			lock (_cacheLock)
			{
				// Engine is now set to 'fen'
				_currentFenCache = fen;
				// We didn't compute legal moves here; invalidate to force recomputation if requested
				_legalMovesCache = null;
			}

			return result;
		}
		catch
		{
			// On failure/cancellation, conservatively invalidate caches
			lock (_cacheLock)
			{
				ClearPositionDependentCaches_NoLock();
			}

			throw;
		}
		finally
		{
			_positionLock.Release();
		}
	}

	public async ValueTask DisposeAsync()
	{
		if (_disposed) return;

		_disposed = true;

		try
		{
			await _client.DisposeAsync().ConfigureAwait(false);
		}
		finally
		{
			_positionLock.Dispose();
		}
	}

	public void Dispose()
	{
		DisposeAsync().AsTask().GetAwaiter().GetResult();
		GC.SuppressFinalize(this);
	}

	private void ClearPositionDependentCaches_NoLock()
	{
		_currentFenCache = null;
		_legalMovesCache = null;
	}
}
