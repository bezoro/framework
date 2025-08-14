using Bezoro.UCI.API;

namespace Bezoro.UCI.Tests;

public class UCITestsBase : IAsyncLifetime
{
	protected const string StockfishPath = "Engine/stockfish/stockfish-windows-x86-64-avx2.exe";

	protected UciConnector? Connector;

	public virtual async Task InitializeAsync()
	{
		Connector = new(StockfishPath);
		await Connector.StartEngineAsync();
		await Connector.SetDefaultPositionAsync();
	}

	public virtual async Task DisposeAsync()
	{
		if (Connector != null) await Connector.DisposeAsync();
	}
}
