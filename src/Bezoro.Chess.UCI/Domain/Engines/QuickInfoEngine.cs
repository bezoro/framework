using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.Chess.UCI.API.Types;

namespace Bezoro.Chess.UCI.Domain.Engines;

internal sealed class QuickInfoEngine : IAsyncDisposable, IDisposable
{
	private readonly ConcurrentDictionary<(string Fen, uint Depth), SearchResult> _evalCache = new();

	// Caching
	private readonly object                       _cacheLock    = new();
	private readonly SemaphoreSlim                _positionLock = new(1, 1);
	private readonly UciEngineClient              _client;
	private          bool                         _disposed;
	private          Fen?                         _currentFenCache;
	private          IReadOnlyCollection<string>? _legalMovesCache;

	public QuickInfoEngine(string enginePath, IEnumerable<string>? args = null, string? workingDirectory = null)
		: this(new UciEngineClient(new ProcessUciTransport(enginePath, args, workingDirectory))) { }

	internal QuickInfoEngine(UciEngineClient client)
	{
		_client = client ?? throw new ArgumentNullException(nameof(client));
	}

	public bool IsHealthy => _client.IsHealthy;
	public bool IsStarted => _client.IsStarted;

	public EngineActivity Activity => _client.Activity;
	internal IReadOnlyList<UciEngineOption> AvailableOptions => _client.AvailableOptions;
	internal UciEngineCapabilities Capabilities => _client.Capabilities;
	internal UciEngineInfo EngineInfo => _client.EngineInfo;

	public TransportStatus Status => _client.Status;

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

