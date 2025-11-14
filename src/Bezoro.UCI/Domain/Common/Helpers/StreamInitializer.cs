using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Bezoro.UCI.Domain.Common.Helpers;

/// <summary>
/// Helper for initializing process streams.
/// </summary>
internal static class StreamInitializer
{
	/// <summary>
	/// Initializes streams for a process with the given encodings and options.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ProcessStreams InitializeStreams(
		Process   process,
		Encoding? stdinEncoding,
		Encoding? stdoutEncoding,
		Encoding? stderrEncoding,
		bool      redirectStandardError,
		string    newLine,
		bool      autoFlush)
	{
		var stdinEnc  = stdinEncoding ?? process.StandardInput.Encoding;
		var stdoutEnc = stdoutEncoding ?? process.StandardOutput.CurrentEncoding;
		var stderrEnc = stderrEncoding ??
						(redirectStandardError ? process.StandardError.CurrentEncoding : Encoding.UTF8);

		var stdin = new StreamWriter(process.StandardInput.BaseStream, stdinEnc, 64 * 1024, true)
		{
			NewLine   = newLine,
			AutoFlush = autoFlush
		};

		var stdout = new StreamReader(process.StandardOutput.BaseStream, stdoutEnc, false, 64 * 1024, true);

		var stderr = redirectStandardError
						? new StreamReader(process.StandardError.BaseStream, stderrEnc, false, 32 * 1024, true)
						: null;

		return new ProcessStreams
		{
			Stdin  = stdin,
			Stdout = stdout,
			Stderr = stderr
		};
	}

	/// <summary>
	/// Safely disposes a stream writer.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static async Task SafeDisposeAsync(IAsyncDisposable? disposable)
	{
		if (disposable is null) return;

		try
		{
			await disposable.DisposeAsync().ConfigureAwait(false);
		}
		catch { }
	}

	/// <summary>
	/// Safely disposes a stream reader or writer.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void SafeDispose(IDisposable? disposable)
	{
		if (disposable is null) return;

		try
		{
			disposable.Dispose();
		}
		catch { }
	}
}

