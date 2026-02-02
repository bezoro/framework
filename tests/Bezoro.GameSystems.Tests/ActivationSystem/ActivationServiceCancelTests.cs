using System.Threading.Tasks;
using Bezoro.GameSystems.ActivationSystem.Services;
using Bezoro.GameSystems.ActivationSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.GameSystems.Tests.ActivationSystem;

[TestSubject(typeof(ActivationService))]
public class ActivationServiceCancelTests
{
	[Fact]
	public async Task WhenCancelAlreadyActivated_ShouldReturnFalse()
	{
		using var service = new ActivationService();
		var       handle  = service.Register(() => { });

		service.Start(new(10, 10));
		await Task.Delay(200);

		bool result = service.Cancel(handle);

		result.Should().BeFalse();
	}

	[Fact]
	public async Task WhenCancelled_ShouldNotInvokeCallback()
	{
		using var service   = new ActivationService();
		var       activated = false;

		var handle = service.Register(() => activated = true);
		service.Cancel(handle);

		service.Start(new(10, 10));
		await Task.Delay(200);

		activated.Should().BeFalse();
	}

	[Fact]
	public void WhenCancelAlreadyCancelled_ShouldReturnFalse()
	{
		using var service = new ActivationService();
		var       handle  = service.Register(() => { });

		service.Cancel(handle);
		bool result = service.Cancel(handle);

		result.Should().BeFalse();
	}

	[Fact]
	public void WhenCancelInvalidHandle_ShouldReturnFalse()
	{
		using var service = new ActivationService();

		bool result = service.Cancel(ActivationHandle.None);

		result.Should().BeFalse();
	}

	[Fact]
	public void WhenCancelPending_ShouldDecrementPendingCount()
	{
		using var service = new ActivationService();
		var       handle  = service.Register(() => { });
		service.Register(() => { });

		service.Cancel(handle);

		service.PendingCount.Should().Be(1);
	}

	[Fact]
	public void WhenCancelPending_ShouldReturnTrue()
	{
		using var service = new ActivationService();
		var       handle  = service.Register(() => { });

		bool result = service.Cancel(handle);

		result.Should().BeTrue();
	}
}
