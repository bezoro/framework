using System;
using System.IO;
using FluentAssertions;
using Xunit;

namespace Bezoro.GameSystems.Tests.ProjectConfiguration;

public sealed class UnityPackageBuildScriptTests
{
	[Fact]
	public void BuildScript_WhenStagingUnityPackage_ShouldPublishDllsIntoRuntimePluginsWithUnityMetaFiles()
	{
		var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
		var scriptPath = Path.Combine(repositoryRoot, "scripts", "Build-UnityPackage.ps1");

		var scriptContents = File.ReadAllText(scriptPath);

		scriptContents.Should().Contain("Get-DeterministicUnityGuid");
		scriptContents.Should().Contain("Get-FolderMetaContent");
		scriptContents.Should().Contain("Get-PluginMetaContent");
		scriptContents.Should().Contain("$pluginsRoot.meta");
		scriptContents.Should().Contain("\"$assemblyName.dll\"");
		scriptContents.Should().NotContain("\"$assemblyName.pdb\"");
		scriptContents.Should().NotContain("\"$assemblyName.xml\"");
		scriptContents.Should().NotContain("      : Any");
	}
}
