// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Dapper;
using Excalibur.Data;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Orchestration;
using Excalibur.Saga.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Tests.Shared.Helpers;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.SqlServer;

public sealed partial class SqlServerSagaCommandOutboxRecoveryShould
{
    [Fact]
    public Task ApplyOrderedOrdinaryTimersAndRetainExactReceiptAfterQueueRetirement() => RunAsync(async harness =>
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var token = budget.Token;
        await InstallTimerReceiver(harness, token);
        var store = harness.Services.GetRequiredService<ISagaTimeoutStore>();
        var receiver = Receiver(store);
        var domain = ReceiverDomain();
        var before = await receiver.ObserveFenceAsync(domain, token);
        var first = "retired-" + Guid.NewGuid().ToString("N");
        var last = "live-" + Guid.NewGuid().ToString("N");
        var journal = ReceiverJournal(domain, before, Guid.NewGuid(),
            ScheduleIntent(domain, first), new(SagaTimerAction.Cancel, first, null),
            new(SagaTimerAction.CancelAll, string.Empty, null), ScheduleIntent(domain, last));
        var result = await receiver.ApplyAsync(journal, token);
        result.Outcome.ShouldBe(SagaTimerRecoveryApplyOutcome.Applied);
        result.IntentResults.Count.ShouldBe(4);
        result.IntentResults.All(item => item.Outcome == SagaTimerRecoveryIntentOutcome.Applied).ShouldBeTrue();
        (await store.GetDueTimeoutsAsync(DateTimeOffset.UtcNow, token)).ShouldHaveSingleItem().TimeoutId.ShouldBe(last);
        var claim = (await store.ClaimDueTimeoutsAsync(DateTimeOffset.UtcNow, 10, token)).ShouldHaveSingleItem();
        var origin = claim.OwnedDelivery.ShouldNotBeNull();
        origin.TimeoutId.ShouldBe(last);
        origin.IncarnationId.ShouldBe(journal.IncarnationId);
        origin.Generation.ShouldBe(journal.Sequence);
        (await store.MarkDeliveredAsync(claim, token)).ShouldBe(SagaTimeoutRetirementOutcome.Retired);
        (await receiver.IsTimeoutDeliveryCurrentAsync(origin, token)).ShouldBeTrue("queue acceptance is not cancellation");
        var replay = await receiver.ApplyAsync(journal, token);
        replay.Outcome.ShouldBe(SagaTimerRecoveryApplyOutcome.AlreadyApplied);
        replay.Fence!.Token.ShouldBe(result.Fence!.Token);
        (await store.GetDueTimeoutsAsync(DateTimeOffset.UtcNow, token)).ShouldBeEmpty("exact replay must not recreate retired payload");
        await store.CancelTimeoutAsync(domain.SagaId.ToString(), last, token);
        (await receiver.IsTimeoutDeliveryCurrentAsync(origin, token)).ShouldBeFalse("retained cancellation must invalidate queued work after payload retirement");
    });

    [Fact]
    public Task CancelOnlyTheNamedCapturedOrdinaryScheduleAndKeepItsHealthyPartner() => RunAsync(async harness =>
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var token = budget.Token;
        await InstallTimerReceiver(harness, token);
        var store = harness.Services.GetRequiredService<ISagaTimeoutStore>();
        var receiver = Receiver(store);
        var domain = ReceiverDomain();
        var before = await receiver.ObserveFenceAsync(domain, token);
        var cancelled = "cancel-" + Guid.NewGuid().ToString("N");
        var healthy = "healthy-" + Guid.NewGuid().ToString("N");
        var journal = ReceiverJournal(domain, before, Guid.NewGuid(), ScheduleIntent(domain, cancelled), ScheduleIntent(domain, healthy));
        await store.CancelTimeoutAsync(domain.SagaId.ToString(), cancelled, token);
        var actual = await receiver.ApplyAsync(journal, token);
        actual.Outcome.ShouldBe(SagaTimerRecoveryApplyOutcome.Applied);
        actual.IntentResults[0].Outcome.ShouldBe(SagaTimerRecoveryIntentOutcome.Cancelled);
        actual.IntentResults[1].Outcome.ShouldBe(SagaTimerRecoveryIntentOutcome.Applied);
        (await store.GetDueTimeoutsAsync(DateTimeOffset.UtcNow, token)).ShouldHaveSingleItem().TimeoutId.ShouldBe(healthy);
        var claim = (await store.ClaimDueTimeoutsAsync(DateTimeOffset.UtcNow, 10, token)).ShouldHaveSingleItem();
        (await receiver.IsTimeoutDeliveryCurrentAsync(claim.OwnedDelivery.ShouldNotBeNull(), token)).ShouldBeTrue();
    });

    [Fact]
    public Task ProtectNewerPublicTimerFromCapturedCancelAllThenAllowFreshCancellation() => RunAsync(async harness =>
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var token = budget.Token;
        await InstallTimerReceiver(harness, token);
        var store = harness.Services.GetRequiredService<ISagaTimeoutStore>();
        var receiver = Receiver(store);
        var domain = ReceiverDomain();
        var observed = await receiver.ObserveFenceAsync(domain, token);
        var journal = ReceiverJournal(domain, observed, Guid.NewGuid(), new SagaTimerIntent(SagaTimerAction.CancelAll, string.Empty, null));
        var id = "newer-" + Guid.NewGuid().ToString("N");
        await store.ScheduleTimeoutAsync(ScheduleIntent(domain, id).Timeout!, token);
        var result = await receiver.ApplyAsync(journal, token);
        result.IntentResults.ShouldHaveSingleItem().Outcome.ShouldBe(SagaTimerRecoveryIntentOutcome.ProtectedNewerTimer);
        (await store.GetDueTimeoutsAsync(DateTimeOffset.UtcNow, token)).ShouldHaveSingleItem().TimeoutId.ShouldBe(id);
        await store.CancelAllTimeoutsAsync(domain.SagaId.ToString(), token);
        (await store.GetDueTimeoutsAsync(DateTimeOffset.UtcNow, token)).ShouldBeEmpty();
        (await receiver.ApplyAsync(journal, token)).Outcome.ShouldBe(SagaTimerRecoveryApplyOutcome.AlreadyApplied);
        (await store.GetDueTimeoutsAsync(DateTimeOffset.UtcNow, token)).ShouldBeEmpty();
    });

    [Fact]
    public Task RollBackTheWholeOrdinaryTimerBatchOnRealSecondInsertThenRetryOriginalIds() => RunAsync(async harness =>
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var token = budget.Token;
        await InstallTimerReceiver(harness, token);
        var store = harness.Services.GetRequiredService<ISagaTimeoutStore>();
        var receiver = Receiver(store);
        var domain = ReceiverDomain();
        var before = await receiver.ObserveFenceAsync(domain, token);
        var first = "first-" + Guid.NewGuid().ToString("N");
        var second = "second-" + Guid.NewGuid().ToString("N");
        var journal = ReceiverJournal(domain, before, Guid.NewGuid(), ScheduleIntent(domain, first), ScheduleIntent(domain, second));
        await ReceiverSql(harness, """
            CREATE TRIGGER dbo.FailSecondOrdinaryTimer ON dbo.SagaTimeouts AFTER INSERT AS
            BEGIN
              SET NOCOUNT ON;
              IF (SELECT COUNT_BIG(*) FROM dbo.SagaTimeouts)>=2
                THROW 51092,'Controlled second ordinary timer insertion failed.',1;
            END;
            """, token);
        var error = await Should.ThrowAsync<OperationFailedException>(() => receiver.ApplyAsync(journal, token).AsTask());
        var sql = error.InnerException.ShouldBeOfType<SqlException>().ShouldNotBeNull();
        sql.Number.ShouldBe(51092);
        sql.Message.ShouldContain("Controlled second ordinary timer insertion failed.");
        (await harness.Count("dbo.SagaTimeouts", token)).ShouldBe(0);
        (await ReceiverCount(harness, "SELECT COUNT_BIG(*) FROM dispatch.SagaTimerRecoveryTimers;", token)).ShouldBe(0);
        (await ReceiverCount(harness, "SELECT COUNT_BIG(*) FROM dispatch.SagaTimerRecoveryResults;", token)).ShouldBe(0);
        var unchanged = await receiver.ObserveFenceAsync(domain, token);
        unchanged.Token.ShouldBe(before.Token);
        unchanged.Sequence.ShouldBe(0);
        unchanged.MutationRevision.ShouldBe(before.MutationRevision);
        await ReceiverSql(harness, "DROP TRIGGER dbo.FailSecondOrdinaryTimer;", token);
        (await receiver.ApplyAsync(journal, token)).Outcome.ShouldBe(SagaTimerRecoveryApplyOutcome.Applied);
        (await store.GetDueTimeoutsAsync(DateTimeOffset.UtcNow, token)).Select(row => row.TimeoutId).Order(StringComparer.Ordinal)
            .ShouldBe(new[] { first, second }.Order(StringComparer.Ordinal));
    });

    [Fact]
    public Task SerializeConcurrentOriginalOrdinaryBatchAndRejectForeignReceiverToken() => RunAsync(async harness =>
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var token = budget.Token;
        await InstallTimerReceiver(harness, token);
        var store = harness.Services.GetRequiredService<ISagaTimeoutStore>();
        var receiver = Receiver(store);
        var domain = ReceiverDomain();
        var before = await receiver.ObserveFenceAsync(domain, token);
        var id = "concurrent-" + Guid.NewGuid().ToString("N");
        var journal = ReceiverJournal(domain, before, Guid.NewGuid(), ScheduleIntent(domain, id));
        var pair = await Task.WhenAll(receiver.ApplyAsync(journal, token).AsTask(), receiver.ApplyAsync(journal, token).AsTask());
        pair.Count(row => row.Outcome == SagaTimerRecoveryApplyOutcome.Applied).ShouldBe(1);
        pair.Count(row => row.Outcome == SagaTimerRecoveryApplyOutcome.AlreadyApplied).ShouldBe(1);
        (await harness.Count("dbo.SagaTimeouts", token)).ShouldBe(1);
        await ReceiverSql(harness, "SELECT TOP(0) * INTO dbo.OtherRecoveryTimeouts FROM dbo.SagaTimeouts;", token);
        await InstallTimerReceiver(harness, token, "dbo.OtherRecoveryTimeouts");
        var other = new SqlServerSagaTimeoutStore(harness.Connection,
            global::Microsoft.Extensions.Options.Options.Create(new SqlServerSagaTimeoutStoreOptions { TableName = "OtherRecoveryTimeouts" }),
            NullLogger<SqlServerSagaTimeoutStore>.Instance, harness.Services.GetRequiredService<ITenantContext>());
        var foreign = Receiver(other);
        var foreignFence = await foreign.ObserveFenceAsync(domain, token);
        foreignFence.Token.ShouldNotBe(before.Token);
        await Should.ThrowAsync<InvalidOperationException>(() => foreign.ApplyAsync(journal, token).AsTask());
        (await ReceiverCount(harness, "SELECT COUNT_BIG(*) FROM dbo.OtherRecoveryTimeouts;", token)).ShouldBe(0);
        (await store.GetDueTimeoutsAsync(DateTimeOffset.UtcNow, token)).ShouldHaveSingleItem().TimeoutId.ShouldBe(id);
    });

    [Fact]
    public Task RecoverKnownCommittedOrdinaryBatchAfterReplyLossAndPreservePreCancellation() => RunAsync(async harness =>
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var token = budget.Token;
        await InstallTimerReceiver(harness, token);
        var store = harness.Services.GetRequiredService<ISagaTimeoutStore>();
        var receiver = Receiver(store);
        var domain = ReceiverDomain();
        var before = await receiver.ObserveFenceAsync(domain, token);
        var id = "reply-lost-" + Guid.NewGuid().ToString("N");
        var journal = ReceiverJournal(domain, before, Guid.NewGuid(), ScheduleIntent(domain, id));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => receiver.ApplyAsync(journal, cancelled.Token).AsTask());
        (await harness.Count("dbo.SagaTimeouts", token)).ShouldBe(0);
        var primary = new IOException("Controlled reply loss after known real receiver commit.");
        var caught = await Should.ThrowAsync<IOException>(async () =>
        {
            (await receiver.ApplyAsync(journal, token)).Outcome.ShouldBe(SagaTimerRecoveryApplyOutcome.Applied);
            throw primary;
        });
        caught.ShouldBeSameAs(primary);
        (await harness.Count("dbo.SagaTimeouts", token)).ShouldBe(1);
        var fresh = new SqlServerSagaTimeoutStore(harness.Connection, NullLogger<SqlServerSagaTimeoutStore>.Instance,
            harness.Services.GetRequiredService<ITenantContext>());
        var replay = await Receiver(fresh).ApplyAsync(journal, token);
        replay.Outcome.ShouldBe(SagaTimerRecoveryApplyOutcome.AlreadyApplied);
        replay.IntentResults.ShouldHaveSingleItem().TimeoutId.ShouldBe(id);
        (await harness.Count("dbo.SagaTimeouts", token)).ShouldBe(1);
    });

    [Fact]
    public Task ProtectAtomicTimerFromOlderOrdinaryCancellationAndRevokeQueuedAtomicAuthority() => RunAsync(async harness =>
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var token = budget.Token;
        await InstallTimerReceiver(harness, token);
        var store = harness.Services.GetRequiredService<ISagaTimeoutStore>();
        var receiver = Receiver(store);
        var id = Guid.NewGuid();
        var domain = new SagaTimerRecoveryDomain(TenantDefaults.DefaultTenantId, id);
        var before = await receiver.ObserveFenceAsync(domain, token);
        var oldCancel = ReceiverJournal(domain, before, Guid.NewGuid(), new SagaTimerIntent(SagaTimerAction.CancelAll, string.Empty, null));
        using (var coordinator = harness.Coordinator(harness.States))
        { await harness.Handle(coordinator, new StartEvent(id.ToString(), "with-timer", Complete: false), "atomic-partner", token); }
        var result = await receiver.ApplyAsync(oldCancel, token);
        result.IntentResults.ShouldHaveSingleItem().Outcome.ShouldBe(SagaTimerRecoveryIntentOutcome.ProtectedNewerTimer);
        var claim = (await store.ClaimDueTimeoutsAsync(DateTimeOffset.UtcNow.AddDays(1), 10, token)).ShouldHaveSingleItem();
        var origin = claim.OwnedDelivery.ShouldNotBeNull();
        var atomic = ((IServiceProvider)harness.States).GetService(typeof(ISagaAtomicTransitionStore)).ShouldBeAssignableTo<ISagaAtomicTransitionStore>().ShouldNotBeNull();
        (await atomic.IsTimeoutDeliveryCurrentAsync(origin, token)).ShouldBeTrue();
        (await store.MarkDeliveredAsync(claim, token)).ShouldBe(SagaTimeoutRetirementOutcome.Retired);
        await store.CancelAllTimeoutsAsync(id.ToString(), token);
        (await atomic.IsTimeoutDeliveryCurrentAsync(origin, token)).ShouldBeFalse();
        (await harness.Count("dbo.SagaTimeouts", token)).ShouldBe(0);
        (await harness.Count("dispatch.SagaTimeoutMembership", token)).ShouldBe(1);
    });

    [Fact]
    public Task PreserveExactTenantSagaAndOpaqueTimeoutAuthorityAcrossPublicCancellation() => RunAsync(async harness =>
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var token = budget.Token;
        await InstallTimerReceiver(harness, token);
        var a = new SqlServerSagaTimeoutStore(harness.Connection, NullLogger<SqlServerSagaTimeoutStore>.Instance, new ReceiverTenant("tenantA"));
        var b = new SqlServerSagaTimeoutStore(harness.Connection, NullLogger<SqlServerSagaTimeoutStore>.Instance, new ReceiverTenant("tenantA "));
        var shared = Guid.NewGuid();
        var domainA = new SagaTimerRecoveryDomain("tenantA", shared);
        var domainB = new SagaTimerRecoveryDomain("tenantA ", shared);
        var domainC = new SagaTimerRecoveryDomain("tenantA", Guid.NewGuid());
        var receiverA = Receiver(a);
        var receiverB = Receiver(b);
        var idA = "opaque-" + Guid.NewGuid().ToString("N");
        var idB = "foreign-" + Guid.NewGuid().ToString("N");
        var idC = "other-saga-" + Guid.NewGuid().ToString("N");
        var journalA = ReceiverJournal(domainA, await receiverA.ObserveFenceAsync(domainA, token), Guid.NewGuid(), ScheduleIntent(domainA, idA));
        var journalB = ReceiverJournal(domainB, await receiverB.ObserveFenceAsync(domainB, token), Guid.NewGuid(), ScheduleIntent(domainB, idB));
        var journalC = ReceiverJournal(domainC, await receiverA.ObserveFenceAsync(domainC, token), Guid.NewGuid(), ScheduleIntent(domainC, idC));
        (await receiverA.ApplyAsync(journalA, token)).Outcome.ShouldBe(SagaTimerRecoveryApplyOutcome.Applied);
        (await receiverB.ApplyAsync(journalB, token)).Outcome.ShouldBe(SagaTimerRecoveryApplyOutcome.Applied);
        (await receiverA.ApplyAsync(journalC, token)).Outcome.ShouldBe(SagaTimerRecoveryApplyOutcome.Applied);
        await Should.ThrowAsync<InvalidOperationException>(() => receiverB.ApplyAsync(journalA, token).AsTask());
        await a.CancelTimeoutAsync(shared.ToString(), idA + " ", token);
        var claims = await a.ClaimDueTimeoutsAsync(DateTimeOffset.UtcNow, 10, token);
        claims.Count.ShouldBe(3, "a padded opaque timeout ID is not the original ID");
        var claimA = claims.Single(row => row.Timeout.TimeoutId == idA);
        var claimB = claims.Single(row => row.Timeout.TimeoutId == idB);
        var claimC = claims.Single(row => row.Timeout.TimeoutId == idC);
        await a.CancelAllTimeoutsAsync(shared.ToString(), token);
        (await receiverA.IsTimeoutDeliveryCurrentAsync(claimA.OwnedDelivery.ShouldNotBeNull(), token)).ShouldBeFalse();
        (await receiverB.IsTimeoutDeliveryCurrentAsync(claimB.OwnedDelivery.ShouldNotBeNull(), token)).ShouldBeTrue();
        (await receiverA.IsTimeoutDeliveryCurrentAsync(claimC.OwnedDelivery.ShouldNotBeNull(), token)).ShouldBeTrue();
        (await a.MarkDeliveredAsync(claimB, token)).ShouldBe(SagaTimeoutRetirementOutcome.Superseded);
        (await b.MarkDeliveredAsync(claimB, token)).ShouldBe(SagaTimeoutRetirementOutcome.Retired);
        (await a.MarkDeliveredAsync(claimC, token)).ShouldBe(SagaTimeoutRetirementOutcome.Retired);
        (await harness.Count("dbo.SagaTimeouts", token)).ShouldBe(0);
    });

    private sealed class ReceiverTenant(string tenant) : ITenantContext
    {
        public string? TenantId { get; } = tenant;
        public bool HasTenant => true;
    }

    private static ISagaTimerRecoveryStore Receiver(ISagaTimeoutStore store) =>
        store.GetService(typeof(ISagaTimerRecoveryStore)).ShouldBeAssignableTo<ISagaTimerRecoveryStore>().ShouldNotBeNull();

    private static SagaTimerRecoveryDomain ReceiverDomain() => new(TenantDefaults.DefaultTenantId, Guid.NewGuid());

    private static SagaTimerIntent ScheduleIntent(SagaTimerRecoveryDomain domain, string id) =>
        new(SagaTimerAction.Schedule, id, new SagaTimeout(id, domain.SagaId.ToString(), "ReceiverSaga", "ReceiverTimeout", [0x1a, 0x2b],
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch));

    private static SagaTimerRecoveryJournal ReceiverJournal(SagaTimerRecoveryDomain domain, SagaTimerRecoveryFence fence,
        Guid incarnation, params SagaTimerIntent[] intents) => new(new SagaScopeIdentity(domain.TenantId, "ReceiverState", domain.SagaId),
            incarnation, 1, Guid.NewGuid(), fence.Token, fence.MutationRevision, 1, SagaTimerRecoveryCodec.Encode(intents));

    private static async Task InstallTimerReceiver(Harness harness, CancellationToken token, string target = "dbo.SagaTimeouts")
    {
        // Same shipped06 bytes, with only the explicitly selected physical target changed for the foreign-table arm.
        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var batch in ShippedSchemaScript.ReadSqlCmdBatches("src/Excalibur/Excalibur.Saga.SqlServer/Scripts/06-SagaTimerRecovery.sql"))
            { await ReceiverSql(harness, batch.Replace("N'dbo.SagaTimeouts'", "N'" + target + "'", StringComparison.Ordinal), token); }
        }
    }

    private static async Task ReceiverSql(Harness harness, string sql, CancellationToken token)
    {
        await using var connection = new SqlConnection(harness.Connection);
        await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: token));
    }

    private static async Task<long> ReceiverCount(Harness harness, string sql, CancellationToken token)
    {
        await using var connection = new SqlConnection(harness.Connection);
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(sql, cancellationToken: token));
    }
}
