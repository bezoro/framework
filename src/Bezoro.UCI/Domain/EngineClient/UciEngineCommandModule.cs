using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.UCI.API.Types;
using Bezoro.UCI.Domain.Common.Constants;

namespace Bezoro.UCI.Domain;

/// <summary>
///     Provides reusable command helpers for interacting with a UCI engine transport.
/// </summary>
internal sealed class UciEngineCommandModule(
	IUciTransport          transport,
	UciLineWaiterRegistry  lineWaiters,
	IUciLineSource         lineSource,
	Action<EngineActivity> setActivity
)
{
	private readonly Action<EngineActivity> _setActivity =
		setActivity ?? throw new ArgumentNullException(nameof(setActivity));
	private readonly IUciLineSource _lineSource = lineSource ?? throw new ArgumentNullException(nameof(lineSource));
	private readonly IUciTransport  _transport  = transport ?? throw new ArgumentNullException(nameof(transport));
	private readonly UciLineWaiterRegistry _lineWaiters =
		lineWaiters ?? throw new ArgumentNullException(nameof(lineWaiters));

	public async Task GoFireAndForgetAsync(SearchParameters parameters, CancellationToken ct)
	{
		string cmd = UciCommandBuilder.BuildGoCommand(parameters);
		await _transport.WriteLineAsync(cmd, ct).ConfigureAwait(false);
		_setActivity(parameters.Ponder ? EngineActivity.Pondering : EngineActivity.Searching);
	}

	public async Task IsReadyAsync(CancellationToken ct)
	{
		await _transport.WriteLineAsync(UciConstants.Commands.IS_READY, ct).ConfigureAwait(false);
		await _lineWaiters.WaitForAsync(
							  static l => l.Trim().Equals(
								  UciConstants.Responses.READY_OK,
								  StringComparison.OrdinalIgnoreCase),
							  TimeSpan.FromSeconds(10),
							  ct)
						  .ConfigureAwait(false);
	}

	public async Task SetOptionAsync(string name, string? value, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(name)) return;

		string cmd = value is null
						 ? $"{UciConstants.Commands.SET_OPTION} {UciConstants.Keywords.NAME} {name}"
						 : $"{UciConstants.Commands.SET_OPTION} {UciConstants.Keywords.NAME} {name} {UciConstants.Keywords.VALUE} {value}";

		await _transport.WriteLineAsync(cmd, ct).ConfigureAwait(false);
	}

	public async Task SetPositionAsync(Fen fen, IEnumerable<string>? moves, CancellationToken ct)
	{
		if (!Fen.Validate(fen.Raw))
			throw new ArgumentException("Invalid FEN provided.", nameof(fen));

		string movePart = moves != null && moves.Any()
							  ? $"{UciConstants.Keywords.MOVES} " + string.Join(' ', moves)
							  : string.Empty;

		await _transport.WriteLineAsync(
			$"{UciConstants.Commands.POSITION} {UciConstants.Keywords.FEN} {fen.Raw} {movePart}",
			ct).ConfigureAwait(false);
	}

	public async Task StopSearchAsync(CancellationToken ct)
	{
		await _transport.WriteLineAsync(UciConstants.Commands.STOP, ct).ConfigureAwait(false);
		_setActivity(EngineActivity.Idle);
	}

	public async Task UciInitAsync(CancellationToken ct)
	{
		await _transport.WriteLineAsync(UciConstants.Commands.UCI, ct).ConfigureAwait(false);
		await _lineWaiters.WaitForAsync(
							  static l => l.Trim().Equals(
								  UciConstants.Responses.UCI_OK,
								  StringComparison.OrdinalIgnoreCase),
							  TimeSpan.FromSeconds(5),
							  ct)
						  .ConfigureAwait(false);

		await IsReadyAsync(ct).ConfigureAwait(false);
	}

	public async Task UciNewGameAsync(CancellationToken ct)
	{
		await _transport.WriteLineAsync(UciConstants.Commands.UCI_NEW_GAME, ct).ConfigureAwait(false);
		await IsReadyAsync(ct).ConfigureAwait(false);
	}

	public async Task<Fen?> GetFenViaDAsync(CancellationToken ct)
	{
		var fenTask = _lineWaiters.WaitForAsync(static line => IsFenLine(line), TimeSpan.FromSeconds(2), ct);

		using var checkersCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
		var checkersTask = _lineWaiters.WaitForAsync(
			static line => IsCheckersLine(line),
			Timeout.InfiniteTimeSpan,
			checkersCts.Token);

		await _transport.WriteLineAsync(UciConstants.Commands.DISPLAY_BOARD, ct).ConfigureAwait(false);

		string fenLine;
		try
		{
			fenLine = await fenTask.ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (!ct.IsCancellationRequested)
		{
			await checkersCts.CancelAsync();
			return null;
		}

		if (!checkersTask.IsCompleted)
			checkersCts.CancelAfter(TimeSpan.FromMilliseconds(750));

		string? checkersLine;
		try
		{
			checkersLine = await checkersTask.ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (!ct.IsCancellationRequested)
		{
			checkersLine = null;
		}

		string? rawFenCache = null;
		_ = Fen.TryParseUciOutputLine(fenLine, ref rawFenCache, out var fenFromFen);

		if (checkersLine is null) return fenFromFen;

		_ = Fen.TryParseUciOutputLine(checkersLine, ref rawFenCache, out var fenWithCheckers);
		return fenWithCheckers;
	}

	public async Task<IReadOnlyCollection<string>> GetLegalMovesViaGoPerft1Async(CancellationToken ct)
	{
		var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		void CaptureMoves(string line) => UciCommandBuilder.CollectMovesFromLine(line, results);

		using var subscription = _lineSource.Subscribe(CaptureMoves);
		await _transport.WriteLineAsync($"{UciConstants.Commands.GO_PERFT} 1", ct).ConfigureAwait(false);
		await IsReadyAsync(ct).ConfigureAwait(false);

		return results.ToList();
	}

	private static bool IsCheckersLine(string line) =>
		line.AsSpan().TrimStart().StartsWith(UciConstants.Prefixes.CHECKERS, StringComparison.OrdinalIgnoreCase);

	private static bool IsFenLine(string line) =>
		line.AsSpan().TrimStart().StartsWith(UciConstants.Prefixes.FEN, StringComparison.OrdinalIgnoreCase);
}
