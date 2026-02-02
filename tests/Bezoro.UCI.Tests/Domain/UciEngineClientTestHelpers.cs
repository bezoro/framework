using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Bezoro.UCI.Domain;
using Bezoro.UCI.Tests.TestHelpers;
using NSubstitute;

namespace Bezoro.UCI.Tests.Domain;

internal static class UciEngineClientTestHelpers
{
	public static (IUciTransport transport, Channel<string> channel) CreateMockTransport()
	{
		var transport = Substitute.For<IUciTransport>();
		transport.StartAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
		transport.StopAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
		transport.WriteLineAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

		var channel = Channel.CreateUnbounded<string>();
		transport.ReadLinesAsync(Arg.Any<CancellationToken>())
				 .Returns(ci => StreamFromChannel(channel.Reader, (CancellationToken)ci[0]));

		return (transport, channel);
	}

	public static (IUciTransport transport, UciEngineClient client) CreateClientWithTransport()
	{
		var transport = Substitute.For<IUciTransport>();
		transport.WriteLineAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
		var client = new UciEngineClient(transport);
		return (transport, client);
	}

	public static Action SetupDelayedReadyResponse(IUciTransport transport, Channel<string> channel)
	{
		var readyGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		transport.ClearReceivedCalls();
		transport.When(x => x.WriteLineAsync("isready", Arg.Any<CancellationToken>()))
				 .Do(async _ =>
					 {
						 await readyGate.Task;
						 await channel.Writer.WriteAsync("readyok");
					 }
				 );

		return () => readyGate.TrySetResult();
	}

	public static async IAsyncEnumerable<string> StreamFromChannel(
		ChannelReader<string>                      reader,
		[EnumeratorCancellation] CancellationToken ct = default)
	{
		while (!ct.IsCancellationRequested)
		{
			while (reader.TryRead(out string? item))
				yield return item;

			bool canRead = await reader.WaitToReadAsync(ct).ConfigureAwait(false);
			if (!canRead) yield break;
		}
	}

	public static async Task<UciEngineClient> StartClientWithConcurrentHandshakeAsync(
		IUciTransport   transport,
		Channel<string> channel)
	{
		var client = new UciEngineClient(transport);

		using var pumpCts = new CancellationTokenSource();
		var pump = Task.Run(
			async () =>
			{
				for (var i = 0;
					 i < TestConstants.HANDSHAKE_PUMP_ITERATIONS && !pumpCts.IsCancellationRequested;
					 i++)
				{
					await channel.Writer.WriteAsync("uciok");
					await Task.Delay(TestConstants.ShortDelay);
					await channel.Writer.WriteAsync("readyok");
					await Task.Delay(TestConstants.ShortDelay);
				}
			},
			pumpCts.Token
		);

		await client.StartAsync(CancellationToken.None);
		pumpCts.Cancel();
		await pump;

		return client;
	}

	public static async Task<UciEngineClient> StartClientWithHandshakeAsync(
		IUciTransport   transport,
		Channel<string> channel,
		int             pumpIterations = TestConstants.EXTENDED_HANDSHAKE_PUMP_ITERATIONS)
	{
		var client = new UciEngineClient(transport);

		using var pumpCts = new CancellationTokenSource();
		var pump = Task.Run(
			async () =>
			{
				for (var i = 0; i < pumpIterations && !pumpCts.IsCancellationRequested; i++)
				{
					await channel.Writer.WriteAsync("uciok");
					await Task.Delay(TestConstants.VeryShortDelay);
					await channel.Writer.WriteAsync("readyok");
					await Task.Delay(TestConstants.VeryShortDelay);
				}
			},
			pumpCts.Token
		);

		await client.StartAsync(CancellationToken.None);
		pumpCts.Cancel();
		await pump;

		return client;
	}
}
