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
		var result = GeneratedSystemMetadataResolver.TryGet(typeof(ResolverDerivedSystem), out var metadata);

		result.Should().BeTrue();
		metadata.SystemType.Should().Be(typeof(ResolverDerivedSystem));
		metadata.Reads.Should().ContainSingle(t => t == typeof(ResolverReadComponent));
		metadata.Writes.Should().ContainSingle(t => t == typeof(ResolverWriteComponent));
		metadata.IsExclusive.Should().BeTrue();
	}

	[Fact]
	public void TryGet_WhenSystemIsStruct_ShouldReturnGeneratedMetadata()
	{
		var result = GeneratedSystemMetadataResolver.TryGet(typeof(ResolverStructSystem), out var metadata);

		result.Should().BeTrue();
		metadata.SystemType.Should().Be(typeof(ResolverStructSystem));
		metadata.Reads.Should().ContainSingle(t => t == typeof(ResolverReadComponent));
		metadata.Writes.Should().ContainSingle(t => t == typeof(ResolverWriteComponent));
		metadata.IsExclusive.Should().BeTrue();
	}

	[Fact]
	public void TryGet_WhenSystemIsNestedType_ShouldReturnGeneratedMetadata()
	{
		var result = GeneratedSystemMetadataResolver.TryGet(typeof(ResolverNestedSystems.NestedSystem), out var metadata);

		result.Should().BeTrue();
		metadata.SystemType.Should().Be(typeof(ResolverNestedSystems.NestedSystem));
		metadata.Reads.Should().ContainSingle(t => t == typeof(ResolverReadComponent));
		metadata.Writes.Should().ContainSingle(t => t == typeof(ResolverWriteComponent));
		metadata.IsExclusive.Should().BeFalse();
	}
}

[Reads<ResolverReadComponent>]
[Exclusive]
internal abstract class ResolverBaseSystem : ISystem
{
	public abstract void Update(IWorld world, in SystemContext context);
}

[Writes<ResolverWriteComponent>]
internal sealed class ResolverDerivedSystem : ResolverBaseSystem
{
	public override void Update(IWorld world, in SystemContext context)
	{
	}
}

internal struct ResolverReadComponent : IComponent;

internal struct ResolverWriteComponent : IComponent;

[Reads<ResolverReadComponent>]
[Writes<ResolverWriteComponent>]
[Exclusive]
internal struct ResolverStructSystem : ISystem
{
	public void Update(IWorld world, in SystemContext context)
	{
	}
}

internal static class ResolverNestedSystems
{
	[Reads<ResolverReadComponent>]
	[Writes<ResolverWriteComponent>]
	internal sealed class NestedSystem : ISystem
	{
		public void Update(IWorld world, in SystemContext context)
		{
		}
	}
}
