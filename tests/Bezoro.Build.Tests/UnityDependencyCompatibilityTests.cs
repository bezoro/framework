using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace Bezoro.Build.Tests;

public sealed class UnityDependencyCompatibilityTests
{
	[Fact]
	public void SystemCollectionsImmutableVersion_WhenBuildingFramework_ShouldMatchUnitySupportedBcl()
	{
		var propsPath = Path.Combine(FindRepositoryRoot(), "Directory.Packages.props");
		var document = XDocument.Load(propsPath);
		var version = document.Descendants("PackageVersion")
			.Single(element => (string?)element.Attribute("Include") == "System.Collections.Immutable")
			.Attribute("Version")?.Value;

		version.Should().Be("8.0.0");
	}

	private static string FindRepositoryRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory is not null)
		{
			if (File.Exists(Path.Combine(directory.FullName, "bezoro.framework.sln")))
			{
				return directory.FullName;
			}

			directory = directory.Parent;
		}

		throw new InvalidOperationException("Could not find repository root.");
	}
}
