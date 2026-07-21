using System;
using System.Reflection;
using Bezoro.Core.Extensions;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;
using Color = Bezoro.Core.Types.Color;

namespace Bezoro.Core.Tests.Extensions;

[TestSubject(typeof(EnumExtensions))]
public class EnumExtensionsTests
{
	private const string ObsoleteMessage = "Use value.ToString() with the corresponding string extension instead.";

	private static readonly string[] CompatibilityMethodNames =
	[
		"Black", "Blue", "Bold", "Brown", "Capitalize", "Cyan", "Gray", "Green", "Italic", "Lowercase", "Magenta",
		"Orange", "Purple", "Red", "Size", "Strikethrough", "Underline", "Uppercase", "White", "Yellow"
	];

	[Fact]
	public void Color_WhenCalledWithCanonicalInputs_ShouldPreserveMarkup()
	{
		TestEnum.Value.Color("red").Should().Be("<color=red>Value</color>");
		TestEnum.Value.Color(new Color(1f, 0f, 0f, 1f)).Should().Be("<color=#FF0000FF>Value</color>");
	}

	[Fact]
	public void IsDefined_WhenCalled_ShouldReturnFalse_WhenValueIsNotDefined()
	{
		// Arrange
		var value = (TestEnum)999;

		// Act
		bool result = value.IsDefined();

		// Assert
		result.Should().BeFalse();
	}

	[Fact]
	public void IsDefined_WhenCalled_ShouldReturnTrue_WhenValueIsDefined()
	{
		// Arrange
		var value = TestEnum.First;

		// Act
		bool result = value.IsDefined();

		// Assert
		result.Should().BeTrue();
	}

	[Fact]
	public void FormattingCompatibilityWrappers_WhenCalled_ShouldPreserveMarkup()
	{
#pragma warning disable CS0618 // Dedicated compatibility-wrapper assertions.
		TestEnum.Value.Black().Should().Be("<color=#000000FF>Value</color>");
		TestEnum.Value.Blue().Should().Be("<color=#0000FFFF>Value</color>");
		TestEnum.Value.Bold().Should().Be("<b>Value</b>");
		TestEnum.Value.Brown().Should().Be("<color=#A6522BFF>Value</color>");
		TestEnum.Value.Capitalize().Should().Be("Value");
		TestEnum.Value.Cyan().Should().Be("<color=#00FFFFFF>Value</color>");
		TestEnum.Value.Gray().Should().Be("<color=#808080FF>Value</color>");
		TestEnum.Value.Green().Should().Be("<color=#00FF00FF>Value</color>");
		TestEnum.Value.Italic().Should().Be("<i>Value</i>");
		TestEnum.Value.Lowercase().Should().Be("value");
		TestEnum.Value.Magenta().Should().Be("<color=#FF00FFFF>Value</color>");
		TestEnum.Value.Orange().Should().Be("<color=#FFA600FF>Value</color>");
		TestEnum.Value.Purple().Should().Be("<color=#A121F0FF>Value</color>");
		TestEnum.Value.Red().Should().Be("<color=#FF0000FF>Value</color>");
		TestEnum.Value.Size(24).Should().Be("<size=24>Value</size>");
		TestEnum.Value.Strikethrough().Should().Be("<s>Value</s>");
		TestEnum.Value.Underline().Should().Be("<u>Value</u>");
		TestEnum.Value.Uppercase().Should().Be("VALUE");
		TestEnum.Value.White().Should().Be("<color=#FFFFFFFF>Value</color>");
		TestEnum.Value.Yellow().Should().Be("<color=#FFFF00FF>Value</color>");
#pragma warning restore CS0618
	}

	[Fact]
	public void FormattingCompatibilityMethods_WhenInspected_ShouldHaveExactObsoleteMessage()
	{
		foreach (string methodName in CompatibilityMethodNames)
		{
			var method = typeof(EnumExtensions).GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);

			method.Should().NotBeNull($"{methodName} remains available during the compatibility window");
			var attribute = method!.GetCustomAttribute<ObsoleteAttribute>();
			attribute.Should().NotBeNull();
			attribute!.Message.Should().Be(ObsoleteMessage);
		}
	}

	[Fact]
	public void ColorOverloads_WhenInspected_ShouldRemainCanonical()
	{
		foreach (var parameterType in new[] { typeof(string), typeof(Color) })
		{
			var method = typeof(EnumExtensions).GetMethod(
				nameof(EnumExtensions.Color),
				BindingFlags.Public | BindingFlags.Static,
				[typeof(Enum), parameterType]
			);

			method.Should().NotBeNull();
			method!.GetCustomAttribute<ObsoleteAttribute>().Should().BeNull();
		}
	}
}

internal enum TestEnum
{
	Value,
	First,
	Second
}
