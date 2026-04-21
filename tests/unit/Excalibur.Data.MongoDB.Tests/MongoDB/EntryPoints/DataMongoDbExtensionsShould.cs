// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.MongoDB;

namespace Excalibur.Data.Tests.MongoDB.EntryPoints;

/// <summary>
/// Unit tests for <see cref="MongoDbServiceCollectionExtensions.AddExcaliburMongoDb"/>.
/// Validates null guards, fluent chaining, and options registration
/// for the <c>Action&lt;IMongoDBDataBuilder&gt;</c> entry point.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "MongoDB")]
public sealed class DataMongoDbExtensionsShould : UnitTestBase
{
    private const string TestConnectionString = "mongodb://localhost:27017";

    [Fact]
    public void AddExcaliburMongoDb_ThrowWhenServicesIsNull()
    {
        // Arrange
        IServiceCollection services = null!;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            services.AddExcaliburMongoDb(mongo => mongo.ConnectionString(TestConnectionString)));
    }

    [Fact]
    public void AddExcaliburMongoDb_ThrowWhenConfigureIsNull()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            services.AddExcaliburMongoDb((Action<IMongoDBDataBuilder>)null!));
    }

    [Fact]
    public void AddExcaliburMongoDb_ReturnSameServicesForChaining()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddExcaliburMongoDb(mongo =>
            mongo.ConnectionString(TestConnectionString));

        // Assert
        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void AddExcaliburMongoDb_RegisterProviderOptions()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddExcaliburMongoDb(mongo =>
            mongo.ConnectionString(TestConnectionString));

        // Assert
        services.ShouldContain(sd =>
            sd.ServiceType == typeof(IConfigureOptions<MongoDbProviderOptions>));
    }

    [Fact]
    public void AddExcaliburMongoDb_ConfiguresDatabaseNameViaBuilder()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddExcaliburMongoDb(mongo => mongo
            .ConnectionString(TestConnectionString)
            .DatabaseName("custom_data"));

        // Assert
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<MongoDbProviderOptions>>();
        options.Value.DatabaseName.ShouldBe("custom_data");
    }
}
