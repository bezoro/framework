using System;
using Bezoro.Core.Extensions;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Extensions;

[TestSubject(typeof(CharExtensions))]
public class CharExtensionsTests
{
	[Fact]
	public void IsEmpty_WhenNotWhiteSpace_ReturnsFalse()
	{
		'a'.IsEmpty().Should().BeFalse();
		'1'.IsEmpty().Should().BeFalse();
		'#'.IsEmpty().Should().BeFalse();
	}

	[Fact]
	public void IsEmpty_WhenWhiteSpace_ReturnsTrue()
	{
		' '.IsEmpty().Should().BeTrue();
		'\t'.IsEmpty().Should().BeTrue();
		'\n'.IsEmpty().Should().BeTrue();
	}

	[Fact]
	public void ThrowIfEmpty_WhenNotWhiteSpace_ReturnsChar()
	{
		'a'.ThrowIfEmpty().Should().Be('a');
		'1'.ThrowIfEmpty().Should().Be('1');
		'#'.ThrowIfEmpty().Should().Be('#');
	}

	[Fact]
	public void ThrowIfEmpty_WhenWhiteSpace_ThrowsArgumentException()
	{
		var act1 = () => ' '.ThrowIfEmpty();
		var act2 = () => '\t'.ThrowIfEmpty();
		var act3 = () => '\n'.ThrowIfEmpty();

		act1.Should().Throw<ArgumentException>();
		act2.Should().Throw<ArgumentException>();
		act3.Should().Throw<ArgumentException>();
	}

	[Fact]
	public void ThrowIfLetter_WhenLetter_ThrowsArgumentException()
	{
		var act1 = () => 'a'.ThrowIfLetter();
		var act2 = () => 'Z'.ThrowIfLetter();

		act1.Should().Throw<ArgumentException>();
		act2.Should().Throw<ArgumentException>();
	}

	[Fact]
	public void ThrowIfLetter_WhenNotLetter_ReturnsChar()
	{
		'1'.ThrowIfLetter().Should().Be('1');
		'#'.ThrowIfLetter().Should().Be('#');
	}

	[Fact]
	public void ThrowIfLowerCase_WhenLowerCase_ThrowsArgumentException()
	{
		var act = () => 'a'.ThrowIfLowerCase();

		act.Should().Throw<ArgumentException>();
	}

	[Fact]
	public void ThrowIfLowerCase_WhenNotLowerCase_ReturnsChar()
	{
		'A'.ThrowIfLowerCase().Should().Be('A');
		'1'.ThrowIfLowerCase().Should().Be('1');
		'#'.ThrowIfLowerCase().Should().Be('#');
	}

	[Fact]
	public void ThrowIfNumber_WhenNotNumber_ReturnsChar()
	{
		'a'.ThrowIfNumber().Should().Be('a');
		'#'.ThrowIfNumber().Should().Be('#');
	}

	[Fact]
	public void ThrowIfNumber_WhenNumber_ThrowsArgumentException()
	{
		var act = () => '1'.ThrowIfNumber();

		act.Should().Throw<ArgumentException>();
	}

	[Fact]
	public void ThrowIfSymbol_WhenNotSymbol_ReturnsChar()
	{
		'a'.ThrowIfSymbol().Should().Be('a');
		'1'.ThrowIfSymbol().Should().Be('1');
	}

	[Fact]
	public void ThrowIfSymbol_WhenSymbol_ThrowsArgumentException()
	{
		var act = () => '©'.ThrowIfSymbol();

		act.Should().Throw<ArgumentException>();
	}

	[Fact]
	public void ThrowIfUpperCase_WhenNotUpperCase_ReturnsChar()
	{
		'a'.ThrowIfUpperCase().Should().Be('a');
		'1'.ThrowIfUpperCase().Should().Be('1');
		'#'.ThrowIfUpperCase().Should().Be('#');
	}

	[Fact]
	public void ThrowIfUpperCase_WhenUpperCase_ThrowsArgumentException()
	{
		var act = () => 'A'.ThrowIfUpperCase();

		act.Should().Throw<ArgumentException>();
	}
}
