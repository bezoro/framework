using System;
using System.Globalization;
using Bezoro.Core.Extensions;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;
using Color = Bezoro.Core.Types.Color;

namespace Bezoro.Core.Tests.Extensions;

[TestSubject(typeof(StringExtensions))]
public class StringExtensionsColourTests
{
	[Fact]
	public void StringExtensionsColour_WhenCalled_ShouldColor_with_bytes_matches_FromArgb()
	{
		const string TEXT     = "t";
		string       expected = TEXT.Color(Color.FromArgb(32, 255, 128, 64)); // a, r, g, b

		TEXT.Color(255, 128, 64, 32).Should().Be(expected); // r, g, b, a
	}

	[Fact]
	public void StringExtensionsColour_WhenCalled_ShouldColor_with_color_name_wraps_text()
	{
		const string TEXT = "hello";
		const string NAME = "red";

		string result = TEXT.Color(NAME);

		result.Should().Be("<color=red>hello</color>");
	}

	[Fact]
	public void StringExtensionsColour_WhenCalled_ShouldColor_with_Color_struct_uses_RGBA_format()
	{
		const string TEXT  = "hello";
		var          color = new Color(1f, 0.5f, 0.25f, 0.75f);

		var    expected = $"<color={color.ToString("RGBA", CultureInfo.InvariantCulture)}>{TEXT}</color>";
		string result   = TEXT.Color(color);

		result.Should().Be(expected);
	}

	[Fact]
	public void StringExtensionsColour_WhenCalled_ShouldColor_with_floats_matches_ctor()
	{
		const string TEXT = "t";
		var          c    = new Color(1f, 0.5f, 0.25f, 0.125f);

		TEXT.Color(1f, 0.5f, 0.25f, 0.125f).Should().Be(TEXT.Color(c));
	}

	[Fact]
	public void StringExtensionsColour_WhenCalled_ShouldColor_with_rgba32_matches_FromRgba32()
	{
		const string TEXT   = "t";
		const uint   RGBA32 = 0x3366CC99; // RRGGBBAA

		TEXT.Color(RGBA32).Should().Be(TEXT.Color(Color.FromRgba32(RGBA32)));
	}

	[Fact]
	public void StringExtensionsColour_WhenCalled_ShouldColorHex_falls_back_to_named_color_when_parse_fails()
	{
		const string TEXT = "hello";
		const string NAME = "not-a-color";

		TEXT.ColorHex(NAME).Should().Be(TEXT.Color(NAME));
	}

	[Fact]
	public void StringExtensionsColour_WhenCalled_ShouldColorHex_parses_valid_hex_and_matches_Color_overload()
	{
		const string TEXT  = "hello";
		const string INPUT = "#11223344"; // RRGGBBAA

		bool parsed = Color.TryParse(INPUT.AsSpan(), CultureInfo.InvariantCulture, out var c);
		parsed.Should().BeTrue("input should be a valid hex color RRGGBBAA");

		TEXT.ColorHex(INPUT).Should().Be(TEXT.Color(c));
	}
}
