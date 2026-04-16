// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Redis;

namespace Excalibur.Data.Tests.Redis.EntryPoints;

/// <summary>
/// Unit tests for <see cref="EventSourcingBuilderRedisExtensions.UseRedis(IEventSourcingBuilder, Action{IRedisEventSourcingBuilder})"/>.
/// Validates null guards, fluent chaining, configure invocation, and options registration
/// for the <c>Action&lt;IRedisEventSourcingBuilder&gt;</c> entry point.
/// </summary>
/// <remarks>
/// This is the most complex Redis rewiring. The old signature took dual
/// <c>Action&lt;RedisEventStoreOptions&gt;</c> + <c>Action&lt;RedisSnapshotStoreOptions&gt;</c>.
/// The new signature unifies into a single <c>Action&lt;IRedisEventSourcingBuilder&gt;</c>.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "Redis")]
public sealed class EventSourcingBuilderRedisExtensionsShould : UnitTestBase
{
    private const string TestConnectionString = "localhost:6379";

    [Fact]
    public void UseRedis_ThrowWhenBuilderIsNull()
    {
        // Arrange
        IEventSourcingBuilder builder = null!;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            builder.UseRedis(redis =>
                redis.ConnectionString(TestConnectionString)));
    }

    [Fact]
    public void UseRedis_ThrowWhenConfigureIsNull()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IEventSourcingBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            builder.UseRedis((Action<IRedisEventSourcingBuilder>)null!));
    }

    [Fact]
    public void UseRedis_ReturnSameBuilderForChaining()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IEventSourcingBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        var result = builder.UseRedis(redis =>
            redis.ConnectionString(TestConnectionString));

        // Assert
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void UseRedis_InvokeConfigureAction()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IEventSourcingBuilder>();
        A.CallTo(() => builder.Services).Returns(services);
        var configureInvoked = false;

        // Act
        builder.UseRedis(redis =>
        {
            redis.ConnectionString(TestConnectionString);
            configureInvoked = true;
        });

        // Assert
        configureInvoked.ShouldBeTrue();
    }

    [Fact]
    public void UseRedis_RegisterEventStoreOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IEventSourcingBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        builder.UseRedis(redis =>
            redis.ConnectionString(TestConnectionString));

        // Assert
        services.ShouldContain(sd =>
            sd.ServiceType == typeof(IConfigureOptions<RedisEventStoreOptions>));
    }

    [Fact]
    public void UseRedis_RegisterSnapshotStoreOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IEventSourcingBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        builder.UseRedis(redis =>
            redis.ConnectionString(TestConnectionString));

        // Assert
        services.ShouldContain(sd =>
            sd.ServiceType == typeof(IConfigureOptions<RedisSnapshotStoreOptions>));
    }

    [Fact]
    public void UseRedis_ConfiguresKeyPrefixViaBuilder()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IEventSourcingBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        builder.UseRedis(redis => redis
            .ConnectionString(TestConnectionString)
            .KeyPrefix("custom_es"));

        // Assert
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RedisEventStoreOptions>>();
        options.Value.StreamKeyPrefix.ShouldBe("custom_es");
    }
}
