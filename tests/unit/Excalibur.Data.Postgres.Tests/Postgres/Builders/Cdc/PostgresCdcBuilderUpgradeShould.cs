// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.Postgres;

using Npgsql;

namespace Excalibur.Data.Tests.Postgres.Builders.Cdc;

/// <summary>
/// Unit tests for the Sprint 768 upgrades to <see cref="PostgresCdcBuilder"/> —
/// new DataSource, DataSourceFactory overloads plus existing feature methods.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "Postgres")]
public sealed class PostgresCdcBuilderUpgradeShould : UnitTestBase
{
    private const string TestConnectionString =
        "Host=localhost;Database=TestDb;Username=test;Password=test";

    private static PostgresCdcBuilder CreateBuilder() =>
        new(new PostgresCdcOptions(), new PostgresCdcStateStoreOptions());

    // --- New DataSource/DataSourceFactory overloads ---

    [Fact]
    public void DataSource_StoreInstance()
    {
        var builder = CreateBuilder();
        using var ds = NpgsqlDataSource.Create(TestConnectionString);
        builder.DataSource(ds);
        builder.DataSourceInstance.ShouldBe(ds);
        builder.DataSourceFactoryFunc.ShouldBeNull();
    }

    [Fact]
    public void DataSourceFactory_StoreFactory()
    {
        var builder = CreateBuilder();
        Func<IServiceProvider, NpgsqlDataSource> factory = _ => NpgsqlDataSource.Create(TestConnectionString);
        builder.DataSourceFactory(factory);
        builder.DataSourceFactoryFunc.ShouldBe(factory);
        builder.DataSourceInstance.ShouldBeNull();
    }

    [Fact]
    public void DataSource_ClearDataSourceFactory()
    {
        var builder = CreateBuilder();
        builder.DataSourceFactory(_ => NpgsqlDataSource.Create(TestConnectionString));
        using var ds = NpgsqlDataSource.Create(TestConnectionString);
        builder.DataSource(ds);
        builder.DataSourceInstance.ShouldBe(ds);
        builder.DataSourceFactoryFunc.ShouldBeNull();
    }

    [Fact]
    public void DataSourceFactory_ClearDataSourceInstance()
    {
        var builder = CreateBuilder();
        using var ds = NpgsqlDataSource.Create(TestConnectionString);
        builder.DataSource(ds);
        Func<IServiceProvider, NpgsqlDataSource> factory = _ => NpgsqlDataSource.Create(TestConnectionString);
        builder.DataSourceFactory(factory);
        builder.DataSourceInstance.ShouldBeNull();
        builder.DataSourceFactoryFunc.ShouldBe(factory);
    }

    // --- Validation guards ---

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
    public void DataSource_ReturnBuilderForChaining()
    {
        var builder = CreateBuilder();
        using var ds = NpgsqlDataSource.Create(TestConnectionString);
        builder.DataSource(ds).ShouldBeSameAs(builder);
    }

    [Fact]
    public void DataSourceFactory_ReturnBuilderForChaining()
    {
        var builder = CreateBuilder();
        builder.DataSourceFactory(_ => NpgsqlDataSource.Create(TestConnectionString)).ShouldBeSameAs(builder);
    }

    // --- Existing feature methods coverage ---

    [Fact]
    public void BatchSize_ThrowOnZero()
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentOutOfRangeException>(() => builder.BatchSize(0));
    }

    [Fact]
    public void BatchSize_ThrowOnNegative()
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentOutOfRangeException>(() => builder.BatchSize(-1));
    }

    [Fact]
    public void PollingInterval_ThrowOnZero()
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentOutOfRangeException>(() => builder.PollingInterval(TimeSpan.Zero));
    }

    [Fact]
    public void Timeout_ThrowOnNegative()
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentOutOfRangeException>(() => builder.Timeout(TimeSpan.FromSeconds(-1)));
    }

    // --- Constructor ---

    [Fact]
    public void Constructor_ThrowOnNullOptions()
    {
        Should.Throw<ArgumentNullException>(() => new PostgresCdcBuilder(null!, new PostgresCdcStateStoreOptions()));
    }

    [Fact]
    public void Constructor_ThrowOnNullStateStoreOptions()
    {
        Should.Throw<ArgumentNullException>(() => new PostgresCdcBuilder(new PostgresCdcOptions(), null!));
    }
}