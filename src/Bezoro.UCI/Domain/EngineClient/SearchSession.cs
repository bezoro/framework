using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Bezoro.UCI.Domain.EngineClient;

/// <summary>
///     Holds shared data for a search session for the duration of an active "go ..." command.
///     Tracks output lines and bestmove completion.
/// </summary>
internal sealed class SearchSession
{
	public SearchSession(bool ponder)
	{
		Ponder = ponder;
	}

	/// <summary>True if this session was started in pondering mode.</summary>
	public bool Ponder { get; }

	/// <summary>Collects all info/bestmove lines output during the search.</summary>
	public ConcurrentQueue<string> Lines { get; } = new();

	/// <summary>Completes when the engine emits "bestmove".</summary>
	public TaskCompletionSource<string> BestMoveCompletion { get; } =
		new(TaskCreationOptions.RunContinuationsAsynchronously);

	/// <summary>
	///     Returns a snapshot of captured lines in FIFO order.
	/// </summary>
	public IReadOnlyCollection<string> SnapshotLines() => Lines.ToList();

	/// <summary>
	///     Searches for the first captured output line that matches <paramref name="predicate" />.
	/// </summary>
	public string? FindFirstLine(Func<string, bool> predicate)
	{
		if (predicate is null) throw new ArgumentNullException(nameof(predicate));

		foreach (string line in Lines)
		{
			try
			{
				if (predicate(line)) return line;
			}
			catch
			{
				// Ignore predicate failures and continue searching.
			}
		}

		return null;
	}

	/// <summary>
	///     Adds an output line to the search session.
	/// </summary>
	public void AddLine(string line)
	{
		if (!string.IsNullOrWhiteSpace(line))
			Lines.Enqueue(line);
	}

	/// <summary>
	///     Completes the best move task with the supplied line.
	/// </summary>
	public void CompleteBestMove(string line) => BestMoveCompletion.TrySetResult(line);
}
