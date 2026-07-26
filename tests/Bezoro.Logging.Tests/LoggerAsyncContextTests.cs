using System.Collections.Concurrent;
using Bezoro.Logging.Types;
using FluentAssertions;

namespace Bezoro.Logging.Tests;

public sealed class LoggerAsyncContextTests
{
	[Fact]
	public async Task BeginAsyncContext_WhenAsyncBranchesOverlap_ShouldIsolateSiblingContexts()
	{
		using var settings = new LoggerSettingsScope();
		var byMessage = new ConcurrentDictionary<string, LogPayload>();
		var leftEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var rightEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var timeout = TimeSpan.FromSeconds(10);
		Action<LogPayload> handler = payload => byMessage[payload.Message] = payload;
		Logger.OnLog += handler;

		try
		{
			using (LoggerSettings.BeginAsyncContext("root"))
			{
				var left = LogBranchAsync("left", leftEntered);
				var right = LogBranchAsync("right", rightEntered);

				try
				{
					await Task.WhenAll(leftEntered.Task, rightEntered.Task).WaitAsync(timeout);
					release.SetResult();
				}
				finally
				{
					release.TrySetResult();
					await Task.WhenAll(left, right).WaitAsync(timeout);
				}
			}
		}
		finally
		{
			Logger.OnLog -= handler;
		}

		byMessage["left"].AsyncContextHierarchy.Should().Equal("root", "left");
		byMessage["right"].AsyncContextHierarchy.Should().Equal("root", "right");

		async Task LogBranchAsync(string name, TaskCompletionSource entered)
		{
			using (LoggerSettings.BeginAsyncContext(name))
			{
				entered.SetResult();
				await release.Task;
				Logger.Log(name);
			}
		}
	}

	[Fact]
	public void BeginAsyncContext_WhenChildIsDisposed_ShouldRestoreRootContext()
	{
		using var settings = new LoggerSettingsScope();
		var payloads = new List<LogPayload>();
		Action<LogPayload> handler = payloads.Add;
		Logger.OnLog += handler;

		try
		{
			using (LoggerSettings.BeginAsyncContext("root"))
			{
				Logger.Log("at root");

				using (LoggerSettings.BeginAsyncContext("child"))
				{
					Logger.Log("at child");
				}

				Logger.Log("after child");
			}
		}
		finally
		{
			Logger.OnLog -= handler;
		}

		payloads.Select(payload => payload.AsyncContextHierarchy)
			.Should().SatisfyRespectively(
				hierarchy => hierarchy.Should().Equal("root"),
				hierarchy => hierarchy.Should().Equal("root", "child"),
				hierarchy => hierarchy.Should().Equal("root"));
		payloads[1].AsyncContext.Should().Be("root > child");
		payloads[1].AsyncContextDepth.Should().Be(2);
	}

	[Fact]
	public void Dispose_WhenAsyncContextScopeIsDisposedTwice_ShouldKeepParentContext()
	{
		using var settings = new LoggerSettingsScope();
		using var root = LoggerSettings.BeginAsyncContext("root");
		var payloads = new List<LogPayload>();
		Action<LogPayload> handler = payloads.Add;
		Logger.OnLog += handler;

		try
		{
			var child = LoggerSettings.BeginAsyncContext("child");
			child.Dispose();
			child.Dispose();

			Logger.Log("after double dispose");
		}
		finally
		{
			Logger.OnLog -= handler;
		}

		payloads.Should().ContainSingle();
		payloads[0].AsyncContextHierarchy.Should().Equal("root");
	}
}
