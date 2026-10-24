using System;
using System.Collections.Generic;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;
using Bezoro.GameSystems.InputSystem.Abstractions;
using Bezoro.GameSystems.InputSystem.Extensions;
using Bezoro.GameSystems.InputSystem.Types;
using Bezoro.GameSystems.MovementSystem.Types;
using Xunit;
using MovementSystemType = Bezoro.GameSystems.MovementSystem.Services.MovementSystem;

namespace Bezoro.GameSystems.Tests.InputSystem;

// Root-owned acceptance, specified before implementation from the public contract.
// Uses public ingress, ECS lifecycle and component outcomes, never private helpers.
public class NumericSafetyAcceptanceTests
{
    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void EveryAxisRejectsNonfiniteValuesWithItsOwnParameter(float invalid)
    {
        var x = Assert.Throws<ArgumentOutOfRangeException>(() => new InputCommand(1, invalid, 0, 0, 1));
        var y = Assert.Throws<ArgumentOutOfRangeException>(() => new InputCommand(1, 0, invalid, 0, 1));
        var z = Assert.Throws<ArgumentOutOfRangeException>(() => new InputCommand(1, 0, 0, invalid, 1));
        Assert.Equal("moveX", x.ParamName);
        Assert.Equal("moveY", y.ParamName);
        Assert.Equal("moveZ", z.ParamName);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void RejectedIngressCannotConsumeSequenceAndValidInputStillMoves(float invalid)
    {
        var world = CreateWorld(out var queue);
        var entity = Spawn(world, 1, 2, 0.5f);
        queue.Enqueue(1, 1, 0, 0, 1);
        world.FixedTick(0.125f);
        AssertPosition(world, entity, 0.25f, 0, 0);

        var failure = Assert.Throws<ArgumentOutOfRangeException>(() => queue.Enqueue(1, 0, invalid, 0, ulong.MaxValue));
        Assert.Equal("moveY", failure.ParamName);
        queue.Enqueue(1, -1, 1, 0.5f, 2);
        world.FixedTick(0.125f);
        AssertPosition(world, entity, 0, 0.25f, 0.125f);
        AssertVelocity(world, entity, -2, 2, 1);
        AssertIntent(world, entity, -1, 1, 0.5f);

        queue.Enqueue(1, 1, -1, -1, 2);
        queue.Enqueue(1, 1, -1, -1, 1);
        world.FixedTick(0.125f);
        AssertPosition(world, entity, -0.25f, 0.5f, 0.25f);
        AssertVelocity(world, entity, -2, 2, 1);
    }

    public static IEnumerable<object[]> InvalidSettings()
    {
        yield return new object[] { float.NaN, 0.5f };
        yield return new object[] { float.PositiveInfinity, 0.5f };
        yield return new object[] { float.NegativeInfinity, 0.5f };
        yield return new object[] { 2f, float.NaN };
        yield return new object[] { 2f, float.PositiveInfinity };
        yield return new object[] { 2f, float.NegativeInfinity };
        yield return new object[] { 2f, -0.125f };
    }

    [Theory]
    [MemberData(nameof(InvalidSettings))]
    public void InvalidMutableSettingsStopOnlyTheirEntityAndCanRecover(float speed, float hold)
    {
        var world = CreateWorld(out var queue);
        var affected = Spawn(world, 1, 2, 0.5f);
        var healthy = Spawn(world, 2, 2, 0.5f);
        // Distinct archetype exercises query integration without depending on its internals.
        world.Add(healthy, new ExtraMarker());
        queue.Enqueue(1, 1, 1, 1, 1);
        queue.Enqueue(2, 1, 1, 1, 1);
        world.FixedTick(0.125f);
        AssertPosition(world, affected, 0.25f, 0.25f, 0.25f);
        AssertPosition(world, healthy, 0.25f, 0.25f, 0.25f);

        ref var settings = ref world.Write<MovementInputSettings>(affected);
        settings.Speed = speed;
        settings.HoldDurationSeconds = hold;
        world.FixedTick(0.125f);
        AssertPosition(world, affected, 0.25f, 0.25f, 0.25f);
        AssertVelocity(world, affected, 0, 0, 0);
        AssertIntent(world, affected, 0, 0, 0);
        AssertPosition(world, healthy, 0.5f, 0.5f, 0.5f);

        ref var restored = ref world.Write<MovementInputSettings>(affected);
        restored.Speed = 2;
        restored.HoldDurationSeconds = 0.5f;
        world.FixedTick(0.125f);
        AssertPosition(world, affected, 0.5f, 0.5f, 0.5f);
        AssertVelocity(world, affected, 2, 2, 2);
        AssertIntent(world, affected, 1, 1, 1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void FiniteMultiplicationOverflowNeutralizesTheWholeVectorThenRecovers(int axis)
    {
        var world = CreateWorld(out var queue);
        var entity = Spawn(world, 1, 2, 0.5f);
        float x = axis == 0 ? 2 : 1;
        float y = axis == 1 ? 2 : 1;
        float z = axis == 2 ? 2 : 1;
        queue.Enqueue(1, x, y, z, 1);
        world.FixedTick(0.125f);
        AssertPosition(world, entity, x * 0.25f, y * 0.25f, z * 0.25f);
        AssertVelocity(world, entity, x * 2, y * 2, z * 2);
        AssertIntent(world, entity, x, y, z);

        ref var overflowSettings = ref world.Write<MovementInputSettings>(entity);
        overflowSettings.Speed = float.MaxValue;
        world.FixedTick(0.125f);
        AssertPosition(world, entity, x * 0.25f, y * 0.25f, z * 0.25f);
        AssertVelocity(world, entity, 0, 0, 0);
        AssertIntent(world, entity, 0, 0, 0);

        ref var settings = ref world.Write<MovementInputSettings>(entity);
        settings.Speed = -2;
        world.FixedTick(0.125f);
        AssertPosition(world, entity, 0, 0, 0);
        AssertVelocity(world, entity, -x * 2, -y * 2, -z * 2);
        AssertIntent(world, entity, x, y, z);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void FinitePositionOverflowIsAtomicAndLaterValidMovementResumes(int axis)
    {
        var world = CreateWorld(out var queue);
        var entity = Spawn(world, 1, 1, 0.5f);
        var start = new Position { X = axis == 0 ? float.MaxValue : 4, Y = axis == 1 ? float.MaxValue : 8, Z = axis == 2 ? float.MaxValue : 16 };
        world.Write<Position>(entity) = start;
        queue.Enqueue(1, axis == 0 ? float.MaxValue : 1, axis == 1 ? float.MaxValue : 1, axis == 2 ? float.MaxValue : 1, 1);
        world.FixedTick(0.125f);
        AssertPosition(world, entity, start.X, start.Y, start.Z);
        // A representable negative step moves the boundary coordinate inward.
        float nextX = axis == 0 ? -float.MaxValue : -1;
        float nextY = axis == 1 ? -float.MaxValue : -1;
        float nextZ = axis == 2 ? -float.MaxValue : -1;
        queue.Enqueue(1, nextX, nextY, nextZ, 2);
        world.FixedTick(0.125f);
        AssertPosition(world, entity, start.X + nextX * 0.125f, start.Y + nextY * 0.125f, start.Z + nextZ * 0.125f);
        AssertVelocity(world, entity, nextX, nextY, nextZ);
    }

    [Fact]
    public void HoldBoundaryIncludesEqualityAndEqualSequenceDoesNotRefreshItsAge()
    {
        var world = CreateWorld(out var queue);
        var entity = Spawn(world, 1, 2, 0.25f);
        queue.Enqueue(1, 1, 0, 0, 1);
        world.FixedTick(0.125f);
        world.FixedTick(0.25f);
        AssertPosition(world, entity, 0.75f, 0, 0);
        AssertVelocity(world, entity, 2, 0, 0);
        queue.Enqueue(1, -1, 0, 0, 1);
        world.FixedTick(0.125f);
        AssertPosition(world, entity, 0.75f, 0, 0);
        AssertVelocity(world, entity, 0, 0, 0);
        AssertIntent(world, entity, 0, 0, 0);
    }

    [Fact]
    public void DefaultCommandZeroSequenceAndZeroHoldRemainUsable()
    {
        var world = CreateWorld(out var queue);
        var entity = Spawn(world, 0, 2, 0);
        IInputCommandSink sink = queue;
        sink.Enqueue(default(InputCommand));
        world.FixedTick(0.125f);
        AssertPosition(world, entity, 0, 0, 0);
        queue.Enqueue(0, 1, 0, 0, 1);
        world.FixedTick(0.125f);
        AssertPosition(world, entity, 0.25f, 0, 0);
        world.FixedTick(0.125f);
        AssertPosition(world, entity, 0.25f, 0, 0);
        AssertVelocity(world, entity, 0, 0, 0);
    }

    [Fact]
    public void FiniteExtremesAndSignedZeroAreAcceptedWithoutClamping()
    {
        var command = new InputCommand(0, float.MaxValue, float.MinValue, -0f, 0);
        Assert.Equal(float.MaxValue, command.MoveX);
        Assert.Equal(float.MinValue, command.MoveY);
        Assert.Equal(0f, command.MoveZ);
        var failure = Assert.Throws<ArgumentOutOfRangeException>(() => new InputCommand(-1, 0, 0, 0, 0));
        Assert.Equal("controlId", failure.ParamName);
    }

    private static World CreateWorld(out InputCommandQueue queue)
    {
        var world = new World();
        queue = world.GetOrCreateInputCommandQueue();
        world.AddMovementInputPipeline();
        world.AddSystem(new MovementSystemType());
        return world;
    }

    private static Entity Spawn(World world, int id, float speed, float hold)
    {
        var entity = world.Spawn(new Position(), new Velocity(), new InputControl { ControlId = id }, new MovementIntent());
        world.Add(entity, new MovementInputSettings { Speed = speed, HoldDurationSeconds = hold });
        return entity;
    }

    private static void AssertPosition(World world, Entity entity, float x, float y, float z)
    {
        var value = world.Read<Position>(entity);
        Assert.Equal(x, value.X);
        Assert.Equal(y, value.Y);
        Assert.Equal(z, value.Z);
    }

    private static void AssertVelocity(World world, Entity entity, float x, float y, float z)
    {
        var value = world.Read<Velocity>(entity);
        Assert.Equal(x, value.X);
        Assert.Equal(y, value.Y);
        Assert.Equal(z, value.Z);
    }

    private static void AssertIntent(World world, Entity entity, float x, float y, float z)
    {
        var value = world.Read<MovementIntent>(entity);
        Assert.Equal(x, value.X);
        Assert.Equal(y, value.Y);
        Assert.Equal(z, value.Z);
    }

    private readonly struct ExtraMarker { }
}
