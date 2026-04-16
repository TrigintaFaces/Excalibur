// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Inbox.DependencyInjection;
using Excalibur.Inbox.Redis;

namespace Excalibur.Data.Tests.Redis.EntryPoints;

/// <summary>
/// Unit tests for <see cref="InboxBuilderRedisExtensions.UseRedis(IInboxBuilder, Action{IRedisInboxBuilder})"/>.
/// Validates null guards, fluent chaining, and options registration
/// for the <c>Action&lt;IRedisInboxBuilder&gt;</c> entry point.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "Redis")]
public sealed class InboxBuilderRedisExtensionsShould : UnitTestBase
{
    private const string TestConnectionString = "localhost:6379";

    [Fact]
    public void UseRedis_ThrowWhenBuilderIsNull()
    {
        // Arrange
        IInboxBuilder builder = null!;

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
        var builder = A.Fake<IInboxBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            builder.UseRedis((Action<IRedisInboxBuilder>)null!));
    }

    [Fact]
    public void UseRedis_ReturnSameBuilderForChaining()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IInboxBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        var result = builder.UseRedis(redis =>
            redis.ConnectionString(TestConnectionString));

        // Assert
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void UseRedis_RegisterInboxOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IInboxBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        builder.UseRedis(redis =>
            redis.ConnectionString(TestConnectionString));

        // Assert
        services.ShouldContain(sd =>
            sd.ServiceType == typeof(IConfigureOptions<RedisInboxOptions>));
    }

    [Fact]
    public void UseRedis_ConfiguresKeyPrefixViaBuilder()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IInboxBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        builder.UseRedis(redis => redis
            .ConnectionString(TestConnectionString)
            .KeyPrefix("custom_inbox"));

        // Assert
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RedisInboxOptions>>();
        options.Value.KeyPrefix.ShouldBe("custom_inbox");
    }
}
