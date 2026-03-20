using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.Chess.UCI.Protocol.API.Types;
using Bezoro.Chess.UCI.Protocol.Domain.Common.Constants;

namespace Bezoro.Chess.UCI.Protocol.Domain.EngineClient;

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
	private readonly SemaphoreSlim _protocolGate = new(1, 1);
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
		await ExecuteSerializedCommandAsync(IsReadyCoreAsync, ct).ConfigureAwait(false);
	}

	public async Task SetOptionAsync(string name, string? value, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(name)) return;

		await ExecuteSerializedCommandAsync(
			async token =>
			{
				await _transport.WriteLineAsync(UciCommandBuilder.BuildSetOptionCommand(name, value), token)
								.ConfigureAwait(false);
				await IsReadyCoreAsync(token).ConfigureAwait(false);
			},
			ct
		).ConfigureAwait(false);
	}

	public async Task SetPositionAsync(Fen fen, IEnumerable<string>? moves, CancellationToken ct)
	{
		if (!Fen.Validate(fen.Raw))
			throw new ArgumentException("Invalid FEN provided.", nameof(fen));

		await _transport.WriteLineAsync(UciCommandBuilder.BuildPositionCommand(fen, moves), ct)
						.ConfigureAwait(false);
	}

	public async Task PonderHitAsync(CancellationToken ct)
	{
		await _transport.WriteLineAsync(UciConstants.Commands.PONDER_HIT, ct).ConfigureAwait(false);
		_setActivity(EngineActivity.Searching);
	}

	public async Task RegisterAsync(UciRegistration registration, CancellationToken ct)
	{
		await ExecuteSerializedCommandAsync(
			token => _transport.WriteLineAsync(UciCommandBuilder.BuildRegisterCommand(registration), token),
			ct
		).ConfigureAwait(false);
	}

	public async Task SetDebugAsync(bool enabled, CancellationToken ct)
	{
		await ExecuteSerializedCommandAsync(
			token => _transport.WriteLineAsync(UciCommandBuilder.BuildDebugCommand(enabled), token),
			ct
		).ConfigureAwait(false);
	}

	public async Task StopSearchAsync(CancellationToken ct)
	{
		await _transport.WriteLineAsync(UciConstants.Commands.STOP, ct).ConfigureAwait(false);
		_setActivity(EngineActivity.Idle);
	}

	public async Task UciInitAsync(CancellationToken ct)
	{
		await ExecuteSerializedCommandAsync(UciInitCoreAsync, ct).ConfigureAwait(false);
	}

	public async Task UciNewGameAsync(CancellationToken ct)
	{
		await ExecuteSerializedCommandAsync(UciNewGameCoreAsync, ct).ConfigureAwait(false);
	}

	// TODO: [CODE SMELL - Engine-Specific Protocol Coupling] GetFenViaDAsync and GetLegalMovesViaGoPerft1Async rely on Stockfish-style 'd' output and 'go perft 1'. Fix: add capability detection and generic UCI fallbacks before claiming broad UCI-engine compatibility.
	public async Task<Fen?> GetFenViaDAsync(CancellationToken ct)
	{
		return await ExecuteSerializedCommandAsync(GetFenViaDCoreAsync, ct).ConfigureAwait(false);
	}

	public async Task<IReadOnlyCollection<string>> GetLegalMovesViaGoPerft1Async(CancellationToken ct)
	{
		return await ExecuteSerializedCommandAsync(GetLegalMovesViaGoPerft1CoreAsync, ct).ConfigureAwait(false);
	}

	private async Task ExecuteSerializedCommandAsync(Func<CancellationToken, Task> action, CancellationToken ct)
	{
		await _protocolGate.WaitAsync(ct).ConfigureAwait(false);
		try
		{
			await action(ct).ConfigureAwait(false);
		}
		finally
		{
			_protocolGate.Release();
		}
	}

	private async Task<T> ExecuteSerializedCommandAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
	{
		await _protocolGate.WaitAsync(ct).ConfigureAwait(false);
		try
		{
			return await action(ct).ConfigureAwait(false);
		}
		finally
		{
			_protocolGate.Release();
		}
	}

	private async Task IsReadyCoreAsync(CancellationToken ct)
	{
		Task<string> readyTask = _lineWaiters.WaitForAsync(
			static l => l.Trim().Equals(
				UciConstants.Responses.READY_OK,
				StringComparison.OrdinalIgnoreCase
			),
			TimeSpan.FromSeconds(10),
			ct
		);

		await _transport.WriteLineAsync(UciConstants.Commands.IS_READY, ct).ConfigureAwait(false);
		await readyTask.ConfigureAwait(false);
	}

	private async Task UciInitCoreAsync(CancellationToken ct)
	{
		Task<string> uciOkTask = _lineWaiters.WaitForAsync(
			static l => l.Trim().Equals(
				UciConstants.Responses.UCI_OK,
				StringComparison.OrdinalIgnoreCase
			),
			TimeSpan.FromSeconds(5),
			ct
		);

		await _transport.WriteLineAsync(UciConstants.Commands.UCI, ct).ConfigureAwait(false);
		await uciOkTask.ConfigureAwait(false);

		await IsReadyCoreAsync(ct).ConfigureAwait(false);
	}

	private async Task UciNewGameCoreAsync(CancellationToken ct)
	{
		await _transport.WriteLineAsync(UciConstants.Commands.UCI_NEW_GAME, ct).ConfigureAwait(false);
		await IsReadyCoreAsync(ct).ConfigureAwait(false);
	}

	private async Task<Fen?> GetFenViaDCoreAsync(CancellationToken ct)
	{
		var fenTask = _lineWaiters.WaitForAsync(static line => IsFenLine(line), TimeSpan.FromSeconds(2), ct);

		using var checkersCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
		var checkersTask = _lineWaiters.WaitForAsync(
			static line => IsCheckersLine(line),
			Timeout.InfiniteTimeSpan,
			checkersCts.Token
		);

		await _transport.WriteLineAsync(UciConstants.Commands.DISPLAY_BOARD, ct).ConfigureAwait(false);

		string fenLine;
		try
		{
			fenLine = await fenTask.ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (!ct.IsCancellationRequested)
		{
#if NETSTANDARD || NETSTANDARD2_0 || NETSTANDARD2_1
			checkersCts.Cancel();
#else
			await checkersCts.CancelAsync();
#endif
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

	private async Task<IReadOnlyCollection<string>> GetLegalMovesViaGoPerft1CoreAsync(CancellationToken ct)
	{
		var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		void CaptureMoves(string line) => UciCommandBuilder.CollectMovesFromLine(line, results);

		using var subscription = _lineSource.Subscribe(CaptureMoves);
		await _transport.WriteLineAsync($"{UciConstants.Commands.GO_PERFT} 1", ct).ConfigureAwait(false);
		await IsReadyCoreAsync(ct).ConfigureAwait(false);

		return results.ToList();
	}

	private static bool IsCheckersLine(string line) =>
		line.AsSpan().TrimStart().StartsWith(UciConstants.Prefixes.CHECKERS, StringComparison.OrdinalIgnoreCase);

	private static bool IsFenLine(string line) =>
		line.AsSpan().TrimStart().StartsWith(UciConstants.Prefixes.FEN, StringComparison.OrdinalIgnoreCase);
}
