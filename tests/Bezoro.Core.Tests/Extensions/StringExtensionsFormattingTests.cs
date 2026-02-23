using Bezoro.Core.Extensions;
using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Extensions;

[TestSubject(typeof(StringExtensions))]
public class StringExtensionsFormattingTests
{
	[Fact]
	public void Lowercase_WhenCalled_ShouldReturnLower_ForNonNull()
	{
		"ABC".Lowercase().Should().Be("abc");
		"AbC".Lowercase().Should().Be("abc");
	}

	[Fact]
	public void Repeat_WhenCalled_ShouldReturnOriginal_OnCountOne()
	{
		"ab".Repeat().Should().Be("ab");
	}

	[Fact]
	public void StringExtensionsFormatting_WhenCalled_ShouldBold_WrapsText()
	{
		"hello".Bold().Should().Be("<b>hello</b>");
	}

	[Fact]
	public void StringExtensionsFormatting_WhenCalled_ShouldBracketed_DefaultBracket_NoColor_NoPadding()
	{
		"abc".Bracketed().Should().Be("[abc]");
	}

	[Fact]
	public void StringExtensionsFormatting_WhenCalled_ShouldBracketed_UnknownBracket_UsesSameForClosing()
	{
		"bar".Bracketed(bracket: '|').Should().Be("|bar|");
	}

	[Fact]
	public void StringExtensionsFormatting_WhenCalled_ShouldBracketed_UsesCorrectClosingBracket_ForVariousOpenings()
	{
		"t".Bracketed(bracket: '(').Should().Be("(t)");
		"t".Bracketed(bracket: '{').Should().Be("{t}");
		"t".Bracketed(bracket: '<').Should().Be("<t>");
	}

	[Fact]
	public void StringExtensionsFormatting_WhenCalled_ShouldBracketed_WithColor_ColorsOnlyBrackets()
	{
		var red = Color.FromRgb(255, 0, 0);
		"abc".Bracketed(color: red, bracket: '[')
			 .Should().Be("<color=#FF0000>[</color>abc<color=#FF0000>]</color>");
	}

	[Fact]
	public void StringExtensionsFormatting_WhenCalled_ShouldBracketed_WithPadding_AddsSpacesAroundText()
	{
		"abc".Bracketed(1).Should().Be("[ abc ]");
		"xy".Bracketed(2, bracket: '(').Should().Be("(  xy  )");
	}

	[Fact]
	public void StringExtensionsFormatting_WhenCalled_ShouldCapitalize_UppercasesFirstCharacter()
	{
		"hello".Capitalize().Should().Be("Hello");
		"h".Capitalize().Should().Be("H");
	}

	[Fact]
	public void StringExtensionsFormatting_WhenCalled_ShouldItalic_WrapsText()
	{
		"hello".Italic().Should().Be("<i>hello</i>");
	}

	[Fact]
	public void StringExtensionsFormatting_WhenCalled_ShouldRepeat_Repeats_OnCountGreaterThanOne()
	{
		"ab".Repeat(3).Should().Be("ababab");
	}

	[Fact]
	public void StringExtensionsFormatting_WhenCalled_ShouldSize_WrapsText_WithProvidedSize()
	{
		"hello".Size(16).Should().Be("<size=16>hello</size>");
	}

	[Fact]
	public void StringExtensionsFormatting_WhenCalled_ShouldStrikethrough_WrapsText()
	{
		"hello".Strikethrough().Should().Be("<s>hello</s>");
	}

	[Fact]
	public void StringExtensionsFormatting_WhenCalled_ShouldUnderline_WrapsText()
	{
		"hello".Underline().Should().Be("<u>hello</u>");
	}

	[Fact]
	public void Uppercase_WhenCalled_ShouldReturnUpper_ForNonNull()
	{
		"abc".Uppercase().Should().Be("ABC");
		"aBc".Uppercase().Should().Be("ABC");
	}
}
