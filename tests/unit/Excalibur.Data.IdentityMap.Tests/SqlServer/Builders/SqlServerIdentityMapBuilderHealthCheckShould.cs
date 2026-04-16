// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.IdentityMap.Builders;
using Excalibur.Data.IdentityMap.SqlServer;
using Excalibur.Data.IdentityMap.SqlServer.Builders;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace Excalibur.Data.IdentityMap.Tests.SqlServer.Builders;

/// <summary>
/// Unit tests for <see cref="SqlServerIdentityMapBuilder.EnableHealthChecks"/> (Sprint 770).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerIdentityMapBuilderHealthCheckShould
{
    private const string TestConnectionString =
        "Server=localhost;Database=TestDb;Integrated Security=true;TrustServerCertificate=true;";

    private sealed class TestIdentityMapBuilder : IIdentityMapBuilder
    {
        public IServiceCollection Services { get; } = new ServiceCollection();
    }

    [Fact]
    public void EnableHealthChecks_SetFlagOnBuilder()
    {
        var imBuilder = new TestIdentityMapBuilder();
        SqlServerIdentityMapBuilder? captured = null;

        imBuilder.UseSqlServer(sql =>
        {
            sql.ConnectionString(TestConnectionString)
               .EnableHealthChecks();
            captured = sql as SqlServerIdentityMapBuilder;
        });

        captured.ShouldNotBeNull();
        captured!.HealthChecksEnabled.ShouldBeTrue();
        captured.HealthCheckName.ShouldBe("sqlserver-identitymap");
    }

    [Fact]
    public void EnableHealthChecks_AcceptCustomName()
    {
        var imBuilder = new TestIdentityMapBuilder();
        SqlServerIdentityMapBuilder? captured = null;

        imBuilder.UseSqlServer(sql =>
        {
            sql.ConnectionString(TestConnectionString)
               .EnableHealthChecks("custom-im-check");
            captured = sql as SqlServerIdentityMapBuilder;
        });

        captured.ShouldNotBeNull();
        captured!.HealthChecksEnabled.ShouldBeTrue();
        captured.HealthCheckName.ShouldBe("custom-im-check");
    }

    [Fact]
    public void EnableHealthChecks_IgnoreNullName()
    {
        var imBuilder = new TestIdentityMapBuilder();
        SqlServerIdentityMapBuilder? captured = null;

        imBuilder.UseSqlServer(sql =>
        {
            sql.ConnectionString(TestConnectionString)
               .EnableHealthChecks(null);
            captured = sql as SqlServerIdentityMapBuilder;
        });

        captured.ShouldNotBeNull();
        captured!.HealthChecksEnabled.ShouldBeTrue();
        captured.HealthCheckName.ShouldBe("sqlserver-identitymap");
    }

    [Fact]
    public void EnableHealthChecks_IgnoreWhitespaceName()
    {
        var imBuilder = new TestIdentityMapBuilder();
        SqlServerIdentityMapBuilder? captured = null;

        imBuilder.UseSqlServer(sql =>
        {
            sql.ConnectionString(TestConnectionString)
               .EnableHealthChecks("   ");
            captured = sql as SqlServerIdentityMapBuilder;
        });

        captured.ShouldNotBeNull();
        captured!.HealthChecksEnabled.ShouldBeTrue();
        captured.HealthCheckName.ShouldBe("sqlserver-identitymap");
    }

    [Fact]
    public void EnableHealthChecks_ReturnBuilderForChaining()
    {
        var imBuilder = new TestIdentityMapBuilder();
        ISqlServerIdentityMapBuilder? chainResult = null;

        imBuilder.UseSqlServer(sql =>
        {
            chainResult = sql.ConnectionString(TestConnectionString)
                             .EnableHealthChecks();
        });

        chainResult.ShouldNotBeNull();
    }
}
