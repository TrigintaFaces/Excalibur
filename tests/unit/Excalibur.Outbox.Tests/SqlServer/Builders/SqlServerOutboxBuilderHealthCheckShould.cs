// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Outbox;
using Excalibur.Outbox.SqlServer;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

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

    // ---------------------------------------------------------------------------------------------
    // The arms above assert the BUILDER FLAG and the NAME. They are green whichever check the
    // registration ends up adding, so they could not see the defect below and must not be read as
    // covering it: the registered check used to be the stock connectivity probe wearing the outbox's
    // name. A database that answered while the outbox table was missing a column the drain binds
    // reported Healthy forever.
    //
    // These two arms bind the EFFECT instead -- that running the check named for the outbox reads the
    // outbox STORE. The mutation that reddens them leaves every type in place and severs only the
    // wiring (restore `.AddSqlServer(sqlOptions.ConnectionString, ...)` in place of
    // `.AddOutboxStoreHealthCheck(...)`): the fake store is then never consulted, which is exactly
    // what shipped.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task RegisterAHealthCheckThatReadsTheOutboxStore()
    {
        // Arrange -- register the store FIRST so the provider extension's TryAddKeyedSingleton keeps it.
        var store = A.Fake<Excalibur.Dispatch.IOutboxStore>(o => o.Implements<Excalibur.Dispatch.IOutboxStoreAdmin>());
        _ = A.CallTo(() => ((Excalibur.Dispatch.IOutboxStoreAdmin)store).GetAllTenantsStatisticsAsync(A<CancellationToken>._))
            .Returns(new Excalibur.Dispatch.OutboxStatistics());

        var check = ResolveRegisteredOutboxCheck(store, out var provider);
        using (provider)
        {
            // Act
            var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

            // Assert -- SAFETY: the store was actually consulted.
            A.CallTo(() => ((Excalibur.Dispatch.IOutboxStoreAdmin)store).GetAllTenantsStatisticsAsync(A<CancellationToken>._))
                .MustHaveHappened();

            // LIVENESS: and a store that answers is reported Healthy. Without this arm the safety half
            // is satisfied by a check that reads the store and then fails no matter what it is told,
            // which is the opposite dishonest surface.
            result.Status.ShouldBe(
                HealthStatus.Healthy,
                "a reachable, answering outbox store must report Healthy; a check that always fails is "
                + "as useless to an operator as one that always passes.");
        }
    }

    [Fact]
    public async Task ReportUnhealthyWhenTheOutboxStoreCannotAnswer()
    {
        // Arrange -- the store is reachable as a service but broken as a store, which is the state the
        // connectivity probe could not distinguish from a working one.
        var store = A.Fake<Excalibur.Dispatch.IOutboxStore>(o => o.Implements<Excalibur.Dispatch.IOutboxStoreAdmin>());
        _ = A.CallTo(() => ((Excalibur.Dispatch.IOutboxStoreAdmin)store).GetAllTenantsStatisticsAsync(A<CancellationToken>._))
            .Throws(new InvalidOperationException("Invalid column name 'ErrorMessage'."));

        var check = ResolveRegisteredOutboxCheck(store, out var provider);
        using (provider)
        {
            // Act
            var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

            // Assert
            result.Status.ShouldBe(
                HealthStatus.Unhealthy,
                "this is the whole point of the migration: a store that cannot serve the drain must not "
                + "report Healthy. The connectivity probe reported Healthy here, because the database "
                + "answered.");
        }
    }

    // Builds the registration through the real provider extension and returns the check registered
    // under the outbox's own name -- not a hand-constructed one, so the arm binds what a consumer gets.
    private static IHealthCheck ResolveRegisteredOutboxCheck(
        Excalibur.Dispatch.IOutboxStore store,
        out ServiceProvider provider)
    {
        var services = new ServiceCollection();
        _ = services.AddKeyedSingleton<Excalibur.Dispatch.IOutboxStore>("default", (_, _) => store);

        _ = services.AddExcaliburOutbox(outbox =>
            outbox.UseSqlServer(sql => sql.ConnectionString(TestConnectionString).EnableHealthChecks()));

        provider = services.BuildServiceProvider();

        var registration = provider
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>()
            .Value.Registrations.Single(r => r.Name == "sqlserver-outbox");

        return registration.Factory(provider);
    }
}
