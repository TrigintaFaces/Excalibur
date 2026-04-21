// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;

namespace Excalibur.Data.Tests.CosmosDb.EntryPoints;

/// <summary>
/// Unit tests for <see cref="CdcBuilderCosmosDbExtensions.UseCosmosDb(ICdcBuilder, Action{ICosmosDbCdcBuilder})"/>.
/// Validates null guards, fluent chaining, and options registration
/// for the <c>Action&lt;ICosmosDbCdcBuilder&gt;</c> entry point.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "CosmosDb")]
public sealed class CdcBuilderCosmosDbExtensionsShould : UnitTestBase
{
    private const string TestConnectionString =
        "AccountEndpoint=https://localhost:8081/;AccountKey=dGVzdA==";

    [Fact]
    public void UseCosmosDb_ThrowWhenBuilderIsNull()
    {
        // Arrange
        ICdcBuilder builder = null!;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            builder.UseCosmosDb((Action<ICosmosDbCdcBuilder>)(cosmo =>
                cosmo.ConnectionString(TestConnectionString))));
    }

    [Fact]
    public void UseCosmosDb_ThrowWhenConfigureIsNull()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<ICdcBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            builder.UseCosmosDb((Action<ICosmosDbCdcBuilder>)null!));
    }

    [Fact]
    public void UseCosmosDb_ReturnSameBuilderForChaining()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<ICdcBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        var result = builder.UseCosmosDb((Action<ICosmosDbCdcBuilder>)(cosmo =>
            cosmo.ConnectionString(TestConnectionString)));

        // Assert
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void UseCosmosDb_RegisterCdcOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<ICdcBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        builder.UseCosmosDb((Action<ICosmosDbCdcBuilder>)(cosmo =>
            cosmo.ConnectionString(TestConnectionString)));

        // Assert
        services.ShouldContain(sd =>
            sd.ServiceType == typeof(IConfigureOptions<CosmosDbCdcOptions>));
    }

    [Fact]
    public void UseCosmosDb_ConfiguresDatabaseIdViaBuilder()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<ICdcBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        builder.UseCosmosDb((Action<ICosmosDbCdcBuilder>)(cosmo => cosmo
            .ConnectionString(TestConnectionString)
            .DatabaseId("custom_cdc")));

        // Assert
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<CosmosDbCdcOptions>>();
        options.Value.DatabaseId.ShouldBe("custom_cdc");
    }

    [Fact]
    public void UseCosmosDb_ConfiguresContainerIdViaBuilder()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<ICdcBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        builder.UseCosmosDb((Action<ICosmosDbCdcBuilder>)(cosmo => cosmo
            .ConnectionString(TestConnectionString)
            .ContainerId("my_cdc_container")));

        // Assert
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<CosmosDbCdcOptions>>();
        options.Value.ContainerId.ShouldBe("my_cdc_container");
    }

    [Fact]
    public void UseCosmosDb_ConfiguresProcessorNameViaBuilder()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<ICdcBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        builder.UseCosmosDb((Action<ICosmosDbCdcBuilder>)(cosmo => cosmo
            .ConnectionString(TestConnectionString)
            .ProcessorName("cdc-processor-1")));

        // Assert
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<CosmosDbCdcOptions>>();
        options.Value.ProcessorName.ShouldBe("cdc-processor-1");
    }
}
