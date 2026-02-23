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
	public void IsEmptyWhenNotWhiteSpace_WhenCalled_ShouldReturnFalse()
	{
		'a'.IsEmpty().Should().BeFalse();
		'1'.IsEmpty().Should().BeFalse();
		'#'.IsEmpty().Should().BeFalse();
	}

	[Fact]
	public void IsEmptyWhenWhiteSpace_WhenCalled_ShouldReturnTrue()
	{
		' '.IsEmpty().Should().BeTrue();
		'\t'.IsEmpty().Should().BeTrue();
		'\n'.IsEmpty().Should().BeTrue();
	}

	[Fact]
	public void ThrowIfEmptyWhenNotWhiteSpace_WhenCalled_ShouldReturnChar()
	{
		'a'.ThrowIfEmpty().Should().Be('a');
		'1'.ThrowIfEmpty().Should().Be('1');
		'#'.ThrowIfEmpty().Should().Be('#');
	}

	[Fact]
	public void ThrowIfEmptyWhenWhiteSpace_WhenCalled_ShouldThrowArgumentException()
	{
		var act1 = () => ' '.ThrowIfEmpty();
		var act2 = () => '\t'.ThrowIfEmpty();
		var act3 = () => '\n'.ThrowIfEmpty();

		act1.Should().Throw<ArgumentException>();
		act2.Should().Throw<ArgumentException>();
		act3.Should().Throw<ArgumentException>();
	}

	[Fact]
	public void ThrowIfLetterWhenLetter_WhenCalled_ShouldThrowArgumentException()
	{
		var act1 = () => 'a'.ThrowIfLetter();
		var act2 = () => 'Z'.ThrowIfLetter();

		act1.Should().Throw<ArgumentException>();
		act2.Should().Throw<ArgumentException>();
	}

	[Fact]
	public void ThrowIfLetterWhenNotLetter_WhenCalled_ShouldReturnChar()
	{
		'1'.ThrowIfLetter().Should().Be('1');
		'#'.ThrowIfLetter().Should().Be('#');
	}

	[Fact]
	public void ThrowIfLowerCaseWhenLowerCase_WhenCalled_ShouldThrowArgumentException()
	{
		var act = () => 'a'.ThrowIfLowerCase();

		act.Should().Throw<ArgumentException>();
	}

	[Fact]
	public void ThrowIfLowerCaseWhenNotLowerCase_WhenCalled_ShouldReturnChar()
	{
		'A'.ThrowIfLowerCase().Should().Be('A');
		'1'.ThrowIfLowerCase().Should().Be('1');
		'#'.ThrowIfLowerCase().Should().Be('#');
	}

	[Fact]
	public void ThrowIfNumberWhenNotNumber_WhenCalled_ShouldReturnChar()
	{
		'a'.ThrowIfNumber().Should().Be('a');
		'#'.ThrowIfNumber().Should().Be('#');
	}

	[Fact]
	public void ThrowIfNumberWhenNumber_WhenCalled_ShouldThrowArgumentException()
	{
		var act = () => '1'.ThrowIfNumber();

		act.Should().Throw<ArgumentException>();
	}

	[Fact]
	public void ThrowIfSymbolWhenNotSymbol_WhenCalled_ShouldReturnChar()
	{
		'a'.ThrowIfSymbol().Should().Be('a');
		'1'.ThrowIfSymbol().Should().Be('1');
	}

	[Fact]
	public void ThrowIfSymbolWhenSymbol_WhenCalled_ShouldThrowArgumentException()
	{
		var act = () => '©'.ThrowIfSymbol();

		act.Should().Throw<ArgumentException>();
	}

	[Fact]
	public void ThrowIfUpperCaseWhenNotUpperCase_WhenCalled_ShouldReturnChar()
	{
		'a'.ThrowIfUpperCase().Should().Be('a');
		'1'.ThrowIfUpperCase().Should().Be('1');
		'#'.ThrowIfUpperCase().Should().Be('#');
	}

	[Fact]
	public void ThrowIfUpperCaseWhenUpperCase_WhenCalled_ShouldThrowArgumentException()
	{
		var act = () => 'A'.ThrowIfUpperCase();

		act.Should().Throw<ArgumentException>();
	}
}
