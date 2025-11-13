using System;
using Bezoro.Core.Common.Extensions.String;
using Bezoro.Core.Common.Primitives;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Common.Extensions.String;

[TestSubject(typeof(Checks))]
public static class ChecksTests
{
	public class Unit
	{
		[Fact]
		public void Bold_WhenEmpty_Throws()
		{
			string s = string.Empty;

			Action act = () => s.Bold();

			act.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void Bold_WhenNull_Throws()
		{
			string? s = null;

			Action act = () => s!.Bold();

			act.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void Bold_WhenWhitespace_Throws()
		{
			var s = "   ";

			Action act = () => s.Bold();

			act.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void Bold_WrapsText()
		{
			"hello".Bold().Should().Be("<b>hello</b>");
		}

		[Fact]
		public void Bracketed_DefaultBracket_NoColor_NoPadding()
		{
			"abc".Bracketed().Should().Be("[abc]");
		}

		[Fact]
		public void Bracketed_UnknownBracket_UsesSameForClosing()
		{
			"bar".Bracketed(bracket: '|').Should().Be("|bar|");
		}

		[Fact]
		public void Bracketed_UsesCorrectClosingBracket_ForVariousOpenings()
		{
			"t".Bracketed(bracket: '(').Should().Be("(t)");
			"t".Bracketed(bracket: '{').Should().Be("{t}");
			"t".Bracketed(bracket: '<').Should().Be("<t>");
		}

		[Fact]
		public void Bracketed_WhenEmpty_Throws()
		{
			string s = string.Empty;

			Action act = () => s.Bracketed();

			act.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void Bracketed_WhenNull_Throws()
		{
			string? s = null;

			Action act = () => s!.Bracketed();

			act.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void Bracketed_WhenWhitespace_Throws()
		{
			var s = "   ";

			Action act = () => s.Bracketed();

			act.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void Bracketed_WithColor_ColorsOnlyBrackets()
		{
			var red = Color.FromRgb(255, 0, 0);
			"abc".Bracketed(color: red, bracket: '[')
				 .Should().Be("<color=#FF0000>[</color>abc<color=#FF0000>]</color>");
		}

		[Fact]
		public void Bracketed_WithPadding_AddsSpacesAroundText()
		{
			"abc".Bracketed(1).Should().Be("[ abc ]");
			"xy".Bracketed(2, bracket: '(').Should().Be("(  xy  )");
		}

		[Fact]
		public void Capitalize_UppercasesFirstCharacter()
		{
			"hello".Capitalize().Should().Be("Hello");
			"h".Capitalize().Should().Be("H");
		}

		[Fact]
		public void Capitalize_WhenEmpty_Throws()
		{
			string s = string.Empty;

			Action act = () => s.Capitalize();

			act.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void Capitalize_WhenNull_Throws()
		{
			string? s = null;

			Action act = () => s!.Capitalize();

			act.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void Capitalize_WhenWhitespace_Throws()
		{
			var s = "   ";

			Action act = () => s.Capitalize();

			act.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void IsEmpty_ReturnsFalse_ForNonWhitespace()
		{
			"a".IsEmpty().Should().BeFalse();
			" a ".IsEmpty().Should().BeFalse();
		}

		[Fact]
		public void IsEmpty_ReturnsTrue_ForEmptyOrWhitespace()
		{
			"".IsEmpty().Should().BeTrue();
			"   ".IsEmpty().Should().BeTrue();
			"\t\n".IsEmpty().Should().BeTrue();
		}

		[Fact]
		public void IsNullOrEmpty_ReturnsFalse_ForNonEmpty()
		{
			"a".IsNullOrEmpty().Should().BeFalse();
			" a ".IsNullOrEmpty().Should().BeFalse();
		}

		[Fact]
		public void IsNullOrEmpty_ReturnsTrue_ForNullOrEmpty()
		{
			string? s = null;
			s.IsNullOrEmpty().Should().BeTrue();
			"".IsNullOrEmpty().Should().BeTrue();
			" ".IsNullOrEmpty().Should().BeTrue();
			"\t".IsNullOrEmpty().Should().BeTrue();
			"\n".IsNullOrEmpty().Should().BeTrue();
		}

		[Fact]
		public void IsNullOrWhiteSpace_ReturnsFalse_ForNonWhitespace()
		{
			"a".IsNullOrWhiteSpace().Should().BeFalse();
			" a ".IsNullOrWhiteSpace().Should().BeFalse();
		}

		[Fact]
		public void IsNullOrWhiteSpace_ReturnsTrue_ForNullEmptyOrWhitespace()
		{
			string? s = null;
			s.IsNullOrWhiteSpace().Should().BeTrue();
			"".IsNullOrWhiteSpace().Should().BeTrue();
			"   ".IsNullOrWhiteSpace().Should().BeTrue();
			"\t\n".IsNullOrWhiteSpace().Should().BeTrue();
		}

		[Fact]
		public void IsWhiteSpace_ReturnsFalse_ForEmptyAndNonWhitespace()
		{
			"".IsWhiteSpace().Should().BeFalse();
			"a".IsWhiteSpace().Should().BeFalse();
			" a ".IsWhiteSpace().Should().BeFalse();
		}

		[Fact]
		public void IsWhiteSpace_ReturnsTrue_ForWhitespaceOnly()
		{
			" ".IsWhiteSpace().Should().BeTrue();
			"\t\n".IsWhiteSpace().Should().BeTrue();
		}

		[Fact]
		public void Italic_WhenEmpty_Throws()
		{
			string s = string.Empty;

			Action act = () => s.Italic();

			act.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void Italic_WhenNull_Throws()
		{
			string? s = null;

			Action act = () => s!.Italic();

			act.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void Italic_WhenWhitespace_Throws()
		{
			var s = "   ";

			Action act = () => s.Italic();

			act.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void Italic_WrapsText()
		{
			"hello".Italic().Should().Be("<i>hello</i>");
		}

		[Fact]
		public void Lowercase_ReturnsLower_ForNonNull()
		{
			"ABC".Lowercase().Should().Be("abc");
			"AbC".Lowercase().Should().Be("abc");
		}

		[Fact]
		public void Lowercase_WhenEmpty_Throws()
		{
			string s = string.Empty;

			Action act = () => s.Lowercase();

			act.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void Lowercase_WhenNull_ShouldThrow()
		{
			string? s = null;

			Action act = () => s!.Lowercase();

			act.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void Lowercase_WhenWhitespace_Throws()
		{
			var s = "   ";

			Action act = () => s.Lowercase();

			act.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void Repeat_Repeats_OnCountGreaterThanOne()
		{
			"ab".Repeat(3).Should().Be("ababab");
		}

		[Fact]
		public void Repeat_ReturnsOriginal_OnCountOne()
		{
			"ab".Repeat().Should().Be("ab");
		}

		[Fact]
		public void Repeat_Throws_OnNullString()
		{
			string? s   = null;
			Action  act = () => s!.Repeat(3);
			act.Should().Throw<ArgumentNullException>()
			   .And.ParamName.Should().Be("str");
		}

		[Fact]
		public void Repeat_WhenEmpty_ShouldThrow()
		{
			string s   = string.Empty;
			Action act = () => s.Repeat(3);
			act.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void Repeat_WhenZeroCount_ShouldThrow()
		{
			Action act = () => "ab".Repeat(0);
			var exception = act.Should().Throw<ArgumentOutOfRangeException>()
							   .Which;
			exception.ParamName.Should().Be("count");
			exception.ActualValue.Should().Be(0u);
		}

		[Fact]
		public void Size_WhenEmpty_Throws()
		{
			string s = string.Empty;

			Action act = () => s.Size(16);

			act.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void Size_WhenNull_ShouldThrow()
		{
			string? s = null;

			Action act = () => s!.Size(16);

			act.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void Size_WhenWhitespace_Throws()
		{
			var s = "   ";

			Action act = () => s.Size(16);

			act.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void Size_WrapsText_WithProvidedSize()
		{
			"hello".Size(16).Should().Be("<size=16>hello</size>");
		}

		[Fact]
		public void Strikethrough_WhenEmpty_Throws()
		{
			string s = string.Empty;

			Action act = () => s.Strikethrough();

			act.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void Strikethrough_WhenNull_Throws()
		{
			string? s = null;

			Action act = () => s!.Strikethrough();

			act.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void Strikethrough_WhenWhitespace_Throws()
		{
			var s = "   ";

			Action act = () => s.Strikethrough();

			act.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void Strikethrough_WrapsText()
		{
			"hello".Strikethrough().Should().Be("<s>hello</s>");
		}

		[Fact]
		public void Underline_WhenEmpty_Throws()
		{
			string s = string.Empty;

			Action act = () => s.Underline();

			act.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void Underline_WhenNull_Throws()
		{
			string? s = null;

			Action act = () => s!.Underline();

			act.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void Underline_WhenWhitespace_Throws()
		{
			var s = "   ";

			Action act = () => s.Underline();

			act.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void Underline_WrapsText()
		{
			"hello".Underline().Should().Be("<u>hello</u>");
		}

		[Fact]
		public void Uppercase_ReturnsUpper_ForNonNull()
		{
			"abc".Uppercase().Should().Be("ABC");
			"aBc".Uppercase().Should().Be("ABC");
		}

		[Fact]
		public void Uppercase_WhenEmpty_Throws()
		{
			string s = string.Empty;

			Action act = () => s.Uppercase();

			act.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void Uppercase_WhenNull_Throws()
		{
			string? s = null;

			Action act = () => s!.Uppercase();

			act.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void Uppercase_WhenWhitespace_Throws()
		{
			var s = "   ";

			Action act = () => s.Uppercase();

			act.Should().Throw<ArgumentNullException>();
		}
	}
}
