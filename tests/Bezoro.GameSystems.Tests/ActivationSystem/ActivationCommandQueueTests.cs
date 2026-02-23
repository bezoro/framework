using System;
using System.Linq;
using System.Threading.Tasks;
using Bezoro.GameSystems.ActivationSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.GameSystems.Tests.ActivationSystem;

[TestSubject(typeof(ActivationCommandQueue))]
public class ActivationCommandQueueTests
{
	[Fact]
	public async Task Register_WhenCalledConcurrently_ShouldReturnUniqueHandles()
	{
		var queue = new ActivationCommandQueue();
		var ids   = new int[1000];

		var tasks = Enumerable.Range(0, ids.Length)
							  .Select(i => Task.Run(() => ids[i] = queue.Register(() => { }).Id))
							  .ToArray();

		await Task.WhenAll(tasks);

		ids.Distinct().Should().HaveCount(ids.Length);
	}

	[Fact]
	public void Cancel_WhenHandleIsInvalid_ShouldReturnFalse()
	{
		var queue = new ActivationCommandQueue();

		bool result = queue.Cancel(ActivationHandle.None);

		result.Should().BeFalse();
	}

	[Fact]
	public void Register_WhenCallbackIsNull_ShouldThrow()
	{
		var queue = new ActivationCommandQueue();

		var act = () => queue.Register(null!);

		act.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void Register_WhenCalled_ShouldReturnValidHandle()
	{
		var queue = new ActivationCommandQueue();

		var handle = queue.Register(() => { });

		handle.IsValid.Should().BeTrue();
	}
}
