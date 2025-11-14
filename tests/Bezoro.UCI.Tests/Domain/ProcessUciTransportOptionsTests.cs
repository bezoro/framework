using Bezoro.UCI.Domain;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests.Domain;

[TestSubject(typeof(ProcessUciTransportOptions))]
public static class ProcessUciTransportOptionsTests
{
	public class UnitTests
	{
		[Fact]
		public void ChannelCapacity_LessOrEqualZero_Throws()
		{
			var    options = new ProcessUciTransportOptions { ChannelCapacity = 0 };
			Action act     = () => _ = new ProcessUciTransport("any/nonempty/path", null, null, options);

			act.Should().Throw<ArgumentOutOfRangeException>();
		}

		[Fact]
		public void NewLine_Empty_Throws()
		{
			var    options = new ProcessUciTransportOptions { NewLine = "" };
			Action act     = () => _ = new ProcessUciTransport("any/nonempty/path", null, null, options);

			act.Should().Throw<ArgumentException>();
		}

		[Fact]
		public void QuitGracePeriod_Negative_Throws()
		{
			var    options = new ProcessUciTransportOptions { QuitGracePeriod = TimeSpan.FromMilliseconds(-1) };
			Action act     = () => _ = new ProcessUciTransport("any/nonempty/path", null, null, options);

			act.Should().Throw<ArgumentOutOfRangeException>();
		}

		[Fact]
		public void QuitGracePeriodDefault_AndQuitGracePeriodMsNegative_Throws()
		{
			var    options = new ProcessUciTransportOptions { QuitGracePeriod = TimeSpan.Zero, QuitGracePeriodMs = -1 };
			Action act     = () => _ = new ProcessUciTransport("any/nonempty/path", null, null, options);

			act.Should().Throw<ArgumentOutOfRangeException>();
		}
	}
}
