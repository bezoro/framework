using System;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.Chess.API.Abstractions;
using Bezoro.Chess.API.Types;

namespace Bezoro.Chess.API.Opponents;

/// <summary>
///     Remote human opponent for online multiplayer games.
///     Uses IRemoteGameService for network communication.
/// </summary>
public sealed class RemoteOpponent : IOpponent
{
	private readonly IRemoteGameService _remoteService;
	private readonly string             _matchId;
	private readonly object             _sync = new();

	private TaskCompletionSource<string>? _pendingMove;
	private bool                          _isDisposed;

	/// <summary>
	///     Creates a new remote opponent from a matched game.
	/// </summary>
	/// <param name="remoteService">The remote game service.</param>
	/// <param name="match">The matched game details.</param>
	public RemoteOpponent(IRemoteGameService remoteService, RemoteMatch match)
	{
		_remoteService = remoteService;
		_matchId       = match.MatchId;
		Profile        = match.Opponent;

		// Subscribe to remote service events
		_remoteService.OpponentMoved        += OnOpponentMoved;
		_remoteService.OpponentResigned     += OnOpponentResigned;
		_remoteService.DrawOffered          += OnDrawOffered;
		_remoteService.OpponentDisconnected += OnOpponentDisconnected;
		_remoteService.Error                += OnRemoteError;
	}

	/// <inheritdoc />
	public OpponentType Type => OpponentType.RemoteHuman;

	/// <inheritdoc />
	public PlayerProfile Profile { get; }

	/// <inheritdoc />
	public bool IsReady => _remoteService.IsConnected;

	/// <summary>
	///     Gets the match ID for this remote game.
	/// </summary>
	public string MatchId => _matchId;

	/// <inheritdoc />
	public event Action<string>? MoveSubmitted;

	/// <inheritdoc />
	public event Action? Resigned;

	/// <inheritdoc />
	public event Action? DrawOffered;

	/// <inheritdoc />
	public event Action? Disconnected;

	/// <inheritdoc />
	public event Action<Exception>? Error;

	/// <inheritdoc />
	public Task InitializeAsync(CancellationToken ct = default)
	{
		// Remote opponent is already initialized through the match
		// Connection should already be established
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public async Task<string?> GetMoveAsync(GameState state, CancellationToken ct = default)
	{
		// Create a task completion source to wait for the opponent's move
		var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

		lock (_sync)
		{
			_pendingMove = tcs;
		}

		using var registration = ct.Register(() => tcs.TrySetCanceled(ct));

		try
		{
			return await tcs.Task.ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			return null;
		}
		finally
		{
			lock (_sync)
			{
				_pendingMove = null;
			}
		}
	}

	/// <inheritdoc />
	public async Task NotifyMovePlayedAsync(string move, GameState state, CancellationToken ct = default)
	{
		// Send our move to the opponent
		try
		{
			await _remoteService.SendMoveAsync(_matchId, move, ct).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			Error?.Invoke(ex);
			throw;
		}
	}

	/// <summary>
	///     Resigns the game.
	/// </summary>
	/// <param name="ct">Cancellation token.</param>
	public async Task ResignAsync(CancellationToken ct = default)
	{
		try
		{
			await _remoteService.ResignAsync(_matchId, ct).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			Error?.Invoke(ex);
		}
	}

	/// <summary>
	///     Offers a draw to the opponent.
	/// </summary>
	/// <param name="ct">Cancellation token.</param>
	public async Task OfferDrawAsync(CancellationToken ct = default)
	{
		try
		{
			await _remoteService.OfferDrawAsync(_matchId, ct).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			Error?.Invoke(ex);
		}
	}

	/// <summary>
	///     Accepts a draw offer from the opponent.
	/// </summary>
	/// <param name="ct">Cancellation token.</param>
	public async Task AcceptDrawAsync(CancellationToken ct = default)
	{
		try
		{
			await _remoteService.AcceptDrawAsync(_matchId, ct).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			Error?.Invoke(ex);
		}
	}

	/// <summary>
	///     Declines a draw offer from the opponent.
	/// </summary>
	/// <param name="ct">Cancellation token.</param>
	public async Task DeclineDrawAsync(CancellationToken ct = default)
	{
		try
		{
			await _remoteService.DeclineDrawAsync(_matchId, ct).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			Error?.Invoke(ex);
		}
	}

	/// <inheritdoc />
	public ValueTask DisposeAsync()
	{
		if (_isDisposed)
			return default;

		_isDisposed = true;

		// Unsubscribe from events
		_remoteService.OpponentMoved        -= OnOpponentMoved;
		_remoteService.OpponentResigned     -= OnOpponentResigned;
		_remoteService.DrawOffered          -= OnDrawOffered;
		_remoteService.OpponentDisconnected -= OnOpponentDisconnected;
		_remoteService.Error                -= OnRemoteError;

		// Cancel any pending move
		TaskCompletionSource<string>? tcs;
		lock (_sync)
		{
			tcs = _pendingMove;
			_pendingMove = null;
		}

		tcs?.TrySetCanceled();

		return default;
	}

	private void OnOpponentMoved(string move)
	{
		TaskCompletionSource<string>? tcs;
		lock (_sync)
		{
			tcs = _pendingMove;
		}

		if (tcs != null && tcs.TrySetResult(move))
		{
			MoveSubmitted?.Invoke(move);
		}
	}

	private void OnOpponentResigned()
	{
		// Cancel any pending move
		TaskCompletionSource<string>? tcs;
		lock (_sync)
		{
			tcs = _pendingMove;
			_pendingMove = null;
		}

		tcs?.TrySetCanceled();
		Resigned?.Invoke();
	}

	private void OnDrawOffered()
	{
		DrawOffered?.Invoke();
	}

	private void OnOpponentDisconnected()
	{
		// Cancel any pending move
		TaskCompletionSource<string>? tcs;
		lock (_sync)
		{
			tcs = _pendingMove;
			_pendingMove = null;
		}

		tcs?.TrySetCanceled();
		Disconnected?.Invoke();
	}

	private void OnRemoteError(Exception ex)
	{
		Error?.Invoke(ex);
	}
}

