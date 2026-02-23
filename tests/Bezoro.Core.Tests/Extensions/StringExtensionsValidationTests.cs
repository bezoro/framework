using System;
using Bezoro.Core.Extensions;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Extensions;

[TestSubject(typeof(StringExtensions))]
public class StringExtensionsValidationTests
{
	[Fact]
	public void IsEmpty_WhenCalled_ShouldReturnFalse_ForNonWhitespace()
	{
		"a".IsEmpty().Should().BeFalse();
		" a ".IsEmpty().Should().BeFalse();
	}

	[Fact]
	public void IsEmpty_WhenCalled_ShouldReturnTrue_ForEmptyOrWhitespace()
	{
		"".IsEmpty().Should().BeTrue();
		"   ".IsEmpty().Should().BeTrue();
		"\t\n".IsEmpty().Should().BeTrue();
	}

	[Fact]
	public void IsNullOrEmpty_WhenCalled_ShouldReturnFalse_ForNonEmpty()
	{
		"a".IsNullOrEmpty().Should().BeFalse();
		" a ".IsNullOrEmpty().Should().BeFalse();
	}

	[Fact]
	public void IsNullOrEmpty_WhenCalled_ShouldReturnTrue_ForNullOrEmpty()
	{
		string? s = null;
		s.IsNullOrEmpty().Should().BeTrue();
		"".IsNullOrEmpty().Should().BeTrue();
		" ".IsNullOrEmpty().Should().BeTrue();
		"\t".IsNullOrEmpty().Should().BeTrue();
		"\n".IsNullOrEmpty().Should().BeTrue();
	}

	[Fact]
	public void IsNullOrWhiteSpace_WhenCalled_ShouldReturnFalse_ForNonWhitespace()
	{
		"a".IsNullOrWhiteSpace().Should().BeFalse();
		" a ".IsNullOrWhiteSpace().Should().BeFalse();
	}

	[Fact]
	public void IsNullOrWhiteSpace_WhenCalled_ShouldReturnTrue_ForNullEmptyOrWhitespace()
	{
		string? s = null;
		s.IsNullOrWhiteSpace().Should().BeTrue();
		"".IsNullOrWhiteSpace().Should().BeTrue();
		"   ".IsNullOrWhiteSpace().Should().BeTrue();
		"\t\n".IsNullOrWhiteSpace().Should().BeTrue();
	}

	[Fact]
	public void IsWhiteSpace_WhenCalled_ShouldReturnFalse_ForEmptyAndNonWhitespace()
	{
		"".IsWhiteSpace().Should().BeFalse();
		"a".IsWhiteSpace().Should().BeFalse();
		" a ".IsWhiteSpace().Should().BeFalse();
	}

	[Fact]
	public void IsWhiteSpace_WhenCalled_ShouldReturnTrue_ForWhitespaceOnly()
	{
		" ".IsWhiteSpace().Should().BeTrue();
		"\t\n".IsWhiteSpace().Should().BeTrue();
	}

