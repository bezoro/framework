using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Bezoro.UCI.Domain.Common.Helpers;

/// <summary>
/// Helper for write operations with timeout and cancellation support.
/// </summary>
internal static class WriteOperationHelper
{
	/// <summary>
	/// Writes a line with caller cancellation support.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static async Task WriteWithCallerCancellationAsync(
		ChannelWriter<string> writer,
		string                line,
		CancellationToken     ct)
	{
		if (!ct.CanBeCanceled) await writer.WriteAsync(line, CancellationToken.None).ConfigureAwait(false);
		else await writer.WriteAsync(line, ct).ConfigureAwait(false);
	}

	/// <summary>
	/// Spins until write succeeds or cancellation is requested.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool SpinUntilWriteOrCancel(
		ChannelWriter<string>   writer,
		string                  line,
		CancellationToken       ct,
		int                     spinIterations)
	{
		var spinner = new SpinWait();
		for (var i = 0; i < spinIterations; i++)
		{
			if (writer.TryWrite(line)) return true;

			if (ct.IsCancellationRequested) throw new OperationCanceledException(ct);

			spinner.SpinOnce();
		}

		return false;
	}

	/// <summary>
	/// Determines if spinning should be used for small timeouts.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool ShouldSpinForSmallTimeout(TimeSpan timeout) =>
		timeout > TimeSpan.Zero && timeout <= TimeSpan.FromMilliseconds(1);
}

