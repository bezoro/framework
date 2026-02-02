using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Bezoro.GameSystems.Tests.TimerSystem;

internal static class TimerTestHelpers
{
	public static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 1000, int pollDelayMs = 10)
	{
		var stopwatch = Stopwatch.StartNew();

		while (stopwatch.ElapsedMilliseconds < timeoutMs)
		{
			if (condition())
				return;

			await Task.Delay(pollDelayMs).ConfigureAwait(false);
		}

		throw new TimeoutException($"Condition was not met within {timeoutMs}ms.");
	}
}
