using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Bezoro.Core.Extensions;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;
using Color = Bezoro.Core.Types.Color;

namespace Bezoro.Core.Tests.Extensions;

[TestSubject(typeof(StringExtensions))]
public class StringExtensionsColourTests
{
	private const string ObsoleteMessage = "Use Color(string) or Color(Color) instead.";

	private static readonly string[] NamedColorMethodNames =
	[
		"Red", "Green", "Blue", "Yellow", "Cyan", "Magenta", "White", "Black", "Gray", "LightGray", "Orange",
		"Purple", "Brown", "Pink", "LightRed", "Crimson", "DarkRed", "Maroon", "IndianRed", "FireBrick", "Salmon",
		"Coral", "Tomato", "SkyBlue", "LightBlue", "DeepBlue", "Navy", "RoyalBlue", "CornflowerBlue", "SteelBlue",
		"DodgerBlue", "DeepSkyBlue", "Teal", "LightGreen", "DarkGreen", "ForestGreen", "Lime", "Olive", "MediumSeaGreen",
		"SpringGreen", "SeaGreen", "PaleGreen", "YellowGreen", "Gold", "LightYellow", "LemonChiffon", "Khaki", "DarkKhaki",
		"Goldenrod", "DarkGoldenrod", "Amber", "Lavender", "Violet", "Plum", "Orchid", "MediumPurple", "DarkOrchid",
		"DarkViolet", "BlueViolet", "Indigo", "MediumOrchid", "SandyBrown", "RosyBrown", "Peru", "Chocolate",
		"SaddleBrown", "Sienna", "Tan", "BurlyWood", "DarkOrange", "LightOrange", "OrangeRed", "Peach", "Tangerine",
		"DarkGray", "DimGray", "Silver", "WhiteSmoke", "Gainsboro", "SlateGray", "PastelPink", "PastelBlue", "PastelGreen",
		"PastelYellow", "PastelPurple", "PastelOrange", "PastelTurquoise", "PastelLavender", "NeonPink", "NeonGreen",
		"NeonBlue", "NeonYellow", "NeonOrange", "NeonPurple"
	];

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
	public void Color_WhenCalledWithCanonicalInputs_ShouldPreserveMarkup()
	{
		"hello".Color("red").Should().Be("<color=red>hello</color>");
		"hello".Color(new Color(1f, 0f, 0f, 1f)).Should().Be("<color=#FF0000FF>hello</color>");
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
	public void ColorHexCompatibility_WhenParseFails_ShouldFallBackToNamedColor()
	{
		const string TEXT = "hello";
		const string NAME = "not-a-color";

#pragma warning disable CS0618 // Dedicated compatibility-wrapper assertion.
		TEXT.ColorHex(NAME).Should().Be(TEXT.Color(NAME));
#pragma warning restore CS0618
	}

	[Fact]
	public void ColorHexCompatibility_WhenHexIsValid_ShouldMatchColorOverload()
	{
		const string TEXT  = "hello";
		const string INPUT = "#11223344"; // RRGGBBAA

		bool parsed = Color.TryParse(INPUT.AsSpan(), CultureInfo.InvariantCulture, out var c);
		parsed.Should().BeTrue("input should be a valid hex color RRGGBBAA");

#pragma warning disable CS0618 // Dedicated compatibility-wrapper assertion.
		TEXT.ColorHex(INPUT).Should().Be(TEXT.Color(c));
#pragma warning restore CS0618
	}

	[Fact]
	public void NamedColorCompatibilityWrappers_WhenCalled_ShouldPreserveMarkup()
	{
#pragma warning disable CS0618 // Dedicated compatibility-wrapper assertions.
		"hello".Red().Should().Be("<color=#FF0000FF>hello</color>");
		"hello".NeonPurple().Should().Be("<color=#BC13FEFF>hello</color>");
#pragma warning restore CS0618
	}

	[Fact]
	public void ColorConvenienceMethods_WhenInspected_ShouldHaveExactObsoleteMessage()
	{
		var colorHex = typeof(StringExtensions).GetMethod(
			nameof(StringExtensions.ColorHex),
			BindingFlags.Public | BindingFlags.Static,
			[typeof(string), typeof(string)]
		);
		colorHex.Should().NotBeNull("ColorHex remains available during the compatibility window");
		var colorHexAttribute = colorHex!.GetCustomAttribute<ObsoleteAttribute>();
		colorHexAttribute.Should().NotBeNull();
		colorHexAttribute!.Message.Should().Be(ObsoleteMessage);

		foreach (string methodName in NamedColorMethodNames)
		{
			var method = typeof(StringExtensions).GetMethod(
				methodName,
				BindingFlags.Public | BindingFlags.Static,
				[typeof(string)]
			);

			method.Should().NotBeNull($"{methodName} remains available during the compatibility window");
			var attribute = method!.GetCustomAttribute<ObsoleteAttribute>();
			attribute.Should().NotBeNull();
			attribute!.Message.Should().Be(ObsoleteMessage);
		}
	}

	[Fact]
	public void ColorOverloads_WhenInspected_ShouldRemainCanonical()
	{
		var methods = typeof(StringExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Where(method => method.Name == nameof(StringExtensions.Color));

		methods.Should().NotBeEmpty().And.OnlyContain(method => method.GetCustomAttribute<ObsoleteAttribute>() == null);
	}
}
