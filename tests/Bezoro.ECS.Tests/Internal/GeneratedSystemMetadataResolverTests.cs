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

	[Fact]
	public void TryGet_WhenSystemUsesForEachQuery_ShouldInferReadAndWriteSets()
	{
		var  resolver = new GeneratedSystemMetadataResolver();
		bool result   = resolver.TryGet(typeof(ResolverInferredForEachSystem), out var metadata);

		result.Should().BeTrue();
		metadata.Reads.Should().Contain(typeof(ResolverInferredVelocity));
		metadata.Writes.Should().Contain(typeof(ResolverInferredPosition));
	}

	[Fact]
	public void TryGet_WhenSystemUsesForEachRwQuery_ShouldInferBothAsWrites()
	{
		var  resolver = new GeneratedSystemMetadataResolver();
		bool result   = resolver.TryGet(typeof(ResolverInferredForEachRwSystem), out var metadata);

		result.Should().BeTrue();
		metadata.Reads.Should().NotContain(typeof(ResolverInferredPosition));
		metadata.Reads.Should().NotContain(typeof(ResolverInferredVelocity));
		metadata.Writes.Should().Contain(typeof(ResolverInferredPosition));
		metadata.Writes.Should().Contain(typeof(ResolverInferredVelocity));
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
	public override void Update(IWorld world, in SystemContext context) { }
}

internal sealed class ResolverInferredForEachRwSystem : ISystem
{
	public void Update(IWorld world, in SystemContext context)
	{
		world.Query().All<ResolverInferredPosition>().All<ResolverInferredVelocity>()
			 .ForEachRW((ref ResolverInferredPosition position, ref ResolverInferredVelocity velocity) => { });
	}
}

internal sealed class ResolverInferredForEachSystem : ISystem
{
	public void Update(IWorld world, in SystemContext context)
	{
		world.Query().All<ResolverInferredPosition>().All<ResolverInferredVelocity>()
			 .ForEach((ref ResolverInferredPosition position, in ResolverInferredVelocity velocity) => { });
	}
}

internal static class ResolverNestedSystems
{
	[Reads<ResolverReadComponent>]
	[Writes<ResolverWriteComponent>]
	internal sealed class NestedSystem : ISystem
	{
		public void Update(IWorld world, in SystemContext context) { }
	}
}

internal struct ResolverInferredPosition;

internal struct ResolverInferredVelocity;

internal struct ResolverReadComponent;

[Reads<ResolverReadComponent>]
[Writes<ResolverWriteComponent>]
[Exclusive]
internal struct ResolverStructSystem : ISystem
{
	public void Update(IWorld world, in SystemContext context) { }
}

internal struct ResolverWriteComponent;
