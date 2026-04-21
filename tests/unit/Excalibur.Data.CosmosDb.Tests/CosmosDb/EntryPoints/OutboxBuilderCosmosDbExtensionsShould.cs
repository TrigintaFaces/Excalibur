// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox;
using Excalibur.Outbox.CosmosDb;

namespace Excalibur.Data.Tests.CosmosDb.EntryPoints;

/// <summary>
/// Unit tests for <see cref="OutboxBuilderCosmosDbExtensions.UseCosmosDb(IOutboxBuilder, Action{ICosmosDbOutboxBuilder})"/>.
/// Validates null guards, fluent chaining, and options registration
/// for the <c>Action&lt;ICosmosDbOutboxBuilder&gt;</c> entry point.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "CosmosDb")]
public sealed class OutboxBuilderCosmosDbExtensionsShould : UnitTestBase
{
    private const string TestConnectionString =
        "AccountEndpoint=https://localhost:8081/;AccountKey=dGVzdA==";

    [Fact]
    public void UseCosmosDb_ThrowWhenBuilderIsNull()
    {
        // Arrange
        IOutboxBuilder builder = null!;

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
        var builder = A.Fake<IOutboxBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            builder.UseCosmosDb((Action<ICosmosDbOutboxBuilder>)null!));
    }

    [Fact]
    public void UseCosmosDb_ReturnSameBuilderForChaining()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IOutboxBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        var result = builder.UseCosmosDb(cosmo =>
            cosmo.ConnectionString(TestConnectionString));

        // Assert
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void UseCosmosDb_RegisterOutboxOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IOutboxBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        builder.UseCosmosDb(cosmo =>
            cosmo.ConnectionString(TestConnectionString));

        // Assert
        services.ShouldContain(sd =>
            sd.ServiceType == typeof(IConfigureOptions<CosmosDbOutboxOptions>));
    }

    [Fact]
    public void UseCosmosDb_InvokeConfigureAction()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IOutboxBuilder>();
        A.CallTo(() => builder.Services).Returns(services);
        var configureInvoked = false;

        // Act
        builder.UseCosmosDb(cosmo =>
        {
            cosmo.ConnectionString(TestConnectionString)
                 .DatabaseName("custom_outbox")
                 .ContainerName("my_outbox");
            configureInvoked = true;
        });

        // Assert
        configureInvoked.ShouldBeTrue();
    }
}
