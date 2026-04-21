// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Postgres;
using Excalibur.Data.Postgres.Persistence;

using Npgsql;

namespace Excalibur.Data.Tests.Postgres.Builders.Data;

/// <summary>
/// Unit tests for <see cref="PostgresDataBuilder"/> — 5 connection overloads,
/// last-wins semantics, and fluent chaining.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "Postgres")]
public sealed class PostgresDataBuilderShould : UnitTestBase
{
    private const string TestConnectionString =
        "Host=localhost;Database=TestDb;Username=test;Password=test";

    private static (PostgresDataBuilder Builder, PostgresPersistenceOptions Options) CreateBuilder()
    {
        var options = new PostgresPersistenceOptions();
        var builder = new PostgresDataBuilder(options);
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
        Func<IServiceProvider, NpgsqlDataSource> factory = _ => NpgsqlDataSource.Create(TestConnectionString);
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
        builder.ConnectionStringName("DataStore");
        builder.ConnectionStringNameValue.ShouldBe("DataStore");
    }

    [Fact]
    public void BindConfiguration_StorePath()
    {
        var (builder, _) = CreateBuilder();
        builder.BindConfiguration("Data:Postgres");
        builder.BindConfigurationPath.ShouldBe("Data:Postgres");
    }

    // --- Last-wins semantics ---

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
        using var ds = NpgsqlDataSource.Create(TestConnectionString);
        builder.DataSource(ds);
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
        using var ds = NpgsqlDataSource.Create(TestConnectionString);
        builder.ConnectionString(TestConnectionString);
        builder.DataSource(ds);

        options.ConnectionString.ShouldBeNull();
        builder.DataSourceInstance.ShouldBe(ds);
        builder.DataSourceFactoryFunc.ShouldBeNull();
        builder.ConnectionStringNameValue.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBeNull();
    }

    [Fact]
    public void ConnectionStringName_ClearAll()
    {
        var (builder, options) = CreateBuilder();
        builder.ConnectionString(TestConnectionString);
        builder.ConnectionStringName("DataStore");

        options.ConnectionString.ShouldBeNull();
        builder.DataSourceFactoryFunc.ShouldBeNull();
        builder.DataSourceInstance.ShouldBeNull();
        builder.ConnectionStringNameValue.ShouldBe("DataStore");
        builder.BindConfigurationPath.ShouldBeNull();
    }

    [Fact]
    public void BindConfiguration_ClearAll()
    {
        var (builder, options) = CreateBuilder();
        using var ds = NpgsqlDataSource.Create(TestConnectionString);
        builder.DataSource(ds);
        builder.BindConfiguration("Data:Postgres");

        options.ConnectionString.ShouldBeNull();
        builder.DataSourceFactoryFunc.ShouldBeNull();
        builder.DataSourceInstance.ShouldBeNull();
        builder.ConnectionStringNameValue.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBe("Data:Postgres");
    }

    // --- Fluent chaining ---

    [Fact]
    public void AllMethods_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        using var ds = NpgsqlDataSource.Create(TestConnectionString);

        builder.ConnectionString(TestConnectionString).ShouldBeSameAs(builder);
        builder.DataSourceFactory(_ => NpgsqlDataSource.Create(TestConnectionString)).ShouldBeSameAs(builder);
        builder.DataSource(ds).ShouldBeSameAs(builder);
        builder.ConnectionStringName("DataStore").ShouldBeSameAs(builder);
        builder.BindConfiguration("Data:Postgres").ShouldBeSameAs(builder);
    }

    // --- Constructor ---

    [Fact]
    public void Constructor_ThrowOnNullOptions()
    {
        Should.Throw<ArgumentNullException>(() => new PostgresDataBuilder(null!));
    }

    // --- Validation guards ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConnectionString_ThrowOnInvalidValue(string? invalidValue)
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.ConnectionString(invalidValue!));
    }

    [Fact]
    public void DataSourceFactory_ThrowOnNull()
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentNullException>(() => builder.DataSourceFactory(null!));
    }

    [Fact]
    public void DataSource_ThrowOnNull()
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentNullException>(() => builder.DataSource(null!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConnectionStringName_ThrowOnInvalidValue(string? invalidValue)
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.ConnectionStringName(invalidValue!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BindConfiguration_ThrowOnInvalidValue(string? invalidValue)
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.BindConfiguration(invalidValue!));
    }
}
