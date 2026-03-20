using Bezoro.Chess.UCI.Protocol.API;
using Bezoro.Chess.UCI.Protocol.API.Types;

if (args.Length == 0)
{
	Console.Error.WriteLine("Usage: Bezoro.Chess.UCI.Protocol.ConsoleDemo <engine-path> [uci-moves...]");
	return 1;
}

var enginePath = args[0];
var moves = args.Skip(1).ToArray();

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

var options = new UciClientOptions
{
	UciHandshakeTimeout = TimeSpan.FromSeconds(5),
	ReadyTimeout = TimeSpan.FromSeconds(10),
	DefaultSearchTimeout = TimeSpan.FromSeconds(20)
};

await using var client = new UciEngineClient(enginePath, options: options);

client.InfoReceived += static message =>
{
	var payload = message.Payload;
	if (payload.Depth is not uint depth || payload.PrincipalVariation is not { } pv)
		return;

	Console.WriteLine($"info depth {depth}: {pv.RawPv}");
};

client.BestMoveMessageReceived += static message =>
	Console.WriteLine($"bestmove {message.BestMove} ponder {message.PonderMove}");

client.StderrReceived += static line =>
	Console.Error.WriteLine($"stderr: {line}");

await client.StartAsync(cts.Token);

Console.WriteLine($"{client.EngineInfo.Name} by {client.EngineInfo.Author}");
Console.WriteLine($"Capabilities: {client.Capabilities}");

if (client.TryGetOption("Threads", out var threads))
	Console.WriteLine($"Threads option default: {threads.DefaultValue}");

foreach (var option in client.AvailableOptions.Take(5))
	Console.WriteLine($"option {option.Name} [{option.Type}] default={option.DefaultValue}");

await client.SetPositionAsync(
	Fen.Default,
	moves.Length == 0 ? ["e2e4", "e7e5"] : moves,
	cts.Token);

var result = await client.GoAsync(new SearchParameters { Depth = 12 }, cts.Token);

Console.WriteLine();
Console.WriteLine($"Search finished: {result.BestMove} ponder {result.PonderMove}");
Console.WriteLine($"Reached depth:   {result.ReachedDepth}");
Console.WriteLine($"Best eval cp:    {result.BestCpScore}");

if (client.Capabilities.SupportsCoordinatorExtensions)
{
	var currentFen = await client.TryGetFenViaDisplayBoardAsync(cts.Token);
	var legalMoves = await client.GetLegalMovesViaPerftAsync(cts.Token);

	Console.WriteLine();
	Console.WriteLine($"Display-board FEN: {currentFen}");
	Console.WriteLine($"Legal moves:       {string.Join(", ", legalMoves.Take(12))}");
}
else
{
	Console.WriteLine();
	Console.WriteLine("Extension helpers are unavailable on this engine.");
}

return 0;
