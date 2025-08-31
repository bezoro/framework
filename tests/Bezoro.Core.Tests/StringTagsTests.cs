using System;
using System.Threading;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests;

[TestSubject(typeof(StringTags))]
public static class StringTagsTests
{
	public class Unit
	{
		private static readonly Lock Sync = new();

		[Fact]
		public void Clear_RemovesAllTags()
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
		public void GetRegisteredTags_ReturnsCurrentSet()
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
		public void Process_AllowsEscapedBraces()
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
		public void Process_LeavesUnknownTagsIntact()
		{
			lock (Sync)
			{
				StringTags.Clear();
				StringTags.Process("Hello {Unknown}!").Should().Be("Hello {Unknown}!");
			}
		}

		[Fact]
		public void Process_ReplacesRegisteredValueTag()
		{
			lock (Sync)
			{
				StringTags.Clear();
				StringTags.RegisterValue("Name", "John");
				StringTags.Process("Hello {Name}!").Should().Be("Hello John!");
			}
		}

		[Fact]
		public void Process_ReturnsInput_WhenNullOrEmpty()
		{
			lock (Sync)
			{
				StringTags.Clear();

				StringTags.Process(null!).Should().BeNull();
				StringTags.Process(string.Empty).Should().BeEmpty();
			}
		}

		[Fact]
		public void Process_WhenProviderThrows_LeavesTagUnchanged()
		{
			lock (Sync)
			{
				StringTags.Clear();
				StringTags.Register("Crash", () => throw new());
				StringTags.Process("X {Crash} Y").Should().Be("X {Crash} Y");
			}
		}

		[Fact]
		public void Register_AllowOverwrite_UpdatesValue()
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
		public void Register_DisallowOverwrite_Throws()
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
		public void Register_Throws_OnInvalidOrWhitespaceName()
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
		public void Register_Throws_OnNullProvider()
		{
			lock (Sync)
			{
				StringTags.Clear();
				var act = () => StringTags.Register("A", null!);
				act.Should().Throw<ArgumentNullException>();
			}
		}

		[Fact]
		public void Unregister_RemovesSpecificTag()
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
}
