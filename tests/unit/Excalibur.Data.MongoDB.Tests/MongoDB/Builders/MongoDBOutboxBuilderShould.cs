// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.MongoDB;

using MongoDB.Driver;

namespace Excalibur.Data.Tests.MongoDB.Builders;

/// <summary>
/// Unit tests for <see cref="MongoDBOutboxBuilder"/> — 4 connection overloads,
/// feature methods, last-wins semantics, and fluent chaining.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "MongoDB")]
public sealed class MongoDBOutboxBuilderShould : UnitTestBase
{
    private const string TestConnectionString = "mongodb://localhost:27017";

    private static (MongoDBOutboxBuilder Builder, MongoDbOutboxOptions Options) CreateBuilder()
    {
        var options = new MongoDbOutboxOptions();
        var builder = new MongoDBOutboxBuilder(options);
        return (builder, options);
    }

    // --- Connection overloads (happy path) ---

    [Fact]
    public void ConnectionString_SetConnectionStringOnOptions()
    {
        var (builder, options) = CreateBuilder();

        builder.ConnectionString(TestConnectionString);

        options.ConnectionString.ShouldBe(TestConnectionString);
    }

    [Fact]
    public void Client_StoreInstance()
    {
        var (builder, _) = CreateBuilder();
        var client = A.Fake<IMongoClient>();

        builder.Client(client);

        builder.ClientInstance.ShouldBe(client);
    }

    [Fact]
    public void ClientFactory_StoreFactory()
    {
        var (builder, _) = CreateBuilder();
        Func<IServiceProvider, IMongoClient> factory = _ => A.Fake<IMongoClient>();

        builder.ClientFactory(factory);

        builder.ClientFactoryFunc.ShouldBe(factory);
    }

    [Fact]
    public void BindConfiguration_StorePath()
    {
        var (builder, _) = CreateBuilder();

        builder.BindConfiguration("Outbox:MongoDB");

        builder.BindConfigurationPath.ShouldBe("Outbox:MongoDB");
    }

    // --- Last-wins semantics ---

    [Fact]
    public void ConnectionString_ClearOtherOverloads()
    {
        var (builder, options) = CreateBuilder();
        var client = A.Fake<IMongoClient>();

        builder.Client(client);
        builder.ConnectionString(TestConnectionString);

        options.ConnectionString.ShouldBe(TestConnectionString);
        builder.ClientInstance.ShouldBeNull();
        builder.ClientFactoryFunc.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBeNull();
    }

    [Fact]
    public void Client_ClearOtherOverloads()
    {
        var (builder, options) = CreateBuilder();
        var client = A.Fake<IMongoClient>();

        builder.ConnectionString(TestConnectionString);
        builder.Client(client);

        options.ConnectionString.ShouldBe(null!);
        builder.ClientInstance.ShouldBe(client);
        builder.ClientFactoryFunc.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBeNull();
    }

    [Fact]
    public void ClientFactory_ClearOtherOverloads()
    {
        var (builder, options) = CreateBuilder();
        var client = A.Fake<IMongoClient>();
        Func<IServiceProvider, IMongoClient> factory = _ => A.Fake<IMongoClient>();

        builder.Client(client);
        builder.ClientFactory(factory);

        options.ConnectionString.ShouldBe(null!);
        builder.ClientInstance.ShouldBeNull();
        builder.ClientFactoryFunc.ShouldBe(factory);
        builder.BindConfigurationPath.ShouldBeNull();
    }

    [Fact]
    public void BindConfiguration_ClearOtherOverloads()
    {
        var (builder, options) = CreateBuilder();
        var client = A.Fake<IMongoClient>();

        builder.Client(client);
        builder.BindConfiguration("Outbox:MongoDB");

        options.ConnectionString.ShouldBe(null!);
        builder.ClientInstance.ShouldBeNull();
        builder.ClientFactoryFunc.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBe("Outbox:MongoDB");
    }

    // --- Feature methods ---

    [Fact]
    public void DatabaseName_SetOnOptions()
    {
        var (builder, options) = CreateBuilder();

        builder.DatabaseName("my_database");

        options.DatabaseName.ShouldBe("my_database");
    }

    [Fact]
    public void CollectionName_SetOnOptions()
    {
        var (builder, options) = CreateBuilder();

        builder.CollectionName("my_outbox");

        options.CollectionName.ShouldBe("my_outbox");
    }

    // --- Fluent chaining ---

    [Fact]
    public void AllMethods_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();

        var result = builder
            .ConnectionString(TestConnectionString)
            .DatabaseName("db")
            .CollectionName("outbox");

        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void Client_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        var result = builder.Client(A.Fake<IMongoClient>());
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void ClientFactory_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        var result = builder.ClientFactory(_ => A.Fake<IMongoClient>());
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void BindConfiguration_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        var result = builder.BindConfiguration("Outbox:MongoDB");
        result.ShouldBeSameAs(builder);
    }

    // --- Constructor ---

    [Fact]
    public void Constructor_ThrowOnNullOptions()
    {
        Should.Throw<ArgumentNullException>(() =>
            new MongoDBOutboxBuilder(null!));
    }

    // --- Validation guards ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConnectionString_ThrowOnInvalidValue(string? value)
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.ConnectionString(value!));
    }

    [Fact]
    public void Client_ThrowOnNull()
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentNullException>(() => builder.Client(null!));
    }

    [Fact]
    public void ClientFactory_ThrowOnNull()
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentNullException>(() => builder.ClientFactory(null!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BindConfiguration_ThrowOnInvalidValue(string? value)
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.BindConfiguration(value!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DatabaseName_ThrowOnInvalidValue(string? value)
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.DatabaseName(value!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CollectionName_ThrowOnInvalidValue(string? value)
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.CollectionName(value!));
    }
}
