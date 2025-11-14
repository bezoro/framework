using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Bezoro.UCI.Domain.Common.Helpers;

/// <summary>
/// Helper methods for process management operations.
/// </summary>
internal static class ProcessHelper
{
	/// <summary>
	/// Waits for a process to exit asynchronously.
	/// </summary>
	public static Task WaitForProcessExitAsync(Process process, CancellationToken ct)
	{
		try
		{
			if (process.HasExited) return Task.CompletedTask;

			if (!process.EnableRaisingEvents) process.EnableRaisingEvents = true;
		}
		catch (ObjectDisposedException)
		{
			return Task.CompletedTask;
		}

		var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

		try
		{
			process.Exited += Handler;
			if (process.HasExited)
			{
				process.Exited -= Handler;
				return Task.CompletedTask;
			}
		}
		catch (ObjectDisposedException)
		{
			return Task.CompletedTask;
		}

		CancellationTokenRegistration reg = default;
		if (ct.CanBeCanceled)
			reg = ct.Register(
				static s =>
				{
					var (src, token) = ((TaskCompletionSource<object?>, CancellationToken))s!;
					src.TrySetCanceled(token);
				},
				(tcs, ct));

		return tcs.Task.ContinueWith(
					  t =>
					  {
						  try
						  {
							  process.Exited -= Handler;
						  }
						  catch { }

						  reg.Dispose();
						  return t;
					  },
					  CancellationToken.None,
					  TaskContinuationOptions.ExecuteSynchronously,
					  TaskScheduler.Default)
				  .Unwrap();

		void Handler(object? _, EventArgs __) => tcs.TrySetResult(null);
	}

	/// <summary>
	/// Safely kills a process, optionally killing the entire process tree.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void SafeKillProcess(Process? process, bool killEntireProcessTree)
	{
		if (process is null) return;

		try
		{
			if (!process.HasExited)
			{
#if NET5_0_OR_GREATER
				process.Kill(killEntireProcessTree);
#else
				process.Kill();
#endif
			}
		}
		catch (Exception ex)
		{
			// Log error but don't throw - process may have already exited
			Logger.LogException($"Failed to kill process. ex={ex}", category: LogCategory.UCI);
		}
	}

	/// <summary>
	/// Safely disposes a process.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void SafeDisposeProcess(Process? process)
	{
		if (process is null) return;

		try
		{
			process.Dispose();
		}
		catch { }
	}

	/// <summary>
	/// Creates a ProcessStartInfo for a UCI engine process.
	/// </summary>
	public static ProcessStartInfo CreateProcessStartInfo(
		string                      path,
		IReadOnlyList<string>?      args,
		string                      workingDirectory,
		bool                        redirectStandardError,
		Encoding?                   stdoutEncoding,
		Encoding?                   stderrEncoding)
	{
		var startInfo = new ProcessStartInfo
		{
			FileName               = path,
			UseShellExecute        = false,
			RedirectStandardInput  = true,
			RedirectStandardOutput = true,
			RedirectStandardError  = redirectStandardError,
			StandardOutputEncoding = stdoutEncoding,
			StandardErrorEncoding  = stderrEncoding,
			CreateNoWindow         = true,
			WorkingDirectory       = workingDirectory
		};

		if (args is { Count: > 0 })
			foreach (string? a in args)
			{
				if (a is { })
					startInfo.ArgumentList.Add(a);
			}

		return startInfo;
	}
}

