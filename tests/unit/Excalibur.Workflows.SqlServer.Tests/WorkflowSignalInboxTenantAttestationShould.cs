// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace Excalibur.Workflows.SqlServer.Tests;

/// <summary>
/// Binds both shipped workflow signal-inbox registrations to the dep-gated tenant-aware seam, so each can
/// attest the tenant scoping it actually performs.
/// </summary>
/// <remarks>
/// <para>
/// The mailbox contract is tenant-owned: a signal belongs to the tenant whose instance it addresses, and
/// both implementations compose the tenant into the key they admit on — the in-process one into its mailbox
/// key, the SQL Server one into <c>UNIQUE (TenantId, InstanceId, SignalId)</c>. Neither registration said
/// so. A multi-tenant host refuses any registered tenant-owned store that attests no mechanism, so the
/// framework's own default inbox was the thing that failed the gate.
/// </para>
/// <para>
/// <b>Both registrations are covered, and the pairing is the point.</b> Attesting from one alone would make
/// the in-process implementation the sole attested one while the durable, correct implementation stayed
/// unattested — the lying-marker shape inverted. These arms hold both.
/// </para>
/// <para>
/// The arms exercise registration only and resolve no connection, so no SQL Server is required.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class WorkflowSignalInboxTenantAttestationShould
{
    // Never contacted: no arm here opens a connection. It only has to parse.
    private const string DummyConnectionString =
        "Server=localhost;Database=workflows;Integrated Security=True;TrustServerCertificate=True";

    /// <summary>
    /// SAFETY: the in-process default attests the tenant scoping it performs.
    /// </summary>
    [Fact]
    public void AttestScopingForTheInProcessDefault()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddWorkflows();

        using var provider = services.BuildServiceProvider();

        provider.GetService<ITenantScopingCapability<IWorkflowSignalInbox>>().ShouldNotBeNull(
            "the in-process inbox keys its mailbox by tenant and requires an ambient context to do it, so "
            + "it must attest that mechanism through the seam that wires it. Registered plainly it attests "
            + "nothing, and a multi-tenant host is then refused for the framework's own default.");
    }

    /// <summary>
    /// LIVENESS: attesting did not cost the registration it describes.
    /// </summary>
    [Fact]
    public void StillResolveTheInProcessInboxItAdvertises()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddWorkflows();

        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredService<IWorkflowSignalInbox>().ShouldNotBeNull();
    }

    /// <summary>
    /// SAFETY: the durable SQL Server inbox attests the same mechanism, from its own registration.
    /// </summary>
    [Fact]
    public void AttestScopingForTheDurableInbox()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddWorkflows();
        _ = services.AddSqlServerWorkflowSignalInbox(o => o.ConnectionString = DummyConnectionString);

        using var provider = services.BuildServiceProvider();

        provider.GetService<ITenantScopingCapability<IWorkflowSignalInbox>>().ShouldNotBeNull(
            "the durable inbox carries the tenant in its unique key, so it must attest tenant scoping "
            + "through the seam that wires it, independently of whatever the default did.");
    }

    /// <summary>
    /// LIVENESS: the durable inbox still overrides the in-process default, and the attestation describes
    /// the instance the contract actually resolves to.
    /// </summary>
    /// <remarks>
    /// The seam registers the CONCRETE store, because that is the instance the marker is bound to. If the
    /// contract forwarded to a second construction, the attestation would describe an object the
    /// application never uses — and, worse here, the override that makes signals durable would be the thing
    /// that got lost.
    /// </remarks>
    [Fact]
    public void ResolveTheDurableInboxAndItsConcreteTypeToOneInstance()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddWorkflows();
        _ = services.AddSqlServerWorkflowSignalInbox(o => o.ConnectionString = DummyConnectionString);

        using var provider = services.BuildServiceProvider();

        var byContract = provider.GetRequiredService<IWorkflowSignalInbox>();

        _ = byContract.ShouldBeOfType<SqlServerWorkflowSignalInbox>(
            "the durable registration must still win over the in-process default it overrides.");
        byContract.ShouldBeSameAs(
            provider.GetRequiredService<SqlServerWorkflowSignalInbox>(),
            "the marker is bound to the instance the seam built; a second construction behind the contract "
            + "would leave the attested instance unused and the used instance unattested.");
    }

    /// <summary>
    /// LIVENESS for the requirement both attestations rest on: the tenant context is a REQUIRED
    /// constructor parameter on both implementations.
    /// </summary>
    /// <remarks>
    /// If it were optional, an inbox could be built having been handed nothing, silently widen to the
    /// untenanted partition, and still be registered through the seam — the marker would then attest a
    /// scoping that is not happening, which is worse than the missing marker these arms exist to prevent.
    /// </remarks>
    [Fact]
    public void RefuseConstructionWithoutATenantContext() =>
        _ = Should.Throw<ArgumentNullException>(() => new SqlServerWorkflowSignalInbox(
            Microsoft.Extensions.Options.Options.Create(
                new SqlServerWorkflowSignalInboxOptions { ConnectionString = DummyConnectionString }),
            null!));
}
