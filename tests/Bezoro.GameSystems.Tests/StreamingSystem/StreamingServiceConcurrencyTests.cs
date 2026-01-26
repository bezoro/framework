using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.GameSystems.StreamingSystem.Services;
using Bezoro.GameSystems.StreamingSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.GameSystems.Tests.StreamingSystem;

[TestSubject(typeof(StreamingService))]
public class StreamingServiceConcurrencyTests
{
	[Fact]
	public async Task WhenConcurrentRegistrationAndUnregistration_ShouldNotThrow()
	{
		using var system = new StreamingService();
		var       config = new StreamingConfig(() => Vector3.Zero);

		system.Start(config);

		var tasks = new List<Task>();

		for (var i = 0; i < 10; i++)
		{
			int index = i;
			tasks.Add(
				Task.Run(() =>
				{
					for (var j = 0; j < 50; j++)
					{
						var entity = new TestEntity(index * 1000 + j, new(j, 0, 0));
						system.Register(entity);
						Thread.Sleep(1);
						system.Unregister(entity);
					}
				}));
		}

		var act = async () => await Task.WhenAll(tasks);

		await act.Should().NotThrowAsync();
	}
}
