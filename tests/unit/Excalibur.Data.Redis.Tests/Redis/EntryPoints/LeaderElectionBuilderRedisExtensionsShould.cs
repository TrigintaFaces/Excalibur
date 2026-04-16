// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection.DependencyInjection;
using Excalibur.LeaderElection.Redis;

namespace Excalibur.Data.Tests.Redis.EntryPoints;

/// <summary>
/// Unit tests for <see cref="RedisLeaderElectionBuilderExtensions.UseRedis(ILeaderElectionBuilder, Action{IRedisLeaderElectionBuilder})"/>.
/// Validates null guards, fluent chaining, and configure invocation
/// for the <c>Action&lt;IRedisLeaderElectionBuilder&gt;</c> entry point.
/// </summary>
/// <remarks>
/// The old signature was <c>UseRedis(lockKey)</c> with a string parameter.
/// The new signature uses <c>UseRedis(Action&lt;IRedisLeaderElectionBuilder&gt;)</c>
/// where the lock key becomes a builder method (<c>.LockKey(key)</c>).
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "Redis")]
public sealed class LeaderElectionBuilderRedisExtensionsShould : UnitTestBase
{
    private const string TestConnectionString = "localhost:6379";
    private const string TestLockKey = "myapp:leader";

    [Fact]
    public void UseRedis_ThrowWhenBuilderIsNull()
    {
        // Arrange
        ILeaderElectionBuilder builder = null!;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            builder.UseRedis(redis =>
                redis.ConnectionString(TestConnectionString).LockKey(TestLockKey)));
    }

    [Fact]
    public void UseRedis_ThrowWhenConfigureIsNull()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<ILeaderElectionBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            builder.UseRedis((Action<IRedisLeaderElectionBuilder>)null!));
    }

    [Fact]
    public void UseRedis_ReturnSameBuilderForChaining()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<ILeaderElectionBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        var result = builder.UseRedis(redis =>
            redis.ConnectionString(TestConnectionString).LockKey(TestLockKey));

        // Assert
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void UseRedis_InvokeConfigureAction()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<ILeaderElectionBuilder>();
        A.CallTo(() => builder.Services).Returns(services);
        var configureInvoked = false;

        // Act
        builder.UseRedis(redis =>
        {
            redis.ConnectionString(TestConnectionString).LockKey(TestLockKey);
            configureInvoked = true;
        });

        // Assert
        configureInvoked.ShouldBeTrue();
    }

    [Fact]
    public void UseRedis_RegisterServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<ILeaderElectionBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        builder.UseRedis(redis =>
            redis.ConnectionString(TestConnectionString).LockKey(TestLockKey));

        // Assert — verify services were registered
        services.Count.ShouldBeGreaterThan(0);
    }
}
