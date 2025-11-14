using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Bezoro.UCI.Domain.Common.Helpers;

/// <summary>
/// Factory for creating channels used in UCI transport.
/// </summary>
internal static class ChannelFactory
{
	/// <summary>
	/// Creates a bounded channel for reading lines from the process.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Channel<string> CreateLinesChannel(int capacity, bool singleReader)
	{
		return Channel.CreateBounded<string>(
			new BoundedChannelOptions(capacity)
			{
				SingleWriter = true,
				SingleReader = singleReader,
				FullMode     = BoundedChannelFullMode.Wait
			});
	}

	/// <summary>
	/// Creates a bounded channel for outgoing messages to the process.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Channel<string> CreateOutgoingChannel(int capacity, bool singleWriter)
	{
		return Channel.CreateBounded<string>(
			new BoundedChannelOptions(capacity)
			{
				SingleWriter = singleWriter,
				SingleReader = true,
				FullMode     = BoundedChannelFullMode.Wait
			});
	}

	/// <summary>
	/// Safely completes a channel writer.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void TryComplete(ChannelWriter<string>? writer, Exception? error = null)
	{
		if (writer is null) return;

		try
		{
			if (error is null) writer.TryComplete();
			else writer.TryComplete(error);
		}
		catch { }
	}
}

