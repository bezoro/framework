using System;
using Bezoro.Core.Extensions;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Common.Extensions;

[TestSubject(typeof(ActionExtensions))]
public static class ActionExtensionsTests
{
	public class Unit
	{
		[Fact]
		public void SafeInvoke_WhenActionIsNull_ShouldThrow()
		{
			Action? action = null;

			var act = () => action!.SafeInvoke();

			act.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void SafeInvoke_WhenActionSucceeds_ShouldExecuteAction()
		{
			var    executed = false;
			Action action   = () => executed = true;

			action.SafeInvoke();

			executed.Should().BeTrue();
		}
	}
}


