// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

using Shouldly;

using Xunit;

namespace Excalibur.Workflows.SqlServer.Tests;

/// <summary>
/// Binds the requirement that the tenant is part of signal identity: one tenant's signal can neither
/// satisfy another tenant's admission check nor appear in another tenant's drain.
/// </summary>
/// <remarks>
/// <para>
/// <b>The suppression arm is the one that matters, and it is not a disclosure.</b> Admission is decided by a
/// unique constraint, not by a <c>WHERE</c> clause. Keyed on <c>(InstanceId, SignalId)</c> alone, a second
/// tenant presenting the same pair raised a unique violation, the store read that as a producer redelivery,
/// and returned "not newly admitted" — so the signal was <em>silently discarded</em>. No row, no exception,
/// no log: the workflow simply waited forever for something the system had received and thrown away. The
/// drain leak is real too, and it exposes <c>PayloadJson</c>, which is producer-authored content — but a
/// disclosure leaves evidence and this does not.
/// </para>
/// <para>
/// <b>Liveness is not optional here, and it has two distinct halves that fail in opposite directions.</b> A
/// store that admitted <em>everything</em> would pass every safety arm below while destroying the
/// exactly-once guarantee the inbox exists to provide; a store that admitted <em>nothing</em> to the second
/// tenant would pass the isolation arms while reproducing the very bug. So each arm asserts both that the
/// foreign signal is kept out AND that the legitimate one still gets in — and
/// <see cref="StillRefuseAGenuineRedeliveryWithinOneTenant"/> is the arm that goes quiet if the key is
/// widened carelessly.
/// </para>
/// <para>
/// <b>verify-against-real-infra-not-mock:</b> real SQL Server (TestContainers), NON-SKIPPED. The property
/// under test is enforced by a UNIQUE constraint evaluated by the engine — there is no code path a mock
/// could stand in for, and a mocked connection would report this inbox isolated while the real one dropped
/// signals.
/// </para>
/// </remarks>
[Collection(SqlServerWorkflowSignalInboxTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerWorkflowSignalInboxTenantIsolationShould
{
    private const string SignalName = "OrderApproved";

    private readonly SqlServerWorkflowSignalInboxContainerFixture _fixture;

    public SqlServerWorkflowSignalInboxTenantIsolationShould(SqlServerWorkflowSignalInboxContainerFixture fixture)
        => _fixture = fixture;

    /// <summary>
    /// SAFETY: tenant B's signal must not be suppressed by tenant A having admitted the same
    /// <c>(instanceId, signalId)</c>. LIVENESS, same test: B's signal is genuinely readable afterwards, so
    /// the fix is not "return true and drop it".
    /// </summary>
    [Fact]
    public async Task AdmitTwoTenantsSignalsThatShareAnInstanceAndSignalId()
    {
        var ct = TestContext.Current.CancellationToken;
        await EnsureReadyAsync().ConfigureAwait(false);

        var instanceId = $"inst-{Guid.NewGuid():N}";
        const string SignalId = "signal-shared";

        var tenantA = InboxFor("tenant-a");
        var tenantB = InboxFor("tenant-b");

        var admittedForA = await tenantA.TryEnqueueAsync(instanceId, SignalId, SignalName, "{\"n\":1}", ct)
            .ConfigureAwait(false);
        admittedForA.ShouldBeTrue("the first admission of a fresh (instanceId, signalId) must succeed");

        // SAFETY: the admission decision must not treat B's signal as a redelivery of A's.
        var admittedForB = await tenantB.TryEnqueueAsync(instanceId, SignalId, SignalName, "{\"n\":2}", ct)
            .ConfigureAwait(false);
        admittedForB.ShouldBeTrue(
            "tenant B has never sent this signal; reporting it as already-admitted discards it silently and "
            + "its workflow waits forever for a signal the system received and threw away");

        // LIVENESS: and it was actually stored, not merely reported as admitted.
        var drainedByB = await tenantB.DrainAsync(instanceId, ct).ConfigureAwait(false);
        drainedByB.Count.ShouldBe(1, "tenant B must be able to drain the signal it just admitted");
        drainedByB[0].PayloadJson.ShouldBe("{\"n\":2}", "tenant B must drain its own payload, not tenant A's");
    }

    /// <summary>
    /// SAFETY: the drain must not return another tenant's signals — the content-disclosure half, since
    /// <c>PayloadJson</c> is producer-authored. LIVENESS, same test: tenant A still drains its own.
    /// </summary>
    [Fact]
    public async Task NotDrainAnotherTenantsSignalsForTheSameInstance()
    {
        var ct = TestContext.Current.CancellationToken;
        await EnsureReadyAsync().ConfigureAwait(false);

        var instanceId = $"inst-{Guid.NewGuid():N}";

        var tenantA = InboxFor("tenant-a");
        var tenantB = InboxFor("tenant-b");

        _ = await tenantA.TryEnqueueAsync(instanceId, "signal-a1", SignalName, "{\"owner\":\"a\"}", ct)
            .ConfigureAwait(false);
        _ = await tenantA.TryEnqueueAsync(instanceId, "signal-a2", SignalName, "{\"owner\":\"a\"}", ct)
            .ConfigureAwait(false);

        // SAFETY: B has admitted nothing under this instance and must see nothing.
        var drainedByB = await tenantB.DrainAsync(instanceId, ct).ConfigureAwait(false);
        drainedByB.ShouldBeEmpty(
            "tenant B admitted no signal for this instance; returning tenant A's exposes PayloadJson, which "
            + "is producer-authored content");

        // LIVENESS: A still drains its own two, so the isolation is not achieved by returning nothing to
        // anybody — which would satisfy the assertion above and break the inbox entirely.
        var drainedByA = await tenantA.DrainAsync(instanceId, ct).ConfigureAwait(false);
        drainedByA.Count.ShouldBe(2, "tenant A must still drain the signals it admitted");
        drainedByA.Select(entry => entry.SignalId).ShouldBe(["signal-a1", "signal-a2"], ignoreOrder: false);
    }

    /// <summary>
    /// LIVENESS, and the arm that goes silent if the key is widened carelessly: deduplication WITHIN a tenant
    /// must still work. Widening the constraint must make two tenants distinguishable — it must not make a
    /// tenant's own redelivery admissible.
    /// </summary>
    /// <remarks>
    /// This is the arm that fails if someone "fixes" the isolation by dropping the uniqueness constraint, or
    /// by keying it on the tenant alone. Both would pass every safety arm in this file while silently
    /// breaking exactly-once signal delivery — the guarantee the inbox exists to provide.
    /// </remarks>
    [Fact]
    public async Task StillRefuseAGenuineRedeliveryWithinOneTenant()
    {
        var ct = TestContext.Current.CancellationToken;
        await EnsureReadyAsync().ConfigureAwait(false);

        var instanceId = $"inst-{Guid.NewGuid():N}";
        const string SignalId = "signal-redelivered";

        var tenantA = InboxFor("tenant-a");

        var firstAdmit = await tenantA.TryEnqueueAsync(instanceId, SignalId, SignalName, "{\"n\":1}", ct)
            .ConfigureAwait(false);
        firstAdmit.ShouldBeTrue("the first admission must succeed");

        var redelivery = await tenantA.TryEnqueueAsync(instanceId, SignalId, SignalName, "{\"n\":1}", ct)
            .ConfigureAwait(false);
        redelivery.ShouldBeFalse(
            "the SAME tenant redelivering the SAME (instanceId, signalId) is a duplicate and must still be "
            + "refused; adding the tenant term must distinguish tenants, not weaken deduplication");

        // And it left exactly one row, so "refused" means refused rather than silently upserted.
        var drained = await tenantA.DrainAsync(instanceId, ct).ConfigureAwait(false);
        drained.Count.ShouldBe(1, "a refused redelivery must not have written a second row");
    }

    /// <summary>
    /// LIVENESS for the untenanted partition: a host that never registered multi-tenancy resolves the
    /// reserved sentinel and must work exactly as before — admitting, deduplicating, and draining its own.
    /// </summary>
    /// <remarks>
    /// Without this arm the whole change could be satisfied by a store that only functions for a scoped
    /// tenant, which would silently break every single-tenant consumer — the majority of them.
    /// </remarks>
    [Fact]
    public async Task StillServeAnUntenantedHostAsTheOneCanonicalTenant()
    {
        var ct = TestContext.Current.CancellationToken;
        await EnsureReadyAsync().ConfigureAwait(false);

        var instanceId = $"inst-{Guid.NewGuid():N}";
        const string SignalId = "signal-untenanted";

        // The framework default context: HasTenant is false and the partition resolves to the reserved
        // sentinel rather than to an empty term.
        var untenanted = new SqlServerWorkflowSignalInbox(
            () => _fixture.CreateConnection(),
            CreateOptions(),
            new UntenantedContext());

        (await untenanted.TryEnqueueAsync(instanceId, SignalId, SignalName, "{\"n\":1}", ct).ConfigureAwait(false))
            .ShouldBeTrue("an untenanted host must still admit signals");

        (await untenanted.TryEnqueueAsync(instanceId, SignalId, SignalName, "{\"n\":1}", ct).ConfigureAwait(false))
            .ShouldBeFalse("an untenanted host must still deduplicate its own redelivery");

        var drained = await untenanted.DrainAsync(instanceId, ct).ConfigureAwait(false);
        drained.Count.ShouldBe(1, "an untenanted host must drain its own signals");

        // SAFETY: and the untenanted partition is not a wildcard — a scoped tenant does not see its rows.
        (await InboxFor("tenant-a").DrainAsync(instanceId, ct).ConfigureAwait(false))
            .ShouldBeEmpty("the untenanted partition must not be visible to a scoped tenant");
    }

    private async Task EnsureReadyAsync()
    {
        _fixture.DockerAvailable.ShouldBeTrue(
            "cross-tenant signal suppression discards a delivered signal with no error and no row left "
            + "behind — this real-SqlServer lock must never be skipped");
        await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
    }

    private SqlServerWorkflowSignalInbox InboxFor(string tenantId) =>
        new(() => _fixture.CreateConnection(), CreateOptions(), new FixedTenantContext(tenantId));

    private SqlServerWorkflowSignalInboxOptions CreateOptions() => new()
    {
        ConnectionString = _fixture.ConnectionString,
        SchemaName = _fixture.SchemaName,
        TableName = _fixture.TableName
    };

    /// <summary>
    /// A tenant context fixed to one identity. Two inboxes differing ONLY in this are what make the tenant
    /// the sole variable across the arms above.
    /// </summary>
    private sealed class FixedTenantContext(string tenantId) : ITenantContext
    {
        public string? TenantId => tenantId;

        public bool HasTenant => true;
    }

    /// <summary>
    /// The shape a single-tenant host presents: no tenant resolved, which the keyed partition maps onto the
    /// reserved untenanted sentinel rather than onto an empty term.
    /// </summary>
    private sealed class UntenantedContext : ITenantContext
    {
        public string? TenantId => TenantScope.UntenantedSentinel;

        public bool HasTenant => false;
    }
}
