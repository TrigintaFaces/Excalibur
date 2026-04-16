// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.CosmosDb;
using Excalibur.EventSourcing.DependencyInjection;

namespace Excalibur.Data.Tests.CosmosDb.EntryPoints;

/// <summary>
/// Unit tests for <see cref="EventSourcingBuilderCosmosDbExtensions.UseCosmosDb(IEventSourcingBuilder, Action{ICosmosDbEventSourcingBuilder})"/>.
/// Validates null guards, fluent chaining, configure invocation, and options registration
/// for the <c>Action&lt;ICosmosDbEventSourcingBuilder&gt;</c> entry point.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "CosmosDb")]
public sealed class EventSourcingBuilderCosmosDbExtensionsShould : UnitTestBase
{
    private const string TestConnectionString =
        "AccountEndpoint=https://localhost:8081/;AccountKey=dGVzdA==";

    [Fact]
    public void UseCosmosDb_ThrowWhenBuilderIsNull()
    {
        // Arrange
        IEventSourcingBuilder builder = null!;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            builder.UseCosmosDb(cosmo =>
                cosmo.ConnectionString(TestConnectionString)));
    }

    [Fact]
    public void UseCosmosDb_ThrowWhenConfigureIsNull()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IEventSourcingBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            builder.UseCosmosDb((Action<ICosmosDbEventSourcingBuilder>)null!));
    }

    [Fact]
    public void UseCosmosDb_ReturnSameBuilderForChaining()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IEventSourcingBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        var result = builder.UseCosmosDb(cosmo =>
            cosmo.ConnectionString(TestConnectionString));

        // Assert
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void UseCosmosDb_InvokeConfigureAction()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IEventSourcingBuilder>();
        A.CallTo(() => builder.Services).Returns(services);
        var configureInvoked = false;

        // Act
        builder.UseCosmosDb(cosmo =>
        {
            cosmo.ConnectionString(TestConnectionString);
            configureInvoked = true;
        });

        // Assert
        configureInvoked.ShouldBeTrue();
    }

    [Fact]
    public void UseCosmosDb_RegisterEventStoreOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IEventSourcingBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        builder.UseCosmosDb(cosmo =>
            cosmo.ConnectionString(TestConnectionString));

        // Assert
        services.ShouldContain(sd =>
            sd.ServiceType == typeof(IConfigureOptions<CosmosDbEventStoreOptions>));
    }

    [Fact]
    public void UseCosmosDb_ConfiguresContainerNameViaBuilder()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IEventSourcingBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        builder.UseCosmosDb(cosmo => cosmo
            .ConnectionString(TestConnectionString)
            .ContainerName("my_events"));

        // Assert
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<CosmosDbEventStoreOptions>>();
        options.Value.EventsContainerName.ShouldBe("my_events");
    }
}
