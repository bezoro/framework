using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.UCI.API.Types;

namespace Bezoro.UCI;

internal sealed class PonderEngine : IAsyncDisposable, IDisposable
{
	private readonly UciEngineClient _client;

	public event Action<string, string>? BestMove;

	public event Action<PrincipalVariation>? InfoPv;

	public PonderEngine(string enginePath, IEnumerable<string>? args = null, string? workingDirectory = null)
	{
		var transport = new ProcessUciTransport(enginePath, args, workingDirectory);
		_client                  =  new(transport);
		_client.InfoPvReceived   += pv => InfoPv?.Invoke(pv);
		_client.BestMoveReceived += (b, p) => BestMove?.Invoke(b, p);
	}

	public bool IsHealthy => _client.IsHealthy;
	public bool IsStarted => _client.IsStarted;

	public EngineActivity Activity => _client.Activity;

	public ProcessUciTransport.TransportStatus Status => _client.Status;

	public Task StartAsync(CancellationToken ct = default) => _client.StartAsync(ct);

	public async Task StartPonderAsync(Fen fen, IEnumerable<string>? playedMoves, CancellationToken ct = default)
	{
		await _client.SetPositionAsync(fen, playedMoves, ct).ConfigureAwait(false);
		await _client.GoFireAndForgetAsync(new() { Ponder = true, Infinite = true }, ct).ConfigureAwait(false);
	}

	public Task StopAsync(CancellationToken ct = default) => _client.StopAsync(ct);

	public Task      StopPonderAsync(CancellationToken ct = default) => _client.StopSearchAsync(ct);
	public ValueTask DisposeAsync()                                  => _client.DisposeAsync();

	public void Dispose()
	{
		DisposeAsync().AsTask().GetAwaiter().GetResult();
	}
}
