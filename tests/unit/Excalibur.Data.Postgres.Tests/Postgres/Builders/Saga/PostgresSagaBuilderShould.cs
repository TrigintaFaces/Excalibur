// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Postgres;

using Npgsql;

namespace Excalibur.Data.Tests.Postgres.Builders.Saga;

/// <summary>
/// Unit tests for <see cref="PostgresSagaBuilder"/> — 5 connection overloads,
/// feature methods, last-wins semantics, and fluent chaining.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "Postgres")]
public sealed class PostgresSagaBuilderShould : UnitTestBase
{
    private const string TestConnectionString =
        "Host=localhost;Database=TestDb;Username=test;Password=test";

    private static (PostgresSagaBuilder Builder, PostgresSagaOptions Options) CreateBuilder()
    {
        var options = new PostgresSagaOptions();
        var builder = new PostgresSagaBuilder(options);
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
    public void DataSourceFactory_StoreFactory()
    {
        var (builder, _) = CreateBuilder();
        Func<IServiceProvider, NpgsqlDataSource> factory = _ =>
            NpgsqlDataSource.Create(TestConnectionString);

        builder.DataSourceFactory(factory);

        builder.DataSourceFactoryFunc.ShouldBe(factory);
    }

    [Fact]
    public void DataSource_StoreInstance()
    {
        var (builder, _) = CreateBuilder();
        using var dataSource = NpgsqlDataSource.Create(TestConnectionString);

        builder.DataSource(dataSource);

        builder.DataSourceInstance.ShouldBe(dataSource);
    }

    [Fact]
    public void ConnectionStringName_StoreName()
    {
        var (builder, _) = CreateBuilder();

        builder.ConnectionStringName("SagaStore");

        builder.ConnectionStringNameValue.ShouldBe("SagaStore");
    }

    [Fact]
    public void BindConfiguration_StorePath()
    {
        var (builder, _) = CreateBuilder();

        builder.BindConfiguration("Saga:Postgres");

        builder.BindConfigurationPath.ShouldBe("Saga:Postgres");
    }

    // --- Last-wins semantics (5 overloads) ---

    [Fact]
    public void DataSourceFactory_ClearConnectionString()
    {
        var (builder, options) = CreateBuilder();

        builder.ConnectionString(TestConnectionString);
        builder.DataSourceFactory(_ => NpgsqlDataSource.Create(TestConnectionString));

        options.ConnectionString.ShouldBeNull();
        builder.DataSourceFactoryFunc.ShouldNotBeNull();
        builder.DataSourceInstance.ShouldBeNull();
        builder.ConnectionStringNameValue.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBeNull();
    }

    [Fact]
    public void ConnectionString_ClearDataSource()
    {
        var (builder, options) = CreateBuilder();
        using var dataSource = NpgsqlDataSource.Create(TestConnectionString);

        builder.DataSource(dataSource);
        builder.ConnectionString(TestConnectionString);

        options.ConnectionString.ShouldBe(TestConnectionString);
        builder.DataSourceInstance.ShouldBeNull();
        builder.DataSourceFactoryFunc.ShouldBeNull();
        builder.ConnectionStringNameValue.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBeNull();
    }

    [Fact]
    public void DataSource_ClearAll()
    {
        var (builder, options) = CreateBuilder();
        using var dataSource = NpgsqlDataSource.Create(TestConnectionString);

        builder.ConnectionString(TestConnectionString);
        builder.DataSource(dataSource);

        options.ConnectionString.ShouldBeNull();
        builder.DataSourceInstance.ShouldBe(dataSource);
        builder.DataSourceFactoryFunc.ShouldBeNull();
        builder.ConnectionStringNameValue.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBeNull();
    }

    [Fact]
    public void ConnectionStringName_ClearAll()
    {
        var (builder, options) = CreateBuilder();

        builder.ConnectionString(TestConnectionString);
        builder.ConnectionStringName("SagaStore");

        options.ConnectionString.ShouldBeNull();
        builder.DataSourceFactoryFunc.ShouldBeNull();
        builder.DataSourceInstance.ShouldBeNull();
        builder.ConnectionStringNameValue.ShouldBe("SagaStore");
        builder.BindConfigurationPath.ShouldBeNull();
    }

    [Fact]
    public void BindConfiguration_ClearAll()
    {
        var (builder, options) = CreateBuilder();
        using var dataSource = NpgsqlDataSource.Create(TestConnectionString);

        builder.DataSource(dataSource);
        builder.BindConfiguration("Saga:Postgres");

        options.ConnectionString.ShouldBeNull();
        builder.DataSourceFactoryFunc.ShouldBeNull();
        builder.DataSourceInstance.ShouldBeNull();
        builder.ConnectionStringNameValue.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBe("Saga:Postgres");
    }

    // --- Feature methods ---

    [Fact]
    public void SchemaName_SetSchemaOnOptions()
    {
        var (builder, options) = CreateBuilder();
        builder.SchemaName("custom_schema");
        options.Schema.ShouldBe("custom_schema");
    }

    [Fact]
    public void TableName_SetTableOnOptions()
    {
        var (builder, options) = CreateBuilder();
        builder.TableName("custom_sagas");
        options.TableName.ShouldBe("custom_sagas");
    }

    // --- Fluent chaining ---

    [Fact]
    public void AllMethods_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();

        var result = builder
            .ConnectionString(TestConnectionString)
            .SchemaName("dispatch")
            .TableName("sagas");

        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void DataSourceFactory_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        var result = builder.DataSourceFactory(_ => NpgsqlDataSource.Create(TestConnectionString));
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void DataSource_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        using var dataSource = NpgsqlDataSource.Create(TestConnectionString);
        var result = builder.DataSource(dataSource);
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void ConnectionStringName_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        var result = builder.ConnectionStringName("SagaStore");
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void BindConfiguration_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        var result = builder.BindConfiguration("Saga:Postgres");
        result.ShouldBeSameAs(builder);
    }

    // --- Constructor ---

    [Fact]
    public void Constructor_ThrowOnNullOptions()
    {
        Should.Throw<ArgumentNullException>(() =>
            new PostgresSagaBuilder(null!));
    }
}
