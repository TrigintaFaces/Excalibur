// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.CosmosDb;
using Excalibur.Saga.DependencyInjection;

namespace Excalibur.Data.Tests.CosmosDb.EntryPoints;

/// <summary>
/// Unit tests for <see cref="SagaBuilderCosmosDbExtensions.UseCosmosDb(ISagaBuilder, Action{ICosmosDbSagaBuilder})"/>.
/// Validates null guards, fluent chaining, and options registration
/// for the <c>Action&lt;ICosmosDbSagaBuilder&gt;</c> entry point.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "CosmosDb")]
public sealed class SagaBuilderCosmosDbExtensionsShould : UnitTestBase
{
    private const string TestConnectionString =
        "AccountEndpoint=https://localhost:8081/;AccountKey=dGVzdA==";

    [Fact]
    public void UseCosmosDb_ThrowWhenBuilderIsNull()
    {
        // Arrange
        ISagaBuilder builder = null!;

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
        var builder = A.Fake<ISagaBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            builder.UseCosmosDb((Action<ICosmosDbSagaBuilder>)null!));
    }

    [Fact]
    public void UseCosmosDb_ReturnSameBuilderForChaining()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<ISagaBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        var result = builder.UseCosmosDb(cosmo =>
            cosmo.ConnectionString(TestConnectionString));

        // Assert
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void UseCosmosDb_RegisterSagaOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<ISagaBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        builder.UseCosmosDb(cosmo =>
            cosmo.ConnectionString(TestConnectionString));

        // Assert
        services.ShouldContain(sd =>
            sd.ServiceType == typeof(IConfigureOptions<CosmosDbSagaOptions>));
    }

    [Fact]
    public void UseCosmosDb_InvokeConfigureAction()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<ISagaBuilder>();
        A.CallTo(() => builder.Services).Returns(services);
        var configureInvoked = false;

        // Act
        builder.UseCosmosDb(cosmo =>
        {
            cosmo.ConnectionString(TestConnectionString)
                 .DatabaseName("custom_sagas")
                 .ContainerName("sagas");
            configureInvoked = true;
        });

        // Assert
        configureInvoked.ShouldBeTrue();
    }
}
