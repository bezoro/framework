using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Attributes;
using Bezoro.ECS.Internal;
using Bezoro.ECS.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.ECS.Tests.Internal;

[TestSubject(typeof(GeneratedSystemMetadataResolver))]
public class GeneratedSystemMetadataResolverTests
{
	[Fact]
	public void TryGet_WhenGeneratedMetadataExists_ShouldReturnSystemMetadata()
	{
		var  resolver = new GeneratedSystemMetadataResolver();
		bool result   = resolver.TryGet(typeof(ResolverDerivedSystem), out var metadata);

		result.Should().BeTrue();
		metadata.SystemType.Should().Be(typeof(ResolverDerivedSystem));
		metadata.Reads.Should().ContainSingle(t => t == typeof(ResolverReadComponent));
		metadata.Writes.Should().ContainSingle(t => t == typeof(ResolverWriteComponent));
		metadata.IsExclusive.Should().BeTrue();
	}

	[Fact]
	public void TryGet_WhenSystemHasNoAttributes_ShouldReturnEmptyMetadata()
	{
		var  resolver = new GeneratedSystemMetadataResolver();
		bool result   = resolver.TryGet(typeof(ResolverUnknownSystem), out var metadata);

		result.Should().BeTrue();
		metadata.SystemType.Should().Be(typeof(ResolverUnknownSystem));
		metadata.Reads.Should().BeEmpty();
		metadata.Writes.Should().BeEmpty();
		metadata.IsExclusive.Should().BeFalse();
	}

	[Fact]
	public void TryGet_WhenSystemIsNestedType_ShouldReturnGeneratedMetadata()
	{
		var  resolver = new GeneratedSystemMetadataResolver();
		bool result   = resolver.TryGet(typeof(ResolverNestedSystems.NestedSystem), out var metadata);

		result.Should().BeTrue();
		metadata.SystemType.Should().Be(typeof(ResolverNestedSystems.NestedSystem));
		metadata.Reads.Should().ContainSingle(t => t == typeof(ResolverReadComponent));
		metadata.Writes.Should().ContainSingle(t => t == typeof(ResolverWriteComponent));
		metadata.IsExclusive.Should().BeFalse();
	}

	[Fact]
	public void TryGet_WhenSystemIsStruct_ShouldReturnGeneratedMetadata()
	{
		var  resolver = new GeneratedSystemMetadataResolver();
		bool result   = resolver.TryGet(typeof(ResolverStructSystem), out var metadata);

		result.Should().BeTrue();
		metadata.SystemType.Should().Be(typeof(ResolverStructSystem));
		metadata.Reads.Should().ContainSingle(t => t == typeof(ResolverReadComponent));
		metadata.Writes.Should().ContainSingle(t => t == typeof(ResolverWriteComponent));
		metadata.IsExclusive.Should().BeTrue();
	}
}

[Reads(typeof(ResolverReadComponent))]
[Exclusive]
internal abstract class ResolverBaseSystem : ISystem
{
	public abstract void Update(in SystemContext context);
}

[Writes(typeof(ResolverWriteComponent))]
internal sealed class ResolverDerivedSystem : ResolverBaseSystem
{
	public override void Update(in SystemContext context) { }
}

internal static class ResolverNestedSystems
{
	[Reads(typeof(ResolverReadComponent))]
	[Writes(typeof(ResolverWriteComponent))]
	internal sealed class NestedSystem : ISystem
	{
		public void Update(in SystemContext context) { }
	}
}

internal struct ResolverReadComponent;

[Reads(typeof(ResolverReadComponent))]
[Writes(typeof(ResolverWriteComponent))]
[Exclusive]
internal struct ResolverStructSystem : ISystem
{
	public void Update(in SystemContext context) { }
}

internal struct ResolverUnknownSystem : ISystem
{
	public void Update(in SystemContext context) { }
}

internal struct ResolverWriteComponent;
