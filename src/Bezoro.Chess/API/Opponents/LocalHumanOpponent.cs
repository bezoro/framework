using System;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.Chess.API.Abstractions;
using Bezoro.Chess.API.Types;

namespace Bezoro.Chess.API.Opponents;

/// <summary>
///     Local human opponent for same-device two-player games.
///     Moves are submitted externally via the SubmitMove method.
/// </summary>
public sealed class LocalHumanOpponent : IOpponent
{
	private readonly object                        _sync = new();
	private          bool                          _isDisposed;
	private          TaskCompletionSource<string>? _pendingMove;

	/// <inheritdoc />
	public event Action? Disconnected;

	/// <inheritdoc />
	public event Action? DrawOffered;

	/// <inheritdoc />
	public event Action<Exception>? Error;

	/// <inheritdoc />
	public event Action<string>? MoveSubmitted;

	/// <inheritdoc />
	public event Action? Resigned;

	/// <summary>
	///     Creates a new local human opponent.
	/// </summary>
	/// <param name="profile">The player's profile.</param>
	public LocalHumanOpponent(PlayerProfile profile)
	{
		Profile = profile;
	}

	/// <summary>
	///     Creates a new local human opponent with a generated profile.
	/// </summary>
	/// <param name="displayName">The player's display name.</param>
	public LocalHumanOpponent(string displayName)
	{
		Profile = PlayerProfile.Create(displayName);
	}

	/// <inheritdoc />
	public bool IsReady => true;

	/// <summary>
	///     Gets whether a move is currently expected from this opponent.
	/// </summary>
	public bool IsWaitingForMove
	{
		get
		{
			lock (_sync)
			{
				return _pendingMove != null && !_pendingMove.Task.IsCompleted;
			}
		}
	}

	/// <inheritdoc />
	public OpponentType Type => OpponentType.LocalHuman;

	/// <inheritdoc />
	public PlayerProfile Profile { get; }

	/// <summary>
	///     Submits a move for this opponent.
	///     Call this when the human player makes their move.
	/// </summary>
	/// <param name="move">The move in UCI notation (e.g., "e2e4").</param>
	/// <returns>True if the move was accepted, false if no move was pending.</returns>
	public bool SubmitMove(string move)
	{
		TaskCompletionSource<string>? tcs;
		lock (_sync)
		{
			tcs = _pendingMove;
		}

		if (tcs == null)
			return false;

		if (tcs.TrySetResult(move))
		{
			MoveSubmitted?.Invoke(move);
			return true;
		}

		return false;
	}

	/// <inheritdoc />
	public Task InitializeAsync(CancellationToken ct = default) =>
		// Local human opponent needs no initialization
		Task.CompletedTask;

	/// <inheritdoc />
	public Task NotifyMovePlayedAsync(string move, GameState state, CancellationToken ct = default) =>
		// Local human doesn't need to be notified of moves
		Task.CompletedTask;

	/// <inheritdoc />
	public async Task<string?> GetMoveAsync(GameState state, CancellationToken ct = default)
	{
		// Create a task completion source for the move
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
	public ValueTask DisposeAsync()
	{
		if (_isDisposed)
			return default;

		_isDisposed = true;

		// Cancel any pending move
		TaskCompletionSource<string>? tcs;
		lock (_sync)
		{
			tcs          = _pendingMove;
			_pendingMove = null;
		}

		tcs?.TrySetCanceled();

		return default;
	}

	/// <summary>
	///     Offers a draw.
	/// </summary>
	public void OfferDraw()
	{
		DrawOffered?.Invoke();
	}

	/// <summary>
	///     Resigns the game for this opponent.
	/// </summary>
	public void Resign()
	{
		// Cancel any pending move
		TaskCompletionSource<string>? tcs;
		lock (_sync)
		{
			tcs          = _pendingMove;
			_pendingMove = null;
		}

		tcs?.TrySetCanceled();
		Resigned?.Invoke();
	}
}