	[Fact]
	public void Lowercase_WhenNull_ShouldThrow()
	{
		string? s = null;

		Action act = () => s!.Lowercase();

		act.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void Repeat_WhenCalled_ShouldThrowOnNullString()
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
	public void Size_WhenNull_ShouldThrow()
	{
		string? s = null;

		Action act = () => s!.Size(16);

		act.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void StringExtensionsValidation_WhenCalled_ShouldBold_WhenEmpty_Throws()
	{
		string s = string.Empty;

		Action act = () => s.Bold();

		act.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void StringExtensionsValidation_WhenCalled_ShouldBold_WhenNull_Throws()
	{
		string? s = null;

		Action act = () => s!.Bold();

		act.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void StringExtensionsValidation_WhenCalled_ShouldBold_WhenWhitespace_Throws()
	{
		var s = "   ";

		Action act = () => s.Bold();

		act.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void StringExtensionsValidation_WhenCalled_ShouldBracketed_WhenEmpty_Throws()
	{
		string s = string.Empty;

		Action act = () => s.Bracketed();

		act.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void StringExtensionsValidation_WhenCalled_ShouldBracketed_WhenNull_Throws()
	{
		string? s = null;

		Action act = () => s!.Bracketed();

		act.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void StringExtensionsValidation_WhenCalled_ShouldBracketed_WhenWhitespace_Throws()
	{
		var s = "   ";

		Action act = () => s.Bracketed();

		act.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void StringExtensionsValidation_WhenCalled_ShouldCapitalize_WhenEmpty_Throws()
	{
		string s = string.Empty;

		Action act = () => s.Capitalize();

		act.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void StringExtensionsValidation_WhenCalled_ShouldCapitalize_WhenNull_Throws()
	{
		string? s = null;

		Action act = () => s!.Capitalize();

		act.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void StringExtensionsValidation_WhenCalled_ShouldCapitalize_WhenWhitespace_Throws()
	{
		var s = "   ";

		Action act = () => s.Capitalize();

		act.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void StringExtensionsValidation_WhenCalled_ShouldItalic_WhenEmpty_Throws()
	{
		string s = string.Empty;

		Action act = () => s.Italic();

		act.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void StringExtensionsValidation_WhenCalled_ShouldItalic_WhenNull_Throws()
	{
		string? s = null;

		Action act = () => s!.Italic();

		act.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void StringExtensionsValidation_WhenCalled_ShouldItalic_WhenWhitespace_Throws()
	{
		var s = "   ";

		Action act = () => s.Italic();

		act.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void StringExtensionsValidation_WhenCalled_ShouldLowercase_WhenEmpty_Throws()
	{
		string s = string.Empty;

		Action act = () => s.Lowercase();

		act.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void StringExtensionsValidation_WhenCalled_ShouldLowercase_WhenWhitespace_Throws()
	{
		var s = "   ";

		Action act = () => s.Lowercase();

		act.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void StringExtensionsValidation_WhenCalled_ShouldSize_WhenEmpty_Throws()
	{
		string s = string.Empty;

		Action act = () => s.Size(16);

		act.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void StringExtensionsValidation_WhenCalled_ShouldSize_WhenWhitespace_Throws()
	{
		var s = "   ";

		Action act = () => s.Size(16);

		act.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void StringExtensionsValidation_WhenCalled_ShouldStrikethrough_WhenEmpty_Throws()
	{
		string s = string.Empty;

		Action act = () => s.Strikethrough();

		act.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void StringExtensionsValidation_WhenCalled_ShouldStrikethrough_WhenNull_Throws()
	{
		string? s = null;

		Action act = () => s!.Strikethrough();

		act.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void StringExtensionsValidation_WhenCalled_ShouldStrikethrough_WhenWhitespace_Throws()
	{
		var s = "   ";

		Action act = () => s.Strikethrough();

		act.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void StringExtensionsValidation_WhenCalled_ShouldUnderline_WhenEmpty_Throws()
	{
		string s = string.Empty;

		Action act = () => s.Underline();

		act.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void StringExtensionsValidation_WhenCalled_ShouldUnderline_WhenNull_Throws()
	{
		string? s = null;

		Action act = () => s!.Underline();

		act.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void StringExtensionsValidation_WhenCalled_ShouldUnderline_WhenWhitespace_Throws()
	{
		var s = "   ";

		Action act = () => s.Underline();

		act.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void StringExtensionsValidation_WhenCalled_ShouldUppercase_WhenEmpty_Throws()
	{
		string s = string.Empty;

		Action act = () => s.Uppercase();

		act.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void StringExtensionsValidation_WhenCalled_ShouldUppercase_WhenNull_Throws()
	{
		string? s = null;

		Action act = () => s!.Uppercase();

		act.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void StringExtensionsValidation_WhenCalled_ShouldUppercase_WhenWhitespace_Throws()
	{
		var s = "   ";

		Action act = () => s.Uppercase();

		act.Should().Throw<ArgumentNullException>();
	}
}
