using Bezoro.Chess.UCI.Protocol.Domain.Common.Helpers;
using FluentAssertions;

namespace Bezoro.Chess.UCI.Protocol.Tests.Domain.Common.Helpers;

public class ProcessHelperExitWaitTests
{
	[Fact]
	public async Task WaitForProcessExitAsync_WithConcurrentWaiters_ShouldCompleteEveryRegistration()
	{
		const int waiterCount = 16;
		for (var iteration = 0; iteration < 4; iteration++)
		{
			using var process = ProcessUciTransportTestHelpers.StartChildProcess("wait-input");
			using var cancellation = new CancellationTokenSource();
			using var start = new Barrier(waiterCount + 1);
			var pending = new Task[waiterCount];
			var registered = new TaskCompletionSource[waiterCount];
			var threads = new Thread[waiterCount];
			for (var index = 0; index < waiterCount; index++)
			{
				int slot = index;
				registered[slot] = new(TaskCreationOptions.RunContinuationsAsynchronously);
				threads[slot] = new Thread(() =>
				{
					try
					{
						if (!start.SignalAndWait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Waiter start barrier timed out.");
						pending[slot] = ProcessHelper.WaitForProcessExitAsync(process, cancellation.Token);
						registered[slot].TrySetResult();
					}
					catch (Exception error) { registered[slot].TrySetException(error); }
				}) { IsBackground = true };
				threads[slot].Start();
			}

			try
			{
				start.SignalAndWait(TimeSpan.FromSeconds(5)).Should().BeTrue();
				await Task.WhenAll(registered.Select(value => value.Task)).WaitAsync(TimeSpan.FromSeconds(5));
				pending.Should().OnlyContain(task => !task.IsCompleted);
				// The child stays alive until every concurrent subscription has been installed.
				await process.StandardInput.WriteLineAsync("exit");
				await process.StandardInput.FlushAsync();
				await Task.WhenAll(pending).WaitAsync(TimeSpan.FromSeconds(5));
				pending.Should().OnlyContain(task => task.IsCompletedSuccessfully);
			}
			finally
			{
				cancellation.Cancel();
				try { await Task.WhenAll(pending.Where(task => task is not null)); } catch (OperationCanceledException) { }
				foreach (var thread in threads) thread.Join(TimeSpan.FromSeconds(5));
				if (!process.HasExited) process.Kill(entireProcessTree: true);
				await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
			}
		}
	}
}
