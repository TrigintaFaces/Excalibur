// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.AuditLogging.Postgres;

using Npgsql;

using Shouldly;

using Tests.Shared;
using Tests.Shared.Categories;

using Xunit;


using Excalibur.AuditLogging;namespace Excalibur.AuditLogging.Postgres.Tests.Builders;

/// <summary>
/// Unit tests for <see cref="PostgresAuditLoggingBuilder"/> — 5 connection overloads,
/// feature methods, last-wins semantics, and fluent chaining.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, "Compliance")]
[Trait("Database", "Postgres")]
public sealed class PostgresAuditLoggingBuilderShould : UnitTestBase
{
    private const string TestConnectionString =
        "Host=localhost;Database=TestDb;Username=test;Password=test";

    private static (PostgresAuditLoggingBuilder Builder, PostgresAuditOptions Options) CreateBuilder()
    {
        var options = new PostgresAuditOptions();
        var builder = new PostgresAuditLoggingBuilder(options);
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
        builder.ConnectionStringName("AuditDb");
        builder.ConnectionStringNameValue.ShouldBe("AuditDb");
    }

    [Fact]
    public void BindConfiguration_StorePath()
    {
        var (builder, _) = CreateBuilder();
        builder.BindConfiguration("Audit:Postgres");
        builder.BindConfigurationPath.ShouldBe("Audit:Postgres");
    }

    // --- Feature methods ---

    [Fact]
    public void SchemaName_SetSchemaOnOptions()
    {
        var (builder, options) = CreateBuilder();
        builder.SchemaName("custom_audit");
        options.SchemaName.ShouldBe("custom_audit");
    }

    [Fact]
    public void TableName_SetTableOnOptions()
    {
        var (builder, options) = CreateBuilder();
        builder.TableName("custom_events");
        options.TableName.ShouldBe("custom_events");
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
        builder.ConnectionStringName("AuditDb");

        options.ConnectionString.ShouldBeNull();
        builder.DataSourceFactoryFunc.ShouldBeNull();
        builder.DataSourceInstance.ShouldBeNull();
        builder.ConnectionStringNameValue.ShouldBe("AuditDb");
        builder.BindConfigurationPath.ShouldBeNull();
    }

    [Fact]
    public void BindConfiguration_ClearAll()
    {
        var (builder, options) = CreateBuilder();
        using var ds = NpgsqlDataSource.Create(TestConnectionString);
        builder.DataSource(ds);
        builder.BindConfiguration("Audit:Postgres");

        options.ConnectionString.ShouldBeNull();
        builder.DataSourceFactoryFunc.ShouldBeNull();
        builder.DataSourceInstance.ShouldBeNull();
        builder.ConnectionStringNameValue.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBe("Audit:Postgres");
    }

    // --- Fluent chaining ---

    [Fact]
    public void AllMethods_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        var result = builder
            .ConnectionString(TestConnectionString)
            .SchemaName("audit")
            .TableName("events");
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void DataSourceFactory_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        builder.DataSourceFactory(_ => NpgsqlDataSource.Create(TestConnectionString)).ShouldBeSameAs(builder);
    }

    [Fact]
    public void DataSource_ReturnBuilderForChaining()
    {
        var (builder, _) = CreateBuilder();
        using var ds = NpgsqlDataSource.Create(TestConnectionString);
        builder.DataSource(ds).ShouldBeSameAs(builder);
    }

    // --- Constructor ---

    [Fact]
    public void Constructor_ThrowOnNullOptions()
    {
        Should.Throw<ArgumentNullException>(() => new PostgresAuditLoggingBuilder(null!));
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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SchemaName_ThrowOnInvalidValue(string? invalidValue)
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.SchemaName(invalidValue!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TableName_ThrowOnInvalidValue(string? invalidValue)
    {
        var (builder, _) = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.TableName(invalidValue!));
    }
}
