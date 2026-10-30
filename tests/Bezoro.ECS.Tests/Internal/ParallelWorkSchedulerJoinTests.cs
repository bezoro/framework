using System;
using System.Threading;
using Bezoro.ECS.Internal;
using Xunit;

namespace Bezoro.ECS.Tests.Internal;

public sealed class ParallelWorkSchedulerJoinTests
{
	[Fact]
	public void FailedSubmission_JoinsAlreadyAcceptedWorkersBeforeThrowing()
	{
		using var entered = new ManualResetEventSlim();
		using var release = new ManualResetEventSlim();
		using var workFinished = new ManualResetEventSlim();
		using var submissionFailed = new ManualResetEventSlim();
		using var ownerExited = new ManualResetEventSlim();
		var expected = new InvalidOperationException("Worker submission failed.");
		Exception? ownerFailure = null;
		Exception? workerFailure = null;
		Thread? queuedWorker = null;
		bool escapedWithActiveWorker = false;
		int submissionCount = 0;
		var owner = new Thread(() =>
		{
			try
			{
				ParallelWorkScheduler.Execute(3, 3, _ =>
				{
					entered.Set();
					if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
					workFinished.Set();
				}, callback =>
				{
					if (++submissionCount == 2)
					{
						submissionFailed.Set();
						throw expected;
					}
					queuedWorker = new Thread(() =>
					{
						try { callback(null); }
						catch (Exception failure) { workerFailure = failure; }
					}) { IsBackground = true };
					queuedWorker.Start();
					if (!entered.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
					return true;
				});
			}
			catch (Exception failure)
			{
				escapedWithActiveWorker = !workFinished.IsSet;
				ownerFailure = failure;
			}
			finally { ownerExited.Set(); }
		}) { IsBackground = true };
		owner.Start();
		try
		{
			Assert.True(submissionFailed.Wait(TimeSpan.FromSeconds(10)));
			Assert.False(ownerExited.Wait(TimeSpan.FromMilliseconds(200)));
		}
		finally
		{
			release.Set();
			Assert.True(owner.Join(TimeSpan.FromSeconds(10)));
			Assert.NotNull(queuedWorker);
			Assert.True(queuedWorker!.Join(TimeSpan.FromSeconds(10)));
		}
		Assert.Same(expected, ownerFailure);
		Assert.Null(workerFailure);
		Assert.False(escapedWithActiveWorker);
	}

	[Fact]
	public void InterruptedWait_JoinsWorkersBeforePropagatingInterruption()
	{
		using var workerEntered = new ManualResetEventSlim();
		using var callerFinished = new ManualResetEventSlim();
		using var releaseWorker = new ManualResetEventSlim();
		using var workerFinished = new ManualResetEventSlim();
		using var ownerExited = new ManualResetEventSlim();
		Exception? ownerFailure = null;
		Exception? workerFailure = null;
		Thread? queuedWorker = null;
		bool escapedWithActiveWorker = false;
		var owner = new Thread(() =>
		{
			try
			{
				ParallelWorkScheduler.Execute(2, 2, index =>
				{
					if (index == 0)
					{
						workerEntered.Set();
						if (!releaseWorker.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
						workerFinished.Set();
					}
					else callerFinished.Set();
				}, callback =>
				{
					queuedWorker = new Thread(() =>
					{
						try { callback(null); }
						catch (Exception failure) { workerFailure = failure; }
					}) { IsBackground = true };
					queuedWorker.Start();
					if (!workerEntered.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
					return true;
				});
			}
			catch (Exception failure)
			{
				escapedWithActiveWorker = !workerFinished.IsSet;
				ownerFailure = failure;
			}
			finally { ownerExited.Set(); }
		}) { IsBackground = true };
		owner.Start();
		try
		{
			Assert.True(callerFinished.Wait(TimeSpan.FromSeconds(10)));
			// If the owner has not entered completion.Wait yet, interruption remains
			// pending and is delivered when that join wait begins.
			owner.Interrupt();
			Assert.False(ownerExited.Wait(TimeSpan.FromMilliseconds(200)));
		}
		finally
		{
			releaseWorker.Set();
			Assert.True(owner.Join(TimeSpan.FromSeconds(10)));
			Assert.NotNull(queuedWorker);
			Assert.True(queuedWorker!.Join(TimeSpan.FromSeconds(10)));
		}
		Assert.IsType<ThreadInterruptedException>(ownerFailure);
		Assert.Null(workerFailure);
		Assert.False(escapedWithActiveWorker);
	}
}
