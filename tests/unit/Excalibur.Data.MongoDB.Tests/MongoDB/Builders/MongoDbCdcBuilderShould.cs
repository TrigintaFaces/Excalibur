// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Cdc.MongoDB;

using MongoDB.Driver;

namespace Excalibur.Data.Tests.MongoDB.Builders;

/// <summary>
/// Unit tests for <see cref="MongoDbCdcBuilder"/> — 4 connection overloads,
/// feature methods (DatabaseName, CollectionNames, ProcessorId, BatchSize,
/// ReconnectInterval, WithStateStore), last-wins semantics, and fluent chaining.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "MongoDB")]
public sealed class MongoDbCdcBuilderShould : UnitTestBase
{
    private const string TestConnectionString = "mongodb://localhost:27017";
    private static readonly string[] ExpectedCollectionNames = ["orders", "customers"];

    private static (MongoDbCdcBuilder Builder, MongoDbCdcOptions Options) CreateBuilder()
    {
        var options = new MongoDbCdcOptions();
        var builder = new MongoDbCdcBuilder(options);
        return (builder, options);
    }

    // --- Connection overloads (happy path) ---

    [Fact]
    public void ConnectionString_SetConnectionStringOnOptions()
    {
        var (builder, options) = CreateBuilder();

        builder.ConnectionString(TestConnectionString);

        options.Connection.ConnectionString.ShouldBe(TestConnectionString);
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

        builder.BindConfiguration("Cdc:MongoDB");

        builder.SourceBindConfigurationPath.ShouldBe("Cdc:MongoDB");
    }

    // --- Last-wins semantics (CDC variant: ConnectionString does not clear BindConfiguration) ---

    [Fact]
    public void ConnectionString_ClearClientInstanceAndFactory()
    {
        var (builder, options) = CreateBuilder();
        var client = A.Fake<IMongoClient>();

        builder.Client(client);
        builder.ConnectionString(TestConnectionString);

        options.Connection.ConnectionString.ShouldBe(TestConnectionString);
        builder.ClientInstance.ShouldBeNull();
        builder.ClientFactoryFunc.ShouldBeNull();
    }

    [Fact]
    public void Client_ClearConnectionStringAndFactory()
    {
        var (builder, options) = CreateBuilder();

        builder.ConnectionString(TestConnectionString);
        var client = A.Fake<IMongoClient>();
        builder.Client(client);

        options.Connection.ConnectionString.ShouldBe(null!);
        builder.ClientInstance.ShouldBe(client);
        builder.ClientFactoryFunc.ShouldBeNull();
    }

    [Fact]
    public void ClientFactory_ClearConnectionStringAndClient()
    {
        var (builder, options) = CreateBuilder();
        var client = A.Fake<IMongoClient>();
        Func<IServiceProvider, IMongoClient> factory = _ => A.Fake<IMongoClient>();

        builder.Client(client);
        builder.ClientFactory(factory);

        options.Connection.ConnectionString.ShouldBe(null!);
        builder.ClientInstance.ShouldBeNull();
        builder.ClientFactoryFunc.ShouldBe(factory);
    }

    [Fact]
    public void BindConfiguration_StorePathWithoutClearingOtherOverloads()
    {
        var (builder, _) = CreateBuilder();

        builder.BindConfiguration("Cdc:MongoDB");

        builder.SourceBindConfigurationPath.ShouldBe("Cdc:MongoDB");
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
    public void CollectionNames_SetOnOptions()
    {
        var (builder, options) = CreateBuilder();

        builder.CollectionNames("orders", "customers");

        options.CollectionNames.ShouldBe(ExpectedCollectionNames);
    }

    [Fact]
    public void ProcessorId_SetOnOptions()
    {
        var (builder, options) = CreateBuilder();

        builder.ProcessorId("my-processor");

        options.ProcessorId.ShouldBe("my-processor");
    }

    [Fact]
    public void BatchSize_SetOnOptions()
    {
        var (builder, options) = CreateBuilder();

        builder.BatchSize(500);

        options.BatchSize.ShouldBe(500);
    }

    [Fact]
    public void ReconnectInterval_SetOnOptions()
    {
        var (builder, options) = CreateBuilder();
        var interval = TimeSpan.FromSeconds(10);

        builder.ReconnectInterval(interval);

        options.ReconnectInterval.ShouldBe(interval);
    }

    [Fact]
    public void WithStateStore_StoreCallback()
    {
        var (builder, _) = CreateBuilder();
        Action<ICdcStateStoreBuilder> configure = _ => { };

        builder.WithStateStore(configure);

        builder.StateStoreConfigure.ShouldBe(configure);
    }

    // --- Fluent chaining ---

    [Fact]
    public void AllMethods_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();

        var result = builder
            .ConnectionString(TestConnectionString)
            .DatabaseName("db")
            .CollectionNames("col1", "col2")
            .ProcessorId("proc-1")
            .BatchSize(200)
            .ReconnectInterval(TimeSpan.FromSeconds(3))
            .WithStateStore(_ => { });

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
        var result = builder.BindConfiguration("Cdc:MongoDB");
        result.ShouldBeSameAs(builder);
    }

    // --- Constructor ---

    [Fact]
    public void Constructor_ThrowOnNullOptions()
    {
        Should.Throw<ArgumentNullException>(() =>
            new MongoDbCdcBuilder(null!));
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

    [Fact]
    public void CollectionNames_ThrowOnNull()
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentNullException>(() => builder.CollectionNames(null!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ProcessorId_ThrowOnInvalidValue(string? value)
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.ProcessorId(value!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void BatchSize_ThrowOnInvalidValue(int value)
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentOutOfRangeException>(() => builder.BatchSize(value));
    }

    [Fact]
    public void ReconnectInterval_ThrowOnZero()
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentOutOfRangeException>(() => builder.ReconnectInterval(TimeSpan.Zero));
    }

    [Fact]
    public void ReconnectInterval_ThrowOnNegative()
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentOutOfRangeException>(() => builder.ReconnectInterval(TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void WithStateStore_ThrowOnNull()
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentNullException>(() => builder.WithStateStore(null!));
    }
}