	public async Task SetOptionAsync(string name, string? value, CancellationToken ct = default)
	{
		await _positionLock.WaitAsync(ct).ConfigureAwait(false);
		try
		{
			await _client.SetOptionAsync(name, value, ct).ConfigureAwait(false);
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

	public async Task SetDebugAsync(bool enabled, CancellationToken ct = default)
	{
		await _positionLock.WaitAsync(ct).ConfigureAwait(false);
		try
		{
			await _client.SetDebugAsync(enabled, ct).ConfigureAwait(false);
		}
		finally
		{
			_positionLock.Release();
		}
	}

	public async Task RegisterAsync(UciRegistration registration, CancellationToken ct = default)
	{
		await _positionLock.WaitAsync(ct).ConfigureAwait(false);
		try
		{
			await _client.RegisterAsync(registration, ct).ConfigureAwait(false);
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
			await ProbeCapabilitiesAsync(ct).ConfigureAwait(false);
			lock (_cacheLock)
			{
				if (_client.Capabilities.DisplayBoardFen != UciCapabilityState.Supported)
					_currentFenCache = null;
				if (_client.Capabilities.PerftMoveListing != UciCapabilityState.Supported)
					_legalMovesCache = null;
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

			EnsureCapabilitySupported(
				_client.Capabilities.DisplayBoardFen,
				"engine FEN retrieval via the non-standard 'd' command"
			);

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

			EnsureCapabilitySupported(
				_client.Capabilities.PerftMoveListing,
				"legal-move enumeration via the non-standard 'go perft 1' command"
			);

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

	public Task<SearchResult> SearchAsync(
		SearchParameters      parameters,
		Fen                   fen,
		IEnumerable<string>?  moves = null,
		CancellationToken     ct    = default)
	{
		if (IsDepthOnlySearch(parameters))
			return EvaluatePositionAsync(fen, moves, parameters.Depth ?? 6, ct);

		return ExecuteSearchAsync(fen, moves, parameters, ct);
	}

	public Task<SearchResult> QuickEvalAsync(Fen fen, uint depth = 6, CancellationToken ct = default) =>
		EvaluatePositionAsync(fen, null, depth, ct);

	public async Task<SearchResult> EvaluatePositionAsync(
		Fen                  fen,
		IEnumerable<string>? moves,
		uint                 depth = 6,
		CancellationToken    ct    = default)
	{
		await _positionLock.WaitAsync(ct).ConfigureAwait(false);
		try
		{
			var movesList = moves?.ToList();
			if (movesList is not { Count: > 0 })
			{
				var key = (fen.ToString(), depth);
				if (_evalCache.TryGetValue(key, out var cached)) return cached;

				await _client.SetPositionAsync(fen, null, ct).ConfigureAwait(false);
				var result = await _client.GoAsync(new() { Depth = depth }, ct).ConfigureAwait(false);

				_evalCache.TryAdd(key, result);

				lock (_cacheLock)
				{
					_currentFenCache = fen;
					_legalMovesCache = null;
				}

				return result;
			}

			await _client.SetPositionAsync(fen, movesList, ct).ConfigureAwait(false);
			var movedResult = await _client.GoAsync(new() { Depth = depth }, ct).ConfigureAwait(false);

			lock (_cacheLock)
			{
				ClearPositionDependentCaches_NoLock();
			}

			return movedResult;
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

	private async Task<SearchResult> ExecuteSearchAsync(
		Fen                  fen,
		IEnumerable<string>? moves,
		SearchParameters     parameters,
		CancellationToken    ct)
	{
		await _positionLock.WaitAsync(ct).ConfigureAwait(false);
		try
		{
			var movesList = moves?.ToList();

			await _client.SetPositionAsync(fen, movesList, ct).ConfigureAwait(false);
			var result = await _client.GoAsync(parameters, ct).ConfigureAwait(false);

			lock (_cacheLock)
			{
				if (movesList is { Count: > 0 })
				{
					ClearPositionDependentCaches_NoLock();
				}
				else
				{
					_currentFenCache = fen;
					_legalMovesCache = null;
				}
			}

			return result;
		}
		catch
		{
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

	private static bool IsDepthOnlySearch(SearchParameters parameters) =>
		!parameters.Infinite &&
		!parameters.Ponder &&
		parameters.SearchMoves is null &&
		parameters.BlackIncrementMs is null &&
		parameters.BlackTimeMs is null &&
		parameters.Mate is null &&
		parameters.MovesToGo is null &&
		parameters.MoveTimeMs is null &&
		parameters.WhiteIncrementMs is null &&
		parameters.WhiteTimeMs is null &&
		parameters.Nodes is null;

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
		// Clear eval cache when position changes to avoid returning stale results
		_evalCache.Clear();
	}

	private static void EnsureCapabilitySupported(UciCapabilityState capability, string capabilityName)
	{
		if (capability == UciCapabilityState.Supported) return;

		throw new NotSupportedException(
			$"The connected engine does not support {capabilityName}, which is required for this operation."
		);
	}

	private async Task ProbeCapabilitiesAsync(CancellationToken ct)
	{
		await _client.SetPositionAsync(Fen.Default, null, ct).ConfigureAwait(false);

		UciCapabilityState displayBoardFen = UciCapabilityState.Unsupported;
		UciCapabilityState perftMoveListing = UciCapabilityState.Unsupported;
		Fen? probedFen = null;
		IReadOnlyCollection<string>? probedMoves = null;

		try
		{
			probedFen = await _client.GetFenViaDAsync(ct).ConfigureAwait(false);
			displayBoardFen = probedFen.HasValue
								  ? UciCapabilityState.Supported
								  : UciCapabilityState.Unsupported;
		}
		catch (OperationCanceledException) when (!ct.IsCancellationRequested)
		{
			displayBoardFen = UciCapabilityState.Unsupported;
		}
		catch (Exception) when (!ct.IsCancellationRequested)
		{
			displayBoardFen = UciCapabilityState.Unsupported;
		}

		try
		{
			probedMoves = await _client.GetLegalMovesViaGoPerft1Async(ct).ConfigureAwait(false);
			perftMoveListing = probedMoves.Count > 0
								   ? UciCapabilityState.Supported
								   : UciCapabilityState.Unsupported;
		}
		catch (OperationCanceledException) when (!ct.IsCancellationRequested)
		{
			perftMoveListing = UciCapabilityState.Unsupported;
		}
		catch (Exception) when (!ct.IsCancellationRequested)
		{
			perftMoveListing = UciCapabilityState.Unsupported;
		}

		_client.SetExtensionCapabilities(displayBoardFen, perftMoveListing);

		lock (_cacheLock)
		{
			ClearPositionDependentCaches_NoLock();
			if (displayBoardFen == UciCapabilityState.Supported && probedFen.HasValue)
				_currentFenCache = probedFen;
			if (perftMoveListing == UciCapabilityState.Supported && probedMoves is { })
				_legalMovesCache = probedMoves;
		}
	}
}
