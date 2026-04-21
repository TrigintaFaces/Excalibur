// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection.DependencyInjection;
using Excalibur.LeaderElection.MongoDB;

namespace Excalibur.Data.Tests.MongoDB.EntryPoints;

/// <summary>
/// Unit tests for <see cref="MongoDbLeaderElectionBuilderExtensions.UseMongoDB"/>.
/// Validates null guards, fluent chaining, and options registration
/// for the <c>Action&lt;IMongoDBLeaderElectionBuilder&gt;</c> entry point.
/// </summary>
/// <remarks>
/// Leader election keeps the <c>resourceName</c> parameter alongside the builder configure action.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "MongoDB")]
public sealed class LeaderElectionBuilderMongoDbExtensionsShould : UnitTestBase
{
    private const string TestConnectionString = "mongodb://localhost:27017";
    private const string TestResourceName = "test-service:leader";

    [Fact]
    public void UseMongoDB_ThrowWhenBuilderIsNull()
    {
        // Arrange
        ILeaderElectionBuilder builder = null!;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            builder.UseMongoDB(TestResourceName, mongo => mongo.ConnectionString(TestConnectionString)));
    }

    [Fact]
    public void UseMongoDB_ThrowWhenResourceNameIsNull()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<ILeaderElectionBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            builder.UseMongoDB(null!, mongo => mongo.ConnectionString(TestConnectionString)));
    }

    [Fact]
    public void UseMongoDB_ThrowWhenResourceNameIsWhitespace()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<ILeaderElectionBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            builder.UseMongoDB("  ", mongo => mongo.ConnectionString(TestConnectionString)));
    }

    [Fact]
    public void UseMongoDB_ThrowWhenConfigureIsNull()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<ILeaderElectionBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            builder.UseMongoDB(TestResourceName, (Action<IMongoDBLeaderElectionBuilder>)null!));
    }

    [Fact]
    public void UseMongoDB_ReturnSameBuilderForChaining()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<ILeaderElectionBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        var result = builder.UseMongoDB(TestResourceName,
            mongo => mongo.ConnectionString(TestConnectionString));

        // Assert
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void UseMongoDB_RegisterLeaderElectionOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<ILeaderElectionBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        builder.UseMongoDB(TestResourceName,
            mongo => mongo.ConnectionString(TestConnectionString));

        // Assert
        services.ShouldContain(sd =>
            sd.ServiceType == typeof(IConfigureOptions<MongoDbLeaderElectionOptions>));
    }

    [Fact]
    public void UseMongoDB_ConfiguresDatabaseNameViaBuilder()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<ILeaderElectionBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        builder.UseMongoDB(TestResourceName, mongo => mongo
            .ConnectionString(TestConnectionString)
            .DatabaseName("custom_le"));

        // Assert
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<MongoDbLeaderElectionOptions>>();
        options.Value.DatabaseName.ShouldBe("custom_le");
    }

    [Fact]
    public void UseMongoDB_ConfiguresCollectionNameViaBuilder()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = A.Fake<ILeaderElectionBuilder>();
        A.CallTo(() => builder.Services).Returns(services);

        // Act
        builder.UseMongoDB(TestResourceName, mongo => mongo
            .ConnectionString(TestConnectionString)
            .CollectionName("my_elections"));

        // Assert
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<MongoDbLeaderElectionOptions>>();
        options.Value.CollectionName.ShouldBe("my_elections");
    }
}
