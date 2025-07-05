using System.Diagnostics;
using Bezoro.UCI.Domain.Extensions;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests;

[TestSubject(typeof(ProcessExtensions))]
public class ProcessExtensionsTest
{
	[Fact]
	public async Task WaitForExitAsync_WhenCancellationTokenIsCancelled_ThrowsOperationCanceledException()
	{
		// Arrange
		// Start a process that will run for a few seconds.
		Process? process = Process.Start(new ProcessStartInfo("cmd.exe", "/c timeout /t 5")
		{
			CreateNoWindow = true
		});

		Assert.NotNull(process);

		// Use a CancellationTokenSource that cancels immediately.
		var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert
		// We expect the awaiter to throw an OperationCanceledException because the token is cancelled.
		await Assert.ThrowsAsync<OperationCanceledException>(() => process.WaitForExitAsync(cts.Token));

		// Cleanup: Ensure the process is killed to not leave it running.
		if (!process.HasExited)
		{
			process.Kill();
		}
	}

	[Fact]
	public async Task WaitForExitAsync_WhenProcessAlreadyExited_ReturnsCompletedTask()
	{
		// Arrange
		// Start a process and immediately wait for it to exit synchronously.
		Process? process = Process.Start(new ProcessStartInfo("cmd.exe", "/c exit")
		{
			CreateNoWindow = true
		});

		Assert.NotNull(process);
		process.WaitForExit(); // The process has now exited.

		// Act
		// Calling WaitForExitAsync on an already-exited process.
		Task task = process.WaitForExitAsync();

		// Assert
		// The returned task should be already completed.
		Assert.True(task.IsCompletedSuccessfully);

		// This await should complete instantly.
		await task;
	}

	[Fact]
	public async Task WaitForExitAsync_WhenProcessExits_CompletesSuccessfully()
	{
		// Arrange
		// Start a process that exits on its own after a short delay.
		// 'timeout /t 1' waits for 1 second, then exits.
		Process? process = Process.Start(new ProcessStartInfo("cmd.exe", "/c timeout /t 1")
		{
			CreateNoWindow = true
		});

		Assert.NotNull(process);

		// Act
		// Wait for the process to exit. We expect this to complete without throwing.
		await process.WaitForExitAsync();

		// Assert
		// The task completed, and the process has exited.
		Assert.True(process.HasExited);
	}
}
