using System;
using System.Globalization;
using Bezoro.Core.Extensions;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;
using Color = Bezoro.Core.Types.Color;

namespace Bezoro.Core.Tests.Common.Extensions.String;

[TestSubject(typeof(StringExtensions))]
public static class StringExtensionsColourTests
{
	public class Unit
	{
		[Fact]
		public void Color_with_bytes_matches_FromArgb()
		{
			const string text     = "t";
			string       expected = text.Color(Color.FromArgb(32, 255, 128, 64)); // a, r, g, b

			text.Color(255, 128, 64, 32).Should().Be(expected); // r, g, b, a
		}

		[Fact]
		public void Color_with_color_name_wraps_text()
		{
			const string text = "hello";
			const string name = "red";

			string result = text.Color(name);

			result.Should().Be("<color=red>hello</color>");
		}

		[Fact]
		public void Color_with_Color_struct_uses_RGBA_format()
		{
			const string text  = "hello";
			var          color = new Color(1f, 0.5f, 0.25f, 0.75f);

			var    expected = $"<color={color.ToString("RGBA", CultureInfo.InvariantCulture)}>{text}</color>";
			string result   = text.Color(color);

			result.Should().Be(expected);
		}

		[Fact]
		public void Color_with_floats_matches_ctor()
		{
			const string text = "t";
			var          c    = new Color(1f, 0.5f, 0.25f, 0.125f);

			text.Color(1f, 0.5f, 0.25f, 0.125f).Should().Be(text.Color(c));
		}

		[Fact]
		public void Color_with_rgba32_matches_FromRgba32()
		{
			const string text   = "t";
			const uint   rgba32 = 0x3366CC99; // RRGGBBAA

			text.Color(rgba32).Should().Be(text.Color(Color.FromRgba32(rgba32)));
		}

		[Fact]
		public void ColorHex_falls_back_to_named_color_when_parse_fails()
		{
			const string text = "hello";
			const string name = "not-a-color";

			text.ColorHex(name).Should().Be(text.Color(name));
		}

		[Fact]
		public void ColorHex_parses_valid_hex_and_matches_Color_overload()
		{
			const string text  = "hello";
			const string input = "#11223344"; // RRGGBBAA

			bool parsed = Color.TryParse(input.AsSpan(), CultureInfo.InvariantCulture, out var c);
			parsed.Should().BeTrue("input should be a valid hex color RRGGBBAA");

			text.ColorHex(input).Should().Be(text.Color(c));
		}
	}
}


