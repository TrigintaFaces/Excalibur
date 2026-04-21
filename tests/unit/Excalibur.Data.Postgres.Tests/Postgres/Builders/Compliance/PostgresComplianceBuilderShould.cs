// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance.Postgres;
using Excalibur.Compliance.Postgres.Erasure;

using Npgsql;

namespace Excalibur.Data.Tests.Postgres.Builders.Compliance;

/// <summary>
/// Unit tests for <see cref="PostgresComplianceBuilder"/> — 5 connection overloads,
/// last-wins semantics, fluent chaining, and multi-store connection propagation.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, "Compliance")]
[Trait("Database", "Postgres")]
public sealed class PostgresComplianceBuilderShould : UnitTestBase
{
    private const string TestConnectionString =
        "Host=localhost;Database=TestDb;Username=test;Password=test";

    private static (PostgresComplianceBuilder Builder, PostgresErasureStoreOptions Erasure, PostgresDataInventoryStoreOptions Inventory, PostgresLegalHoldStoreOptions LegalHold) CreateBuilder()
    {
        var erasure = new PostgresErasureStoreOptions();
        var inventory = new PostgresDataInventoryStoreOptions();
        var legalHold = new PostgresLegalHoldStoreOptions();
        var builder = new PostgresComplianceBuilder(erasure, inventory, legalHold);
        return (builder, erasure, inventory, legalHold);
    }

    // --- ConnectionString sets ALL three sub-store options ---

    [Fact]
    public void ConnectionString_SetOnAllThreeSubStores()
    {
        var (builder, erasure, inventory, legalHold) = CreateBuilder();
        builder.ConnectionString(TestConnectionString);

        erasure.ConnectionString.ShouldBe(TestConnectionString);
        inventory.ConnectionString.ShouldBe(TestConnectionString);
        legalHold.ConnectionString.ShouldBe(TestConnectionString);
    }

    // --- Other overloads clear ConnectionString on all sub-stores ---

    [Fact]
    public void DataSourceFactory_ClearAllSubStoreConnectionStrings()
    {
        var (builder, erasure, inventory, legalHold) = CreateBuilder();
        builder.ConnectionString(TestConnectionString);
        builder.DataSourceFactory(_ => NpgsqlDataSource.Create(TestConnectionString));

        erasure.ConnectionString.ShouldBeNull();
        inventory.ConnectionString.ShouldBeNull();
        legalHold.ConnectionString.ShouldBeNull();
        builder.DataSourceFactoryFunc.ShouldNotBeNull();
    }

    [Fact]
    public void DataSource_ClearAllSubStoreConnectionStrings()
    {
        var (builder, erasure, inventory, legalHold) = CreateBuilder();
        using var ds = NpgsqlDataSource.Create(TestConnectionString);
        builder.ConnectionString(TestConnectionString);
        builder.DataSource(ds);

        erasure.ConnectionString.ShouldBeNull();
        inventory.ConnectionString.ShouldBeNull();
        legalHold.ConnectionString.ShouldBeNull();
        builder.DataSourceInstance.ShouldBe(ds);
    }

    [Fact]
    public void ConnectionStringName_ClearAllSubStoreConnectionStrings()
    {
        var (builder, erasure, inventory, legalHold) = CreateBuilder();
        builder.ConnectionString(TestConnectionString);
        builder.ConnectionStringName("ComplianceDb");

        erasure.ConnectionString.ShouldBeNull();
        inventory.ConnectionString.ShouldBeNull();
        legalHold.ConnectionString.ShouldBeNull();
        builder.ConnectionStringNameValue.ShouldBe("ComplianceDb");
    }

    [Fact]
    public void BindConfiguration_ClearAllSubStoreConnectionStrings()
    {
        var (builder, erasure, inventory, legalHold) = CreateBuilder();
        builder.ConnectionString(TestConnectionString);
        builder.BindConfiguration("Compliance:Postgres");

        erasure.ConnectionString.ShouldBeNull();
        inventory.ConnectionString.ShouldBeNull();
        legalHold.ConnectionString.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBe("Compliance:Postgres");
    }

    // --- Last-wins semantics ---

    [Fact]
    public void ConnectionString_ClearDataSource()
    {
        var (builder, erasure, _, _) = CreateBuilder();
        using var ds = NpgsqlDataSource.Create(TestConnectionString);
        builder.DataSource(ds);
        builder.ConnectionString(TestConnectionString);

        erasure.ConnectionString.ShouldBe(TestConnectionString);
        builder.DataSourceInstance.ShouldBeNull();
        builder.DataSourceFactoryFunc.ShouldBeNull();
        builder.ConnectionStringNameValue.ShouldBeNull();
        builder.BindConfigurationPath.ShouldBeNull();
    }

    // --- Fluent chaining ---

    [Fact]
    public void AllMethods_ReturnBuilderForChaining()
    {
        var (builder, _, _, _) = CreateBuilder();
        using var ds = NpgsqlDataSource.Create(TestConnectionString);

        builder.ConnectionString(TestConnectionString).ShouldBeSameAs(builder);
        builder.DataSourceFactory(_ => NpgsqlDataSource.Create(TestConnectionString)).ShouldBeSameAs(builder);
        builder.DataSource(ds).ShouldBeSameAs(builder);
        builder.ConnectionStringName("ComplianceDb").ShouldBeSameAs(builder);
        builder.BindConfiguration("Compliance:Postgres").ShouldBeSameAs(builder);
    }

    // --- Constructor guards ---

    [Fact]
    public void Constructor_ThrowOnNullErasureOptions()
    {
        Should.Throw<ArgumentNullException>(() =>
            new PostgresComplianceBuilder(null!, new PostgresDataInventoryStoreOptions(), new PostgresLegalHoldStoreOptions()));
    }

    [Fact]
    public void Constructor_ThrowOnNullInventoryOptions()
    {
        Should.Throw<ArgumentNullException>(() =>
            new PostgresComplianceBuilder(new PostgresErasureStoreOptions(), null!, new PostgresLegalHoldStoreOptions()));
    }

    [Fact]
    public void Constructor_ThrowOnNullLegalHoldOptions()
    {
        Should.Throw<ArgumentNullException>(() =>
            new PostgresComplianceBuilder(new PostgresErasureStoreOptions(), new PostgresDataInventoryStoreOptions(), null!));
    }

    // --- Validation guards ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConnectionString_ThrowOnInvalidValue(string? invalidValue)
    {
        var (builder, _, _, _) = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.ConnectionString(invalidValue!));
    }

    [Fact]
    public void DataSourceFactory_ThrowOnNull()
    {
        var (builder, _, _, _) = CreateBuilder();
        Should.Throw<ArgumentNullException>(() => builder.DataSourceFactory(null!));
    }

    [Fact]
    public void DataSource_ThrowOnNull()
    {
        var (builder, _, _, _) = CreateBuilder();
        Should.Throw<ArgumentNullException>(() => builder.DataSource(null!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConnectionStringName_ThrowOnInvalidValue(string? invalidValue)
    {
        var (builder, _, _, _) = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.ConnectionStringName(invalidValue!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BindConfiguration_ThrowOnInvalidValue(string? invalidValue)
    {
        var (builder, _, _, _) = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.BindConfiguration(invalidValue!));
    }
}
