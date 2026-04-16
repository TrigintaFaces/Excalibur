// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Redis;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

using Tests.Shared.Categories;

using Xunit;

namespace Excalibur.Data.Tests.Redis;

/// <summary>
/// Unit tests for <see cref="RedisProviderServiceCollectionExtensions.AddExcaliburRedis"/>.
/// </summary>
/// <remarks>
/// Sprint 781: Rewired from old AddRedisProvider to AddExcaliburRedis(Action&lt;IRedisDataBuilder&gt;).
/// Tests verify the new builder-based entry point.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "Redis")]
public sealed class RedisDataExtensionsShould
{
    // --- Null Guards ---

    [Fact]
    public void AddExcaliburRedis_ThrowWhenServicesIsNull()
    {
        // Arrange
        IServiceCollection? services = null;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            services!.AddExcaliburRedis(redis => redis.ConnectionString("localhost:6379")));
    }

    [Fact]
    public void AddExcaliburRedis_ThrowWhenConfigureIsNull()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            services.AddExcaliburRedis((Action<IRedisDataBuilder>)null!));
    }

    // --- Fluent Chaining ---

    [Fact]
    public void AddExcaliburRedis_ReturnSameServiceCollectionForChaining()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddExcaliburRedis(redis =>
            redis.ConnectionString("localhost:6379"));

        // Assert
        result.ShouldBeSameAs(services);
    }

    // --- Configure Invocation ---

    [Fact]
    public void AddExcaliburRedis_InvokeConfigureAction()
    {
        // Arrange
        var services = new ServiceCollection();
        var configureInvoked = false;

        // Act
        services.AddExcaliburRedis(redis =>
        {
            configureInvoked = true;
            redis.ConnectionString("localhost:6379");
        });

        // Assert
        configureInvoked.ShouldBeTrue();
    }

    // --- Options Registration ---

    [Fact]
    public void AddExcaliburRedis_RegisterRedisProviderOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddExcaliburRedis(redis =>
            redis.ConnectionString("localhost:6379"));

        // Assert
        services.ShouldContain(sd =>
            sd.ServiceType == typeof(IConfigureOptions<RedisProviderOptions>));
    }

    // --- Options Value Verification ---

    [Fact]
    public void AddExcaliburRedis_ConfigureConnectionStringViaBuilder()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddExcaliburRedis(redis =>
            redis.ConnectionString("localhost:6379"));

        // Assert
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RedisProviderOptions>>();
        options.Value.ConnectionString.ShouldBe("localhost:6379");
    }

    [Fact]
    public void AddExcaliburRedis_ConfigureKeyPrefixViaBuilder()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddExcaliburRedis(redis =>
            redis.ConnectionString("localhost:6379")
                 .KeyPrefix("myapp"));

        // Assert
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RedisProviderOptions>>();
        options.Value.Name.ShouldBe("myapp");
    }

    [Fact]
    public void AddExcaliburRedis_ConfigureDatabaseViaBuilder()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddExcaliburRedis(redis =>
            redis.ConnectionString("localhost:6379")
                 .Database(3));

        // Assert
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RedisProviderOptions>>();
        options.Value.DatabaseId.ShouldBe(3);
    }
}
