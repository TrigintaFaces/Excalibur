// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox;
using Excalibur.Outbox.MongoDB;

namespace Excalibur.Data.Tests.MongoDB.EntryPoints;

/// <summary>
/// Unit tests for <see cref="OutboxBuilderMongoDbExtensions.UseMongoDB"/>.
/// Validates null guards, fluent chaining, and options registration
/// for the <c>Action&lt;IMongoDBOutboxBuilder&gt;</c> entry point.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "MongoDB")]
public sealed class OutboxBuilderMongoDbExtensionsShould : UnitTestBase
{
    private const string TestConnectionString = "mongodb://localhost:27017";

    [Fact]
    public void UseMongoDB_ThrowWhenBuilderIsNull()
    {
        // Arrange
        IOutboxBuilder builder = null!;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            builder.UseMongoDB(mongo => mongo.ConnectionString(TestConnectionString)));
    }

    [Fact]
    public void UseMongoDB_ThrowWhenConfigureIsNull()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IOutboxBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            builder.UseMongoDB((Action<IMongoDBOutboxBuilder>)null!));
    }

    [Fact]
    public void UseMongoDB_ReturnSameBuilderForChaining()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IOutboxBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        var result = builder.UseMongoDB(mongo => mongo.ConnectionString(TestConnectionString));

        // Assert
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void UseMongoDB_RegisterOutboxOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IOutboxBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        builder.UseMongoDB(mongo => mongo.ConnectionString(TestConnectionString));

        // Assert
        services.ShouldContain(sd =>
            sd.ServiceType == typeof(IConfigureOptions<MongoDbOutboxOptions>));
    }

    [Fact]
    public void UseMongoDB_ConfiguresDatabaseNameViaBuilder()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IOutboxBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        builder.UseMongoDB(mongo => mongo
            .ConnectionString(TestConnectionString)
            .DatabaseName("custom_outbox"));

        // Assert
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<MongoDbOutboxOptions>>();
        options.Value.DatabaseName.ShouldBe("custom_outbox");
    }

    [Fact]
    public void UseMongoDB_ConfiguresCollectionNameViaBuilder()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IOutboxBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        builder.UseMongoDB(mongo => mongo
            .ConnectionString(TestConnectionString)
            .CollectionName("my_outbox"));

        // Assert
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<MongoDbOutboxOptions>>();
        options.Value.CollectionName.ShouldBe("my_outbox");
    }
}