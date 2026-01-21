using Bezoro.UCI.Domain;
using Bezoro.UCI.Tests.TestHelpers;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit.Abstractions;

namespace Bezoro.UCI.Tests.Domain;

[TestSubject(typeof(ProcessUciTransportOptions))]
public class ProcessUciTransportOptionsTests(ITestOutputHelper output) : UnitTestBase(output)
{
	public static TheoryData<ChannelCapacityTestCase> InvalidChannelCapacities =>
	[
		new(0, "Zero capacity"),
		new(-1, "Negative capacity"),
		new(-100, "Large negative capacity")
	];

	public static TheoryData<TimeoutTestCase> InvalidQuitGracePeriods =>
	[
		new(TimeSpan.FromMilliseconds(-1), false, "Negative grace period"),
		new(TimeSpan.FromMilliseconds(-100), false, "Large negative grace period")
	];

	[Theory]
	[MemberData(nameof(InvalidChannelCapacities))]
	public void ChannelCapacity_WhenInvalid_ShouldThrowArgumentOutOfRangeException(ChannelCapacityTestCase testCase)
	{
		Log("Testing channel capacity: {0}", testCase.Description);
		var    options = new ProcessUciTransportOptions { ChannelCapacity = testCase.Value };
		Action act     = () => _ = new ProcessUciTransport("any/nonempty/path", null, null, options);

		act.Should().Throw<ArgumentOutOfRangeException>();
	}

	[Fact]
	public void NewLine_WhenEmpty_ShouldThrowArgumentException()
	{
		Log("Testing empty newline");
		var    options = new ProcessUciTransportOptions { NewLine = "" };
		Action act     = () => _ = new ProcessUciTransport("any/nonempty/path", null, null, options);

		act.Should().Throw<ArgumentException>();
	}

	[Theory]
	[MemberData(nameof(InvalidQuitGracePeriods))]
	public void QuitGracePeriod_WhenInvalid_ShouldThrowArgumentOutOfRangeException(TimeoutTestCase testCase)
	{
		Log("Testing quit grace period: {0}", testCase.Description);
		var    options = new ProcessUciTransportOptions { QuitGracePeriod = testCase.Timeout };
		Action act     = () => _ = new ProcessUciTransport("any/nonempty/path", null, null, options);

		act.Should().Throw<ArgumentOutOfRangeException>();
	}

	[Fact]
	public void QuitGracePeriodMs_WhenNegative_ShouldThrowArgumentOutOfRangeException()
	{
		Log("Testing negative QuitGracePeriodMs");
		var    options = new ProcessUciTransportOptions { QuitGracePeriod = TimeSpan.Zero, QuitGracePeriodMs = -1 };
		Action act     = () => _ = new ProcessUciTransport("any/nonempty/path", null, null, options);

		act.Should().Throw<ArgumentOutOfRangeException>();
	}
}
