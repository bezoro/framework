using System;
using System.Threading;
using Bezoro.Core.Utilities;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Utilities;

[TestSubject(typeof(StringTags))]
public class StringTagsTests
{
	private static readonly Lock Sync = new();

	[Fact]
	public void GetRegisteredTags_WhenCalled_ShouldReturnCurrentSet()
	{
		lock (Sync)
		{
			StringTags.Clear();
			StringTags.RegisterValue("A", "1");
			StringTags.RegisterValue("B", "2");

			StringTags.GetRegisteredTags().Should().BeEquivalentTo("A", "B");
		}
	}

	[Fact]
	public void Process_WhenCalled_ShouldReturnInput_WhenNullOrEmpty()
	{
		lock (Sync)
		{
			StringTags.Clear();

			StringTags.Process(null!).Should().BeNull();
			StringTags.Process(string.Empty).Should().BeEmpty();
		}
	}

	[Fact]
	public void Register_WhenCalled_ShouldThrowOnInvalidOrWhitespaceName()
	{
		lock (Sync)
		{
			StringTags.Clear();
			var act1 = () => StringTags.Register("bad-name", () => "x");
			var act2 = () => StringTags.Register("  ",       () => "x");
			act1.Should().Throw<ArgumentException>();
			act2.Should().Throw<ArgumentException>();
		}
	}

	[Fact]
	public void Register_WhenCalled_ShouldThrowOnNullProvider()
	{
		lock (Sync)
		{
			StringTags.Clear();
			var act = () => StringTags.Register("A", null!);
			act.Should().Throw<ArgumentNullException>();
		}
	}

	[Fact]
	public void StringTags_WhenCalled_ShouldClear_RemovesAllTags()
	{
		lock (Sync)
		{
			StringTags.Clear();
			StringTags.RegisterValue("A", "1");
			StringTags.Clear();
			StringTags.Process("{A}").Should().Be("{A}");
		}
	}

	[Fact]
	public void StringTags_WhenCalled_ShouldProcess_AllowsEscapedBraces()
	{
		lock (Sync)
		{
			StringTags.Clear();
			var input    = @"\{Name\} literal and \{Unknown}";
			var expected = "{Name} literal and {Unknown}";
			StringTags.Process(input).Should().Be(expected);
		}
	}

	[Fact]
	public void StringTags_WhenCalled_ShouldProcess_LeavesUnknownTagsIntact()
	{
		lock (Sync)
		{
			StringTags.Clear();
			StringTags.Process("Hello {Unknown}!").Should().Be("Hello {Unknown}!");
		}
	}

	[Fact]
	public void StringTags_WhenCalled_ShouldProcess_ReplacesRegisteredValueTag()
	{
		lock (Sync)
		{
			StringTags.Clear();
			StringTags.RegisterValue("Name", "John");
			StringTags.Process("Hello {Name}!").Should().Be("Hello John!");
		}
	}

	[Fact]
	public void StringTags_WhenCalled_ShouldProcess_WhenProviderThrows_LeavesTagUnchanged()
	{
		lock (Sync)
		{
			StringTags.Clear();
			StringTags.Register("Crash", () => throw new());
			StringTags.Process("X {Crash} Y").Should().Be("X {Crash} Y");
		}
	}

	[Fact]
	public void StringTags_WhenCalled_ShouldRegister_AllowOverwrite_UpdatesValue()
	{
		lock (Sync)
		{
			StringTags.Clear();
			StringTags.RegisterValue("Tag", 1);
			StringTags.RegisterValue("Tag", 2, true);
			StringTags.Process("{Tag}").Should().Be("2");
		}
	}

	[Fact]
	public void StringTags_WhenCalled_ShouldRegister_DisallowOverwrite_Throws()
	{
		lock (Sync)
		{
			StringTags.Clear();
			StringTags.RegisterValue("Tag", 1);
			var act = () => StringTags.RegisterValue("Tag", 2);
			act.Should().Throw<InvalidOperationException>();
		}
	}

	[Fact]
	public void StringTags_WhenCalled_ShouldUnregister_RemovesSpecificTag()
	{
		lock (Sync)
		{
			StringTags.Clear();
			StringTags.RegisterValue("A", "1");
			StringTags.RegisterValue("B", "2");
			StringTags.Unregister("A");

			StringTags.Process("{A} {B}").Should().Be("{A} 2");
		}
	}
}
