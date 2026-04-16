// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Inbox.DependencyInjection;
using Excalibur.Inbox.SqlServer;

namespace Excalibur.Data.Tests.SqlServer.Inbox.Builders;

/// <summary>
/// Unit tests for <see cref="SqlServerInboxBuilder.EnableHealthChecks"/> (Sprint 770).
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Database", "SqlServer")]
public sealed class SqlServerInboxBuilderHealthCheckShould : UnitTestBase
{
    private const string TestConnectionString =
        "Server=localhost;Database=TestDb;Integrated Security=true;TrustServerCertificate=true;";

    private sealed class TestInboxBuilder : IInboxBuilder
    {
        public IServiceCollection Services { get; } = new ServiceCollection();
    }

    [Fact]
    public void EnableHealthChecks_SetFlagOnBuilder()
    {
        // Arrange
        var inboxBuilder = new TestInboxBuilder();

        // Act
        SqlServerInboxBuilder? captured = null;
        inboxBuilder.UseSqlServer(sql =>
        {
            sql.ConnectionString(TestConnectionString)
               .EnableHealthChecks();

            // Access internal state via the builder (InternalsVisibleTo)
            captured = sql as SqlServerInboxBuilder;
        });

        // Assert
        captured.ShouldNotBeNull();
        captured!.HealthChecksEnabled.ShouldBeTrue();
        captured.HealthCheckName.ShouldBe("sqlserver-inbox");
    }

    [Fact]
    public void EnableHealthChecks_AcceptCustomName()
    {
        var inboxBuilder = new TestInboxBuilder();
        SqlServerInboxBuilder? captured = null;

        inboxBuilder.UseSqlServer(sql =>
        {
            sql.ConnectionString(TestConnectionString)
               .EnableHealthChecks("custom-inbox-check");
            captured = sql as SqlServerInboxBuilder;
        });

        captured.ShouldNotBeNull();
        captured!.HealthChecksEnabled.ShouldBeTrue();
        captured.HealthCheckName.ShouldBe("custom-inbox-check");
    }

    [Fact]
    public void EnableHealthChecks_IgnoreNullName()
    {
        var inboxBuilder = new TestInboxBuilder();
        SqlServerInboxBuilder? captured = null;

        inboxBuilder.UseSqlServer(sql =>
        {
            sql.ConnectionString(TestConnectionString)
               .EnableHealthChecks(null);
            captured = sql as SqlServerInboxBuilder;
        });

        captured.ShouldNotBeNull();
        captured!.HealthChecksEnabled.ShouldBeTrue();
        captured.HealthCheckName.ShouldBe("sqlserver-inbox"); // Default preserved
    }

    [Fact]
    public void EnableHealthChecks_IgnoreWhitespaceName()
    {
        var inboxBuilder = new TestInboxBuilder();
        SqlServerInboxBuilder? captured = null;

        inboxBuilder.UseSqlServer(sql =>
        {
            sql.ConnectionString(TestConnectionString)
               .EnableHealthChecks("   ");
            captured = sql as SqlServerInboxBuilder;
        });

        captured.ShouldNotBeNull();
        captured!.HealthChecksEnabled.ShouldBeTrue();
        captured.HealthCheckName.ShouldBe("sqlserver-inbox"); // Default preserved
    }

    [Fact]
    public void EnableHealthChecks_ReturnBuilderForChaining()
    {
        var inboxBuilder = new TestInboxBuilder();
        ISqlServerInboxBuilder? chainResult = null;

        inboxBuilder.UseSqlServer(sql =>
        {
            chainResult = sql.ConnectionString(TestConnectionString)
                             .EnableHealthChecks();
        });

        chainResult.ShouldNotBeNull();
    }
}
