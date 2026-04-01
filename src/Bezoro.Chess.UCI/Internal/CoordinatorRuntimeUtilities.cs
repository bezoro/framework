using System.Collections.Generic;
using System.Threading;
using Bezoro.Chess.UCI.API.Types;

namespace Bezoro.Chess.UCI.Internal;

internal static class CoordinatorRuntimeUtilities
{
	public static void CancelAndDispose(
		ref CancellationTokenSource? ctsField,
		CancellationTokenSource?     replacement = null)
	{
		var old = Interlocked.Exchange(ref ctsField, replacement);
		try
		{
			old?.Cancel();
		}
		catch
		{
			// Best-effort: ignore errors when canceling
		}
		finally
		{
			old?.Dispose();
		}
	}

	public static bool CancelAndDisposeIfCurrent(
		ref CancellationTokenSource? ctsField,
		CancellationTokenSource      candidate)
	{
		var current = Interlocked.CompareExchange(ref ctsField, null, candidate);
		if (!ReferenceEquals(current, candidate))
			return false;

		try
		{
			candidate.Cancel();
		}
		catch
		{
			// Best-effort: ignore errors when canceling
		}
		finally
		{
			candidate.Dispose();
		}

		return true;
	}

	public static void EnsureCoordinatorCapabilities(UciEngineCapabilities capabilities)
	{
		if (capabilities.SupportsCoordinatorExtensions)
			return;

		throw new NotSupportedException(
			$"UciGameEngineSession requires engine support for display-board FEN retrieval and perft move listing. " +
			$"Detected capabilities: DisplayBoardFen={capabilities.DisplayBoardFen}, " +
			$"PerftMoveListing={capabilities.PerftMoveListing}."
		);
	}

	public static void ThrowStopFailures(
		IReadOnlyList<Exception> failures,
		bool                     cancellationRequested,
		CancellationToken        ct)
	{
		if (failures.Count == 0)
		{
			if (cancellationRequested)
				throw new OperationCanceledException(ct);

			return;
		}

		if (failures.Count == 1)
			throw failures[0];

		throw new AggregateException(failures);
	}
}
