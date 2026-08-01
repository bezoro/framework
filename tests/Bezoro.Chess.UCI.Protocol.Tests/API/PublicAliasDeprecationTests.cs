using System.Reflection;
using FluentAssertions;

namespace Bezoro.Chess.UCI.Protocol.Tests.API;

public sealed class PublicAliasDeprecationTests
{
	[Theory]
#pragma warning disable CS0618 // Intentional reference to the compatibility alias under test.
	[InlineData(nameof(UciPlayableMatchSession.PlayEngineMoveAsync), "Use PlayControlledMoveAsync instead.")]
	[InlineData(nameof(UciPlayableMatchSession.ApplyHumanMove), "Use ApplyMove instead.")]
#pragma warning restore CS0618
	public void CompatibilityMethod_WhenInspected_ShouldHaveNonErrorObsoleteMetadata(
		string memberName,
		string expectedMessage)
	{
		var member = typeof(UciPlayableMatchSession).GetMethod(memberName, BindingFlags.Instance | BindingFlags.Public);

		member.Should().NotBeNull();
		var obsolete = member!.GetCustomAttribute<ObsoleteAttribute>();
		obsolete.Should().NotBeNull();
		obsolete!.Message.Should().Be(expectedMessage);
		obsolete.IsError.Should().BeFalse();
	}

	[Theory]
#pragma warning disable CS0618 // Intentional reference to the compatibility alias under test.
	[InlineData(nameof(UciEngineClient.BestMoveReceived), "Use BestMoveMessageReceived instead.")]
	[InlineData(nameof(UciEngineClient.LineReceived), "Use RawLineReceived instead.")]
#pragma warning restore CS0618
	public void CompatibilityEvent_WhenInspected_ShouldHaveNonErrorObsoleteMetadata(
		string memberName,
		string expectedMessage)
	{
		var member = typeof(UciEngineClient).GetEvent(memberName, BindingFlags.Instance | BindingFlags.Public);

		member.Should().NotBeNull();
		var obsolete = member!.GetCustomAttribute<ObsoleteAttribute>();
		obsolete.Should().NotBeNull();
		obsolete!.Message.Should().Be(expectedMessage);
		obsolete.IsError.Should().BeFalse();
	}

	[Fact]
	public void InfoPvReceived_WhenInspected_ShouldRemainNonObsolete()
	{
		var member = typeof(UciEngineClient).GetEvent(
			nameof(UciEngineClient.InfoPvReceived),
			BindingFlags.Instance | BindingFlags.Public
		);

		member.Should().NotBeNull();
		member!.GetCustomAttribute<ObsoleteAttribute>().Should().BeNull();
	}
}
