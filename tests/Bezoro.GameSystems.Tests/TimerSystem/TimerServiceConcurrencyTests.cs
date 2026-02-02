using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.GameSystems.TimerSystem.Services;
using Bezoro.GameSystems.TimerSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.GameSystems.Tests.TimerSystem;

[TestSubject(typeof(TimerService))]
public class TimerServiceConcurrencyTests
{
	[Fact]
	public async Task WhenConcurrentCreateAndCancel_ShouldNotThrow()
	{
		using var service = new TimerService();
		service.Start(new(10));

		var tasks = new List<Task>();

		for (var i = 0; i < 10; i++)
		{
			tasks.Add(
				Task.Run(() =>
					{
						for (var j = 0; j < 50; j++)
						{
							var handle = service.Create(TimeSpan.FromMilliseconds(100));
							Thread.Sleep(1);
							service.Cancel(handle);
						}
					}
				)
			);
		}

		var act = async () => await Task.WhenAll(tasks);

		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task WhenConcurrentPauseAndResume_ShouldNotThrow()
	{
		using var service = new TimerService();
		service.Start(new(10));

		var handles = new List<TimerHandle>();
		for (var i = 0; i < 20; i++)
			handles.Add(service.Create(TimeSpan.FromSeconds(10)));

		var tasks = new List<Task>();

		for (var i = 0; i < 10; i++)
		{
			int index = i;
			tasks.Add(
				Task.Run(() =>
					{
						for (var j = 0; j < 50; j++)
						{
							var handle = handles[(index * 2 + j) % handles.Count];
							service.Pause(handle);
							Thread.Sleep(1);
							service.Resume(handle);
						}
					}
				)
			);
		}

		var act = async () => await Task.WhenAll(tasks);

		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task WhenConcurrentQueryDuringTicking_ShouldNotThrow()
	{
		using var service = new TimerService();
		service.Start(new(10));

		var handles = new List<TimerHandle>();
		for (var i = 0; i < 20; i++)
			handles.Add(service.Create(TimeSpan.FromMilliseconds(200)));

		var tasks = new List<Task>();

		for (var i = 0; i < 10; i++)
		{
			int index = i;
			tasks.Add(
				Task.Run(() =>
					{
						for (var j = 0; j < 50; j++)
						{
							var handle = handles[(index * 2 + j) % handles.Count];
							service.TryGetInfo(handle, out _);
							Thread.Sleep(1);
						}
					}
				)
			);
		}

		var act = async () => await Task.WhenAll(tasks);

		await act.Should().NotThrowAsync();
	}
}
