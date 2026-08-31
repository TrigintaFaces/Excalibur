// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

namespace Excalibur.Workflows.Tests;

/// <summary>
/// Regression lock for the in-process signal inbox's tenant term. Both <c>instanceId</c> and
/// <c>signalId</c> are producer-supplied strings, unique only within the system that issued them, so two
/// tenants routinely present the same pair. Keyed without the tenant they share one mailbox, and the
/// admission half leaves nothing behind: the second tenant's signal fails the deduplication check, is
/// reported "not newly admitted", and is discarded — not stored, not logged, not errored — so its workflow
/// waits forever for a signal the system received and threw away.
/// </summary>
/// <remarks>
/// Both arms are required and neither is redundant. The isolation arm alone is satisfied by an inbox that
/// admits nothing and drains nothing to anybody; the liveness arm is what fails against that. The dedup arm
/// guards the opposite error — a widened key that stops deduplicating <em>within</em> a tenant, which would
/// trade a silent drop for a duplicate side-effect.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Workflows")]
public sealed class WorkflowSignalInboxTenantIsolationShould
{
    private const string InstanceId = "order-1042";
    private const string SignalId = "approval-1";
    private const string SignalName = "Approved";

    [Fact]
    public async Task AdmitAndDrainEachTenantSeparately_WhenTwoTenantsShareOneInstanceAndSignalId()
    {
        // One registered inbox serving every caller, as in production: the tenant is re-resolved per call.
        var tenant = new MutableTenantContext();
        var inbox = new InMemoryWorkflowSignalInbox(tenant);

        tenant.TenantId = "tenant-a";
        var admittedA = await inbox.TryEnqueueAsync(
            InstanceId, SignalId, SignalName, """{"by":"a"}""", TestContext.Current.CancellationToken);

        tenant.TenantId = "tenant-b";
        var admittedB = await inbox.TryEnqueueAsync(
            InstanceId, SignalId, SignalName, """{"by":"b"}""", TestContext.Current.CancellationToken);

        // SAFETY: tenant B's signal is a distinct signal, not tenant A's redelivery. Without the tenant
        // term this is false and the signal is gone with nothing raised.
        admittedA.ShouldBeTrue("tenant A's signal is the first for its own mailbox");
        admittedB.ShouldBeTrue(
            "tenant B's signal shares only a producer-supplied id with tenant A's and must not be " +
            "mistaken for a redelivery of it");

        tenant.TenantId = "tenant-a";
        var drainedA = await inbox.DrainAsync(InstanceId, TestContext.Current.CancellationToken);

        tenant.TenantId = "tenant-b";
        var drainedB = await inbox.DrainAsync(InstanceId, TestContext.Current.CancellationToken);

        // LIVENESS: each tenant still receives its OWN signal. An inbox that dropped everything would
        // satisfy the isolation assertions above and fail here.
        drainedA.ShouldHaveSingleItem().PayloadJson.ShouldBe("""{"by":"a"}""");
        drainedB.ShouldHaveSingleItem().PayloadJson.ShouldBe("""{"by":"b"}""");
    }

    [Fact]
    public async Task AdmitDrainAndStillDeduplicate_ForASingleUntenantedHost()
    {
        var inbox = new InMemoryWorkflowSignalInbox(UntenantedContext.Instance);

        // LIVENESS: the ordinary single-tenant round trip still works.
        var admitted = await inbox.TryEnqueueAsync(
            InstanceId, SignalId, SignalName, """{"by":"host"}""", TestContext.Current.CancellationToken);
        admitted.ShouldBeTrue();

        var drained = await inbox.DrainAsync(InstanceId, TestContext.Current.CancellationToken);
        var entry = drained.ShouldHaveSingleItem();
        entry.SignalId.ShouldBe(SignalId);
        entry.SignalName.ShouldBe(SignalName);
        entry.PayloadJson.ShouldBe("""{"by":"host"}""");

        // Widening the key must not weaken deduplication: a redelivery WITHIN one tenant still collides.
        var redelivered = await inbox.TryEnqueueAsync(
            InstanceId, SignalId, SignalName, """{"by":"host"}""", TestContext.Current.CancellationToken);
        redelivered.ShouldBeFalse("a redelivery within one tenant is still a duplicate");

        (await inbox.DrainAsync(InstanceId, TestContext.Current.CancellationToken)).Count.ShouldBe(1);
    }

    /// <summary>
    /// A tenant context whose resolved tenant changes between calls, so one inbox instance can be addressed
    /// by two tenants in a single test — the shape a registered singleton actually sees.
    /// </summary>
    private sealed class MutableTenantContext : ITenantContext
    {
        public string? TenantId { get; set; }

        public bool HasTenant => !string.IsNullOrWhiteSpace(TenantId);
    }
}
