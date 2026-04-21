// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.LeaderElection.Postgres;

namespace Excalibur.Data.Tests.Postgres.Builders.LeaderElection;

/// <summary>
/// Unit tests for <see cref="PostgresLeaderElectionBuilder"/> argument validation guards.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "Postgres")]
public sealed class PostgresLeaderElectionBuilderValidationShould : UnitTestBase
{
    private static PostgresLeaderElectionBuilder CreateBuilder() =>
        new(new PostgresLeaderElectionOptions());

    // --- ConnectionString guards ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConnectionString_ThrowOnInvalidValue(string? invalidValue)
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.ConnectionString(invalidValue!));
    }

    // --- DataSourceFactory guards ---

    [Fact]
    public void DataSourceFactory_ThrowOnNull()
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentNullException>(() =>
            builder.DataSourceFactory(null!));
    }

    // --- DataSource guards ---

    [Fact]
    public void DataSource_ThrowOnNull()
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentNullException>(() =>
            builder.DataSource(null!));
    }

    // --- ConnectionStringName guards ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConnectionStringName_ThrowOnInvalidValue(string? invalidValue)
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.ConnectionStringName(invalidValue!));
    }

    // --- BindConfiguration guards ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BindConfiguration_ThrowOnInvalidValue(string? invalidValue)
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentException>(() => builder.BindConfiguration(invalidValue!));
    }

    // --- LockKey guards ---

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void LockKey_ThrowOnInvalidValue(long invalidValue)
    {
        var builder = CreateBuilder();
        Should.Throw<ArgumentOutOfRangeException>(() => builder.LockKey(invalidValue));
    }
}
