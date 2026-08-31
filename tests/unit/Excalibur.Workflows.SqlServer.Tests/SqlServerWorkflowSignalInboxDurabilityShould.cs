// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Data.SqlClient;

using Shouldly;

using Xunit;

namespace Excalibur.Workflows.SqlServer.Tests;

/// <summary>
/// Real-infrastructure regression lock for the durable exactly-once guarantee of
/// <see cref="SqlServerWorkflowSignalInbox"/> against a live SQL Server container.
/// </summary>
/// <remarks>
/// This exercises the property the in-process default cannot make: a signal's deduplication key survives a
/// process restart. A "restart" is modelled by constructing a FRESH inbox instance over the SAME database —
/// no in-memory state carries across — and asserting the redelivered <c>(instanceId, signalId)</c> is still
/// refused. Never skipped: the fixture fails fast when Docker is unavailable, so a missing container surfaces
/// as a failure, not a silent pass.
/// </remarks>
[Collection(SqlServerWorkflowSignalInboxTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerWorkflowSignalInboxDurabilityShould
{
    private readonly SqlServerWorkflowSignalInboxContainerFixture _fixture;

    public SqlServerWorkflowSignalInboxDurabilityShould(SqlServerWorkflowSignalInboxContainerFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// SAFETY: dedup survives a "restart". Admit (inst, sig) on inbox A, then a FRESH inbox B over the same
    /// DB refuses the same (inst, sig). LIVENESS pair: B still admits a DISTINCT (inst, sig2) — the store is
    /// not blanket-false, it genuinely deduplicates. RED on a mutation that upserts / swallows-and-returns-true
    /// / drops the UNIQUE constraint.
    /// </summary>
    [Fact]
    public async Task Deduplicate_A_Redelivered_Signal_Across_A_Fresh_Inbox_Instance()
    {
        var ct = TestContext.Current.CancellationToken;
        await EnsureReadyAsync().ConfigureAwait(false);

        var instanceId = $"inst-{Guid.NewGuid():N}";
        const string signalId = "signal-A";
        var options = CreateOptions();

        // Inbox instance A admits the signal (newly admitted -> true).
        var inboxA = new SqlServerWorkflowSignalInbox(() => _fixture.CreateConnection(), options, TenantInboxFixture.SingleTenant);
        var firstAdmit = await inboxA.TryEnqueueAsync(instanceId, signalId, "OrderApproved", "{\"n\":1}", ct)
            .ConfigureAwait(false);
        firstAdmit.ShouldBeTrue("The first admission of a fresh (instanceId, signalId) must succeed.");

        // A FRESH inbox instance B over the SAME database == a process restart: no in-memory state carries.
        var inboxB = new SqlServerWorkflowSignalInbox(() => _fixture.CreateConnection(), options, TenantInboxFixture.SingleTenant);

        // SAFETY: the redelivered (inst, sig) is still refused after the "restart".
        var redeliver = await inboxB.TryEnqueueAsync(instanceId, signalId, "OrderApproved", "{\"n\":1}", ct)
            .ConfigureAwait(false);
        redeliver.ShouldBeFalse(
            "Durability guarantee: a producer's redelivery of the same (instanceId, signalId) must be " +
            "deduplicated even by a fresh inbox instance (a restart) — never re-admitted, never upserted.");

        // LIVENESS pair: the fresh inbox still admits a genuinely new signal (not a blanket-false store).
        var distinctAdmit = await inboxB.TryEnqueueAsync(instanceId, "signal-A2", "OrderShipped", null, ct)
            .ConfigureAwait(false);
        distinctAdmit.ShouldBeTrue(
            "Liveness: a fresh inbox instance must still admit a DISTINCT (instanceId, signalId) — the inbox " +
            "deduplicates, it does not refuse everything.");
    }

    /// <summary>
    /// LIVENESS (ordering): admit N distinct signals; DrainAsync returns them in ascending Sequence (arrival)
    /// order — the IDENTITY arrival sequence, never a wall-clock timestamp.
    /// </summary>
    [Fact]
    public async Task Drain_Admitted_Signals_In_Ascending_Arrival_Order()
    {
        var ct = TestContext.Current.CancellationToken;
        await EnsureReadyAsync().ConfigureAwait(false);

        var instanceId = $"inst-{Guid.NewGuid():N}";
        var options = CreateOptions();
        var inbox = new SqlServerWorkflowSignalInbox(() => _fixture.CreateConnection(), options, TenantInboxFixture.SingleTenant);

        var expected = new[] { "sig-0", "sig-1", "sig-2", "sig-3", "sig-4" };
        foreach (var signalId in expected)
        {
            var admitted = await inbox.TryEnqueueAsync(instanceId, signalId, "Tick", null, ct).ConfigureAwait(false);
            admitted.ShouldBeTrue($"Distinct signal {signalId} should be newly admitted.");
        }

        var drained = await inbox.DrainAsync(instanceId, ct).ConfigureAwait(false);

        drained.Count.ShouldBe(expected.Length);
        drained.Select(e => e.SignalId).ShouldBe(expected, "Drain must return signals in ascending arrival order.");

        // Sequence is strictly ascending (the IDENTITY arrival column), matching insertion order.
        var sequences = drained.Select(e => e.Sequence).ToArray();
        for (var i = 1; i < sequences.Length; i++)
        {
            sequences[i].ShouldBeGreaterThan(
                sequences[i - 1],
                "Drain order must follow the monotonic IDENTITY arrival sequence.");
        }
    }

    private SqlServerWorkflowSignalInboxOptions CreateOptions() => new()
    {
        ConnectionString = _fixture.ConnectionString,
        SchemaName = _fixture.SchemaName,
        TableName = _fixture.TableName,
    };

    private async Task EnsureReadyAsync()
    {
        _fixture.DockerAvailable.ShouldBeTrue(
            "SQL Server container must be available - real-infra durability lock is never skipped.");
        await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// The ambient tenant these inboxes run under. Every construction in this file binds the SAME tenant, so
    /// the deduplication these tests assert is deduplication WITHIN one tenant — which is exactly the
    /// property that must survive the widened key.
    /// </summary>
    private sealed class TenantInboxFixture : Excalibur.Dispatch.ITenantContext
    {
        public static Excalibur.Dispatch.ITenantContext SingleTenant { get; } = new TenantInboxFixture();

        public string? TenantId => "tenant-a";

        public bool HasTenant => true;
    }

}
