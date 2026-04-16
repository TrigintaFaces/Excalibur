// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox;
using Excalibur.Outbox.SqlServer;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace Excalibur.Outbox.Tests.SqlServer.Builders;

/// <summary>
/// Unit tests for <see cref="SqlServerOutboxBuilder.EnableHealthChecks"/> (Sprint 770).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerOutboxBuilderHealthCheckShould
{
    private const string TestConnectionString =
        "Server=localhost;Database=TestDb;Trusted_Connection=True;";

    [Fact]
    public void EnableHealthChecks_SetFlagOnBuilder()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        SqlServerOutboxBuilder? captured = null;
        services.AddExcaliburOutbox(outbox =>
        {
            outbox.UseSqlServer(sql =>
            {
                sql.ConnectionString(TestConnectionString)
                   .EnableHealthChecks();
                captured = sql as SqlServerOutboxBuilder;
            });
        });

        // Assert
        captured.ShouldNotBeNull();
        captured!.HealthChecksEnabled.ShouldBeTrue();
        captured.HealthCheckName.ShouldBe("sqlserver-outbox");
    }

    [Fact]
    public void EnableHealthChecks_AcceptCustomName()
    {
        var services = new ServiceCollection();
        SqlServerOutboxBuilder? captured = null;

        services.AddExcaliburOutbox(outbox =>
        {
            outbox.UseSqlServer(sql =>
            {
                sql.ConnectionString(TestConnectionString)
                   .EnableHealthChecks("custom-outbox-check");
                captured = sql as SqlServerOutboxBuilder;
            });
        });

        captured.ShouldNotBeNull();
        captured!.HealthChecksEnabled.ShouldBeTrue();
        captured.HealthCheckName.ShouldBe("custom-outbox-check");
    }

    [Fact]
    public void EnableHealthChecks_IgnoreNullName()
    {
        var services = new ServiceCollection();
        SqlServerOutboxBuilder? captured = null;

        services.AddExcaliburOutbox(outbox =>
        {
            outbox.UseSqlServer(sql =>
            {
                sql.ConnectionString(TestConnectionString)
                   .EnableHealthChecks(null);
                captured = sql as SqlServerOutboxBuilder;
            });
        });

        captured.ShouldNotBeNull();
        captured!.HealthChecksEnabled.ShouldBeTrue();
        captured.HealthCheckName.ShouldBe("sqlserver-outbox");
    }

    [Fact]
    public void EnableHealthChecks_IgnoreWhitespaceName()
    {
        var services = new ServiceCollection();
        SqlServerOutboxBuilder? captured = null;

        services.AddExcaliburOutbox(outbox =>
        {
            outbox.UseSqlServer(sql =>
            {
                sql.ConnectionString(TestConnectionString)
                   .EnableHealthChecks("   ");
                captured = sql as SqlServerOutboxBuilder;
            });
        });

        captured.ShouldNotBeNull();
        captured!.HealthChecksEnabled.ShouldBeTrue();
        captured.HealthCheckName.ShouldBe("sqlserver-outbox");
    }

    [Fact]
    public void EnableHealthChecks_ReturnBuilderForChaining()
    {
        var services = new ServiceCollection();
        ISqlServerOutboxBuilder? chainResult = null;

        services.AddExcaliburOutbox(outbox =>
        {
            outbox.UseSqlServer(sql =>
            {
                chainResult = sql.ConnectionString(TestConnectionString)
                                 .EnableHealthChecks();
            });
        });

        chainResult.ShouldNotBeNull();
    }
}
