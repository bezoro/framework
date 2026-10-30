using System;
using System.Diagnostics;
using System.IO;
using FluentAssertions;
using Xunit;

namespace Bezoro.Build.Tests;

public sealed class UnityPackageScriptTests
{
    [Fact]
    public void StagingIncludesDependenciesAndExcludesHostAssemblies()
    {
        using var fixture = new StagingFixture();
        fixture.AddProject("Bezoro.One", ("System.Collections.Immutable.dll", "immutable"),
            ("System.Memory.dll", "host memory"));
        fixture.AddProject("Bezoro.Two", ("System.Collections.Immutable.dll", "immutable"),
            ("System.Text.Json.dll", "json"));

        var result = fixture.Run();

        result.ExitCode.Should().Be(0, result.Error);
        File.ReadAllText(Path.Combine(fixture.Path, "artifacts/package/Runtime/System.Collections.Immutable.dll"))
            .Should().Be("immutable");
        File.Exists(Path.Combine(fixture.Path, "artifacts/package/Runtime/System.Text.Json.dll")).Should().BeTrue();
        File.Exists(Path.Combine(fixture.Path, "artifacts/package/Runtime/System.Memory.dll")).Should().BeFalse();
        File.Exists(Path.Combine(fixture.Path, "artifacts/package/Runtime/Bezoro.One.README.md")).Should().BeTrue();
        File.ReadAllText(Path.Combine(fixture.Path, "artifacts/package/package.json")).Should().Contain("1.2.3");
    }

    [Fact]
    public void StagingResolvesFlattenedReadmeLinksToSourceDocumentation()
    {
        using var fixture = new StagingFixture();
        fixture.AddProject("Bezoro.One");
        File.WriteAllText(Path.Combine(fixture.Path, "src/Bezoro.One/README.md"),
            "[Details](Details/README.md#usage)\n[Notes](<Details/My Notes.md#topic> \"Notes\")\n" +
            "[Local](#local)\n[External](https://example.com/guide)\n");

        var result = fixture.Run();

        result.ExitCode.Should().Be(0, result.Error);
        string readme = File.ReadAllText(Path.Combine(fixture.Path, "artifacts/package/Runtime/Bezoro.One.README.md"));
        readme.Should().Contain("https://github.com/bezoro/framework/blob/main/src/Bezoro.One/Details/README.md#usage");
        readme.Should().Contain("https://github.com/bezoro/framework/blob/main/src/Bezoro.One/Details/My%20Notes.md#topic \"Notes\"");
        readme.Should().Contain("[Local](#local)").And.Contain("[External](https://example.com/guide)");
    }

    [Fact]
    public void StagingRejectsConflictingDependencyVersions()
    {
        using var fixture = new StagingFixture();
        fixture.AddProject("Bezoro.One", ("System.Text.Json.dll", "version one"));
        fixture.AddProject("Bezoro.Two", ("System.Text.Json.dll", "version two"));

        var result = fixture.Run();

        result.ExitCode.Should().NotBe(0);
        result.Error.Should().Contain("Conflicting runtime dependency");
    }

    [Fact]
    public void StagingRejectsMissingProjectAssembly()
    {
        using var fixture = new StagingFixture();
        fixture.AddProject("Bezoro.One");
        File.Delete(Path.Combine(fixture.Path, "src/Bezoro.One/bin/Release/netstandard2.1/Bezoro.One.dll"));

        var result = fixture.Run();

        result.ExitCode.Should().NotBe(0);
        result.Error.Should().Contain("Missing runtime assembly");
    }

    [Theory]
    [InlineData(".")]
    [InlineData("src")]
    [InlineData("unity-package")]
    [InlineData("../outside")]
    public void StagingRejectsSourceAndAncestorOutputPaths(string output)
    {
        using var fixture = new StagingFixture();

        var result = fixture.Run(output);

        result.ExitCode.Should().NotBe(0);
        File.Exists(Path.Combine(fixture.Path, "unity-package/package.json")).Should().BeTrue();
        result.Error.Should().Contain("OutputPath must be a staging directory");
    }

    private sealed class StagingFixture : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "bezoro-staging", Guid.NewGuid().ToString("N"));

        public StagingFixture()
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root is not null && !File.Exists(System.IO.Path.Combine(root.FullName, "bezoro.framework.sln")))
                root = root.Parent;
            if (root is null) throw new InvalidOperationException("Repository not found.");
            Directory.CreateDirectory(System.IO.Path.Combine(Path, "scripts"));
            Directory.CreateDirectory(System.IO.Path.Combine(Path, "src"));
            Directory.CreateDirectory(System.IO.Path.Combine(Path, "unity-package"));
            foreach (var script in new[] { "Build-UnityPackage.ps1", "Get-PackageVersion.ps1" })
                File.Copy(System.IO.Path.Combine(root.FullName, "scripts", script), System.IO.Path.Combine(Path, "scripts", script));
            File.WriteAllText(System.IO.Path.Combine(Path, "unity-package/package.json"), "{\"name\":\"test\",\"version\":\"0.0.0\"}");
        }

        public void AddProject(string name, params (string Name, string Content)[] dependencies)
        {
            var project = System.IO.Path.Combine(Path, "src", name);
            var output = System.IO.Path.Combine(project, "bin/Release/netstandard2.1");
            Directory.CreateDirectory(output);
            File.WriteAllText(System.IO.Path.Combine(project, name + ".csproj"), "<Project />");
            File.WriteAllText(System.IO.Path.Combine(project, "README.md"), name);
            File.WriteAllText(System.IO.Path.Combine(output, name + ".dll"), name);
            foreach (var dependency in dependencies)
                File.WriteAllText(System.IO.Path.Combine(output, dependency.Name), dependency.Content);
        }

        public (int ExitCode, string Error) Run(string output = "artifacts/package")
        {
            using var process = new Process { StartInfo = new ProcessStartInfo("pwsh")
            {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
            }};
            foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-File",
                System.IO.Path.Combine(Path, "scripts/Build-UnityPackage.ps1"), "-PackageVersion", "1.2.3", "-OutputPath", output })
                process.StartInfo.ArgumentList.Add(argument);
            process.Start();
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            return (process.ExitCode, stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult());
        }

        public void Dispose() => Directory.Delete(Path, true);
    }
}
