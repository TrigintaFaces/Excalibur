// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.Postgres;

using Npgsql;

namespace Excalibur.Data.Tests.Postgres.Builders.Outbox;

/// <summary>
/// Unit tests for the Sprint 768 upgrades to <see cref="PostgresOutboxBuilder"/> —
/// new ConnectionStringName, BindConfiguration, DataSource, DataSourceFactory overloads.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "Postgres")]
public sealed class PostgresOutboxBuilderUpgradeShould : UnitTestBase
{
    private const string TestConnectionString =
        "Host=localhost;Database=TestDb;Username=test;Password=test";

    private static PostgresOutboxBuilder CreateBuilder() =>
        new(new PostgresOutboxStoreOptions());

    // --- New connection overloads (happy path) ---

    [Fact]
    public void ConnectionStringName_StoreName()
    {
        var builder = CreateBuilder();
        builder.ConnectionStringName("OutboxStore");
        builder.ConnectionStringNameValue.ShouldBe("OutboxStore");
    }

    [Fact]
    public void BindConfiguration_StorePath()
    {
        var builder = CreateBuilder();
        builder.BindConfiguration("Outbox:Postgres");
        builder.BindConfigurationPath.ShouldBe("Outbox:Postgres");
    }

    [Fact]
    public void DataSource_StoreInstance()
    {
        var builder = CreateBuilder();
        using var dataSource = NpgsqlDataSource.Create(TestConnectionString);
        builder.DataSource(dataSource);
        builder.DataSourceInstance.ShouldBe(dataSource);
    }

    [Fact]
    public void DataSourceFactory_StoreFactory()
    {
        var builder = CreateBuilder();
        Func<IServiceProvider, NpgsqlDataSource> factory = _ => NpgsqlDataSource.Create(TestConnectionString);
        builder.DataSourceFactory(factory);
        builder.DataSourceFactoryFunc.ShouldBe(factory);
    }

    // --- Last-wins semantics for new overloads ---

    [Fact]
    public void ConnectionStringName_ClearAllOtherConnections()
    {
        var builder = CreateBuilder();
        builder.ConnectionString(TestConnectionString);
        builder.ConnectionStringName("OutboxStore");

        builder.ConfiguredConnectionString.ShouldBeNull();
        builder.ConfiguredDbFactory.ShouldBeNull();
        builder.DataSourceInstance.ShouldBeNull();
        builder.DataSourceFactoryFunc.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBeNull();
        builder.ConnectionStringNameValue.ShouldBe("OutboxStore");
    }

    [Fact]
    public void BindConfiguration_ClearAllOtherConnections()
    {
        var builder = CreateBuilder();
        using var ds = NpgsqlDataSource.Create(TestConnectionString);
        builder.DataSource(ds);
        builder.BindConfiguration("Outbox:Postgres");

        builder.ConfiguredConnectionString.ShouldBeNull();
        builder.ConfiguredDbFactory.ShouldBeNull();
        builder.DataSourceInstance.ShouldBeNull();
        builder.DataSourceFactoryFunc.ShouldBeNull();
        builder.ConnectionStringNameValue.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBe("Outbox:Postgres");
    }

    [Fact]
    public void DataSource_ClearAllOtherConnections()
    {
        var builder = CreateBuilder();
        using var ds = NpgsqlDataSource.Create(TestConnectionString);
        builder.ConnectionString(TestConnectionString);
        builder.DataSource(ds);

        builder.ConfiguredConnectionString.ShouldBeNull();
        builder.ConfiguredDbFactory.ShouldBeNull();
        builder.DataSourceFactoryFunc.ShouldBeNull();
        builder.ConnectionStringNameValue.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBeNull();
        builder.DataSourceInstance.ShouldBe(ds);
    }

    [Fact]
    public void DataSourceFactory_ClearAllOtherConnections()
    {
        var builder = CreateBuilder();
        builder.ConnectionString(TestConnectionString);
        Func<IServiceProvider, NpgsqlDataSource> factory = _ => NpgsqlDataSource.Create(TestConnectionString);
        builder.DataSourceFactory(factory);

        builder.ConfiguredConnectionString.ShouldBeNull();
        builder.ConfiguredDbFactory.ShouldBeNull();
        builder.DataSourceInstance.ShouldBeNull();
        builder.ConnectionStringNameValue.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBeNull();
        builder.DataSourceFactoryFunc.ShouldBe(factory);
    }

    // --- Validation guards for new overloads ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConnectionStringName_ThrowOnInvalidValue(string? invalidValue)
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.ConnectionStringName(invalidValue!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BindConfiguration_ThrowOnInvalidValue(string? invalidValue)
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.BindConfiguration(invalidValue!));
    }

    [Fact]
    public void DataSource_ThrowOnNull()
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentNullException>(() => builder.DataSource(null!));
    }

    [Fact]
    public void DataSourceFactory_ThrowOnNull()
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentNullException>(() => builder.DataSourceFactory(null!));
    }

    // --- Fluent chaining ---

    [Fact]
    public void NewOverloads_ReturnBuilderForChaining()
    {
        var builder = CreateBuilder();
        using var ds = NpgsqlDataSource.Create(TestConnectionString);

        builder.ConnectionStringName("OutboxStore").ShouldBeSameAs(builder);
        builder.BindConfiguration("Outbox:Postgres").ShouldBeSameAs(builder);
        builder.DataSource(ds).ShouldBeSameAs(builder);
        builder.DataSourceFactory(_ => NpgsqlDataSource.Create(TestConnectionString)).ShouldBeSameAs(builder);
    }
}
