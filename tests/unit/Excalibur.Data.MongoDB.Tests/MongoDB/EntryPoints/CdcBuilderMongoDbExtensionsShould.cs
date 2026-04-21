// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Cdc.MongoDB;

namespace Excalibur.Data.Tests.MongoDB.EntryPoints;

/// <summary>
/// Unit tests for <see cref="CdcBuilderMongoDbExtensions.UseMongoDB(ICdcBuilder, Action{IMongoDbCdcBuilder})"/>.
/// Validates null guards, fluent chaining, and options registration
/// for the <c>Action&lt;IMongoDbCdcBuilder&gt;</c> entry point.
/// </summary>
/// <remarks>
/// The CDC entry point was rewired prior to S779 but lacked entry point extension tests.
/// These tests cover the <c>Action&lt;IMongoDbCdcBuilder&gt;</c> overload specifically.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "MongoDB")]
public sealed class CdcBuilderMongoDbExtensionsShould : UnitTestBase
{
    private const string TestConnectionString = "mongodb://localhost:27017";

    [Fact]
    public void UseMongoDB_ThrowWhenBuilderIsNull()
    {
        // Arrange
        ICdcBuilder builder = null!;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            builder.UseMongoDB((Action<IMongoDbCdcBuilder>)(mongo =>
                mongo.ConnectionString(TestConnectionString))));
    }

    [Fact]
    public void UseMongoDB_ThrowWhenConfigureIsNull()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<ICdcBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            builder.UseMongoDB((Action<IMongoDbCdcBuilder>)null!));
    }

    [Fact]
    public void UseMongoDB_ReturnSameBuilderForChaining()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<ICdcBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        var result = builder.UseMongoDB((Action<IMongoDbCdcBuilder>)(mongo =>
            mongo.ConnectionString(TestConnectionString)));

        // Assert
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void UseMongoDB_RegisterCdcOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<ICdcBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        builder.UseMongoDB((Action<IMongoDbCdcBuilder>)(mongo =>
            mongo.ConnectionString(TestConnectionString)));

        // Assert
        services.ShouldContain(sd =>
            sd.ServiceType == typeof(IConfigureOptions<MongoDbCdcOptions>));
    }

    [Fact]
    public void UseMongoDB_ConfiguresDatabaseNameViaBuilder()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<ICdcBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        builder.UseMongoDB((Action<IMongoDbCdcBuilder>)(mongo => mongo
            .ConnectionString(TestConnectionString)
            .DatabaseName("custom_cdc")));

        // Assert
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<MongoDbCdcOptions>>();
        options.Value.DatabaseName.ShouldBe("custom_cdc");
    }

    [Fact]
    public void UseMongoDB_ConfiguresProcessorIdViaBuilder()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<ICdcBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        builder.UseMongoDB((Action<IMongoDbCdcBuilder>)(mongo => mongo
            .ConnectionString(TestConnectionString)
            .ProcessorId("cdc-processor-1")));

        // Assert
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<MongoDbCdcOptions>>();
        options.Value.ProcessorId.ShouldBe("cdc-processor-1");
    }

    [Fact]
    public void UseMongoDB_ConfiguresCollectionNamesViaBuilder()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<ICdcBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        builder.UseMongoDB((Action<IMongoDbCdcBuilder>)(mongo => mongo
            .ConnectionString(TestConnectionString)
            .CollectionNames("orders", "customers")));

        // Assert
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<MongoDbCdcOptions>>();
        options.Value.CollectionNames.ShouldContain("orders");
        options.Value.CollectionNames.ShouldContain("customers");
    }
}
