using System.Collections.Generic;
using System.Collections.Immutable;
using Bezoro.Chess.API.Types;

namespace Bezoro.Chess.Internal;

/// <summary>
///     Manages the move history for a chess game with undo/redo capability.
/// </summary>
internal sealed class GameHistory
{
	private readonly List<ChessMove>     _moves       = new();
	private readonly List<GameClock>     _clockStates = new();
	private readonly Stack<HistoryEntry> _redoStack   = new();
	private readonly Stack<HistoryEntry> _undoStack   = new();

    /// <summary>
    ///     Gets whether redo is available.
    /// </summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>
    ///     Gets whether undo is available.
    /// </summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>
    ///     Gets the last move played, or null if no moves have been played.
    /// </summary>
    public ChessMove? LastMove => _moves.Count > 0 ? _moves[^1] : null;

    /// <summary>
    ///     Gets an immutable list of moves for state snapshots.
    /// </summary>
    public ImmutableList<ChessMove> MovesImmutable => _moves.ToImmutableList();

    /// <summary>
    ///     Gets the number of moves played.
    /// </summary>
    public int Count => _moves.Count;

    /// <summary>
    ///     Gets the number of moves that can be redone.
    /// </summary>
    public int RedoCount => _redoStack.Count;

    /// <summary>
    ///     Gets the number of moves that can be undone.
    /// </summary>
    public int UndoCount => _undoStack.Count;

    /// <summary>
    ///     Gets the list of moves played so far.
    /// </summary>
    public IReadOnlyList<ChessMove> Moves => _moves;

    /// <summary>
    ///     Gets the move at the specified index.
    /// </summary>
    public ChessMove GetMove(int index) => _moves[index];

    /// <summary>
    ///     Gets the clock state before the specified move index.
    /// </summary>
    public GameClock GetClockBefore(int moveIndex) => _clockStates[moveIndex];

    /// <summary>
    ///     Redoes the last undone move.
    /// </summary>
    /// <returns>The entry that was redone, or null if nothing to redo.</returns>
    public HistoryEntry? Redo()
	{
		if (_redoStack.Count == 0)
			return null;

		var entry = _redoStack.Pop();
		_undoStack.Push(entry);

		_moves.Add(entry.Move);
		_clockStates.Add(entry.ClockBefore);

		return entry;
	}

    /// <summary>
    ///     Undoes the last move.
    /// </summary>
    /// <returns>The entry that was undone, or null if nothing to undo.</returns>
    public HistoryEntry? Undo()
	{
		if (_undoStack.Count == 0)
			return null;

		var entry = _undoStack.Pop();
		_redoStack.Push(entry);

		_moves.RemoveAt(_moves.Count - 1);
		_clockStates.RemoveAt(_clockStates.Count - 1);

		return entry;
	}

    /// <summary>
    ///     Redoes multiple moves.
    /// </summary>
    /// <param name="count">Number of moves to redo.</param>
    /// <returns>List of redone entries.</returns>
    public IReadOnlyList<HistoryEntry> Redo(int count)
	{
		var results = new List<HistoryEntry>();
		for (var i = 0; i < count && _redoStack.Count > 0; i++)
		{
			var entry = Redo();
			if (entry.HasValue)
				results.Add(entry.Value);
		}

		return results;
	}

    /// <summary>
    ///     Undoes multiple moves.
    /// </summary>
    /// <param name="count">Number of moves to undo.</param>
    /// <returns>List of undone entries.</returns>
    public IReadOnlyList<HistoryEntry> Undo(int count)
	{
		var results = new List<HistoryEntry>();
		for (var i = 0; i < count && _undoStack.Count > 0; i++)
		{
			var entry = Undo();
			if (entry.HasValue)
				results.Add(entry.Value);
		}

		return results;
	}

    /// <summary>
    ///     Gets the UCI move notation strings.
    /// </summary>
    public IReadOnlyList<string> GetUciMoves()
	{
		var result = new List<string>(_moves.Count);
		foreach (var move in _moves)
			result.Add(move.Notation);

		return result;
	}

    /// <summary>
    ///     Adds a move to the history, clearing any redo stack.
    /// </summary>
    /// <param name="move">The move that was played.</param>
    /// <param name="clockBefore">The clock state before the move.</param>
    public void AddMove(ChessMove move, GameClock clockBefore)
	{
		// Clear redo stack when a new move is made
		_redoStack.Clear();

		// Push to undo stack
		_undoStack.Push(new(move, clockBefore));

		// Add to move list
		_moves.Add(move);
		_clockStates.Add(clockBefore);
	}

    /// <summary>
    ///     Clears all history.
    /// </summary>
    public void Clear()
	{
		_undoStack.Clear();
		_redoStack.Clear();
		_moves.Clear();
		_clockStates.Clear();
	}
}

/// <summary>
///     Represents a single entry in the game history.
/// </summary>
/// <param name="Move">The move that was played.</param>
/// <param name="ClockBefore">The clock state before the move was played.</param>
internal readonly record struct HistoryEntry(ChessMove Move, GameClock ClockBefore);
