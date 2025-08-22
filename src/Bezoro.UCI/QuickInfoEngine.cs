using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.UCI.API.Types;

namespace Bezoro.UCI;

internal sealed class QuickInfoEngine : IAsyncDisposable, IDisposable
{
	private readonly UciEngineClient _client;

	public QuickInfoEngine(string enginePath, IEnumerable<string>? args = null, string? workingDirectory = null)
	{
		var transport = new ProcessUciTransport(enginePath, args, workingDirectory);
		_client = new(transport);
	}

	public bool IsHealthy => _client.IsHealthy;
	public bool IsStarted => _client.IsStarted;

	public EngineActivity Activity => _client.Activity;

	public ProcessUciTransport.TransportStatus Status => _client.Status;

	public Task StartAsync(CancellationToken ct = default) => _client.StartAsync(ct);
	public Task StopAsync(CancellationToken  ct = default) => _client.StopAsync(ct);

	public Task<Fen?> GetCurrentFenAsync(CancellationToken ct = default) =>
		_client.GetFenViaDAsync(ct);


	public Task SetPositionAsync(Fen fen, IEnumerable<string>? moves = null, CancellationToken ct = default) =>
		_client.SetPositionAsync(fen, moves, ct);

	public Task<IReadOnlyList<string>> GetLegalMovesAsync(CancellationToken ct = default) =>
		_client.GetLegalMovesViaGoPerft1Async(ct);

	public async Task<SearchResult> QuickEvalAsync(Fen fen, uint depth = 6, CancellationToken ct = default)
	{
		await _client.SetPositionAsync(fen, null, ct).ConfigureAwait(false);
		return await _client.GoAsync(new() { Depth = depth }, ct).ConfigureAwait(false);
	}

	public ValueTask DisposeAsync() => _client.DisposeAsync();

	public void Dispose()
	{
		DisposeAsync().AsTask().GetAwaiter().GetResult();
	}
}
