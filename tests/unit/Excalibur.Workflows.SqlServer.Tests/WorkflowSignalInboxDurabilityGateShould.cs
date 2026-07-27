// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Shouldly;

using Xunit;

namespace Excalibur.Workflows.SqlServer.Tests;

/// <summary>
/// Pure-DI (no container) regression lock for the opt-in fail-fast durability gate wired by
/// <c>RequireDurableSignalInbox()</c>, and for the capability marker's inseparability from the durable
/// wiring (S886 rw2ull class: a marker must never be present without the store it attests).
/// </summary>
/// <remarks>
/// These arms exercise <em>registration</em> only — the startup guard probes
/// <c>IServiceProviderIsService</c> and never resolves the inbox — so no SQL Server is required.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class WorkflowSignalInboxDurabilityGateShould
{
    private const string DummyConnectionString =
        "Server=localhost;Database=workflows;User Id=sa;Password=Test@Pass123;TrustServerCertificate=True";

    /// <summary>
    /// SAFETY: with only the in-memory default wired, <c>RequireDurableSignalInbox()</c> makes host start
    /// FAIL — the startup validator's StartAsync throws because the durability marker is absent.
    /// </summary>
    [Fact]
    public async Task Fail_Host_Start_When_Only_The_In_Memory_Default_Is_Wired()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        // A real host always registers logging; this container is the only place it is absent. Without it
        // the SqlServer schema guard cannot be activated (ILogger<T> is unresolvable) and BOTH arms report
        // that instead of the property they exist to test -- the liveness arm fails on it, and the safety
        // arm would pass on it, throwing for an accidental reason rather than the missing durability marker.
        _ = services.AddWorkflows();
        _ = services.RequireDurableSignalInbox();

        await using var provider = services.BuildServiceProvider();

        _ = await Should.ThrowAsync<InvalidOperationException>(
            async () => await StartAllHostedServicesAsync(provider).ConfigureAwait(false));
    }

    /// <summary>
    /// LIVENESS: the same requirement plus <c>AddSqlServerWorkflowSignalInbox()</c> does NOT throw at start,
    /// AND the <see cref="IWorkflowSignalDurability"/> marker IS resolvable — marker present ⇔ durable wired.
    /// </summary>
    [Fact]
    public async Task Start_Cleanly_And_Expose_The_Durability_Marker_When_The_Durable_Inbox_Is_Wired()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddWorkflows();
        _ = services.AddSqlServerWorkflowSignalInbox(options => options.ConnectionString = DummyConnectionString);
        _ = services.RequireDurableSignalInbox();

        await using var provider = services.BuildServiceProvider();

        // LIVENESS: the permitted configuration is not rejected BY THE DURABILITY GATE.
        //
        // Asserted as "the gate does not fire" rather than "nothing throws at all", because this is a UNIT
        // test project and StartAsync also runs the SqlServer schema guard, which opens a real connection
        // using DummyConnectionString. A blanket NotThrow therefore passed only on a machine where some
        // SQL Server happened to answer on localhost:1433 with those exact credentials — it bound the
        // developer's environment, not the product. Here it surfaced as SqlException "Login failed for
        // user 'sa'" while a SQL Server container was in fact running: someone else's, with other
        // credentials. That is an infrastructure fact and says nothing about the gate.
        //
        // InvalidOperationException is precisely the gate's own signal — the safety arm above asserts the
        // gate raises it when the durable inbox is absent. So "no InvalidOperationException" is the exact
        // complement of that arm, and a regression that made the gate reject a correctly-wired host still
        // fails here. Unreachable infrastructure no longer can.
        var startFailure = await Record.ExceptionAsync(
            async () => await StartAllHostedServicesAsync(provider).ConfigureAwait(false))
            .ConfigureAwait(false);

        startFailure.ShouldNotBeOfType<InvalidOperationException>(
            "the durability gate must ACCEPT a host where AddSqlServerWorkflowSignalInbox() is wired. An "
            + "InvalidOperationException here is the gate rejecting a configuration it is meant to permit, "
            + "which would make the durable inbox impossible to satisfy.");

        // The marker is inseparable from the durable wiring: its presence attests the durable inbox was wired.
        provider.GetService<IWorkflowSignalDurability>().ShouldNotBeNull(
            "AddSqlServerWorkflowSignalInbox() must register the durability marker in the same call as the " +
            "durable inbox — marker present if and only if the durable store was wired.");

        // The wired inbox is the durable SQL Server implementation, not the in-memory default.
        provider.GetRequiredService<IWorkflowSignalInbox>().ShouldBeOfType<SqlServerWorkflowSignalInbox>();
    }

    private static async Task StartAllHostedServicesAsync(IServiceProvider provider)
    {
        foreach (var hostedService in provider.GetServices<IHostedService>())
        {
            await hostedService.StartAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }
}
