using System.Threading;
using System.Threading.Tasks;
using Bezoro.UCI.Domain.Helpers;

namespace Bezoro.UCI.API.Commands
{
	public readonly record struct UCICommand : IEngineCommand
	{
		private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(5);

		public async Task<object?> ExecuteAsync(UCIEngine engine)
		{
			// Send the uci command to the engine to initiate the handshake.
			await engine.WriteLineAsync("uci").ConfigureAwait(false);

			// A CancellationToken with a timeout is crucial to prevent waiting indefinitely.
			using var timeoutCts = new CancellationTokenSource(HandshakeTimeout);
			try
			{
				// The engine will respond with multiple lines ('id', 'option') before 'uciok'.
				// We must read and process lines until 'uciok' is received.
				while (!timeoutCts.IsCancellationRequested)
				{
					string line       = await engine.ReadNextOutputLineAsync(timeoutCts.Token).ConfigureAwait(false);
					var    outputType = UCIParser.GetOutputType(line);

					switch (outputType)
					{
						case UCIOutputType.UciOk:
							// Handshake successful.
							Logger.LogSuccess("UCI handshake complete. Received 'uciok'.");
							return line; // Return the line to confirm success.

						case UCIOutputType.Id:
						case UCIOutputType.Option:
							// These are expected intermediate messages during the handshake. Log them.
							Logger.LogInfo($"[UCI Handshake]: Received {outputType}: {line}");
							break;

						default:
							// Log any other unexpected output during the handshake.
							Logger.LogWarning($"[UCI Handshake]: Received unexpected line: {line}");
							break;
					}
				}
			}
			catch (OperationCanceledException)
			{
				// This is thrown by ReadNextOutputLineAsync when the timeout is reached.
				throw new TimeoutException(
					$"Engine did not respond with 'uciok' within the {HandshakeTimeout.TotalSeconds}-second timeout.");
			}

			// This part should be unreachable if the logic is correct, but the compiler needs it.
			throw new InvalidOperationException("UCI Handshake failed unexpectedly.");
		}
	}
}
