// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Inbox.DependencyInjection;
using Excalibur.Inbox.MongoDB;

namespace Excalibur.Data.Tests.MongoDB.EntryPoints;

/// <summary>
/// Unit tests for <see cref="InboxBuilderMongoDbExtensions.UseMongoDB"/>.
/// Validates null guards, fluent chaining, and options registration
/// for the <c>Action&lt;IMongoDBInboxBuilder&gt;</c> entry point.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "MongoDB")]
public sealed class InboxBuilderMongoDbExtensionsShould : UnitTestBase
{
    private const string TestConnectionString = "mongodb://localhost:27017";

    [Fact]
    public void UseMongoDB_ThrowWhenBuilderIsNull()
    {
        // Arrange
        IInboxBuilder builder = null!;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            builder.UseMongoDB(mongo => mongo.ConnectionString(TestConnectionString)));
    }

    [Fact]
    public void UseMongoDB_ThrowWhenConfigureIsNull()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IInboxBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            builder.UseMongoDB((Action<IMongoDBInboxBuilder>)null!));
    }

    [Fact]
    public void UseMongoDB_ReturnSameBuilderForChaining()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IInboxBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        var result = builder.UseMongoDB(mongo => mongo.ConnectionString(TestConnectionString));

        // Assert
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void UseMongoDB_RegisterInboxOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IInboxBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        builder.UseMongoDB(mongo => mongo.ConnectionString(TestConnectionString));

        // Assert
        services.ShouldContain(sd =>
            sd.ServiceType == typeof(IConfigureOptions<MongoDbInboxOptions>));
    }

    [Fact]
    public void UseMongoDB_ConfiguresDatabaseNameViaBuilder()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IInboxBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        builder.UseMongoDB(mongo => mongo
            .ConnectionString(TestConnectionString)
            .DatabaseName("custom_inbox"));

        // Assert
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<MongoDbInboxOptions>>();
        options.Value.DatabaseName.ShouldBe("custom_inbox");
    }

    [Fact]
    public void UseMongoDB_ConfiguresCollectionNameViaBuilder()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<IInboxBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        builder.UseMongoDB(mongo => mongo
            .ConnectionString(TestConnectionString)
            .CollectionName("my_inbox"));

        // Assert
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<MongoDbInboxOptions>>();
        options.Value.CollectionName.ShouldBe("my_inbox");
    }
}
