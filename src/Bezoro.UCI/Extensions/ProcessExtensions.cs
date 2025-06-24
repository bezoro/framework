using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Bezoro.UCI.Extensions
{
	internal static class ProcessExtensions
	{
		/// <summary>
		///     Asynchronously waits for the process to exit.
		/// </summary>
		/// <param name="process">The process to wait for.</param>
		/// <param name="cancellationToken">A token to cancel the wait operation.</param>
		/// <returns>A task that completes when the process exits.</returns>
		public static Task WaitForExitAsync(this Process process, CancellationToken cancellationToken = default)
		{
			if (process.HasExited)
			{
				return Task.CompletedTask;
			}

			var tcs = new TaskCompletionSource<object>();
			process.Exited += (sender, args) => tcs.TrySetResult(null);
			cancellationToken.Register(() => tcs.TrySetCanceled());

			// A final check in case the process exited after the initial check but before the event handler was attached.
			if (process.HasExited)
			{
				return Task.CompletedTask;
			}

			return tcs.Task;
		}
	}
}
