// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Abstractions;
using Excalibur.Saga.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Tests.Shared.Helpers;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.SqlServer;

public sealed partial class SqlServerSagaCommandOutboxRecoveryShould
{
    [Theory]
    [InlineData(MessageFailureKind.Permanent)]
    [InlineData(MessageFailureKind.Poison)]
    [InlineData(MessageFailureKind.Unknown)]
    public Task PersistOriginalFailureHoldAndRecoverThroughAnIndependentSqlStore(MessageFailureKind kind) => RunAsync(async harness =>
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var token = budget.Token;
        var store = FailureStore(harness);
        var facet = FailureFacet(store);
        facet.HoldsSurviveProcessRestart.ShouldBeTrue();
        await InstallFailureSchema(harness, "dbo.SagaTimeouts", token);
        await facet.ValidateFailureStorageAsync(token);
        var original = FailureTimeout("held-" + Guid.NewGuid().ToString("N"));
        await store.ScheduleTimeoutAsync(original, token);
        var claim = (await store.ClaimDueTimeoutsAsync(DateTimeOffset.UtcNow, 10, token)).Single();
        claim.SourceEpoch.ShouldNotBeNullOrEmpty(); claim.DeliveryEpoch.ShouldNotBeNullOrEmpty();
        var held = RequireHeld(await facet.TryHoldFailureAsync(new(claim, kind, "original reason"), claim, Guid.NewGuid(), token));
        held.State.ShouldBe(SagaTimeoutHoldState.Held);
        var independent = FailureStore(harness);
        var saved = (await FailureFacet(independent).GetHeldFailuresAsync(TenantDefaults.DefaultTenantId, original.SagaId, 10, null, token)).Items.Single();
        saved.Token.Value.ShouldBe(held.Token.Value);
        saved.Timeout.TimeoutData.ShouldBe(original.TimeoutData);
        saved.Timeout.DueAt.ShouldBe(original.DueAt);
        (await independent.ClaimDueTimeoutsAsync(DateTimeOffset.UtcNow.AddHours(1), 10, token)).ShouldBeEmpty();
        (await independent.GetDueTimeoutsAsync(DateTimeOffset.UtcNow, token)).Single().TimeoutId.ShouldBe(original.TimeoutId);
        (await independent.MarkDeliveredAsync(claim, token)).ShouldBe(SagaTimeoutRetirementOutcome.Superseded);
        await independent.ScheduleTimeoutAsync(FailureTimeout("healthy-" + Guid.NewGuid().ToString("N")), token);
        var healthy = (await independent.ClaimDueTimeoutsAsync(DateTimeOffset.UtcNow, 10, token)).Single();
        (await independent.MarkDeliveredAsync(healthy, token)).ShouldBe(SagaTimeoutRetirementOutcome.Retired);
        var command = Guid.NewGuid();
        (await FailureFacet(independent).TryResumeHeldFailureAsync(saved.Token, command, token))!.State.ShouldBe(SagaTimeoutHoldState.Resumed);
        var resumed = (await independent.ClaimDueTimeoutsAsync(DateTimeOffset.UtcNow, 10, token)).Single();
        resumed.SourceEpoch.ShouldBe(claim.SourceEpoch); resumed.DeliveryEpoch.ShouldNotBe(claim.DeliveryEpoch);
        resumed.Timeout.TimeoutData.ShouldBe(original.TimeoutData);
        await FailureFacet(independent).TryResumeHeldFailureAsync(saved.Token, command, token);
        (await independent.MarkDeliveredAsync(resumed, token)).ShouldBe(SagaTimeoutRetirementOutcome.Retired);
    });

    [Fact]
    public Task ReconcileExactSqlHoldOperationAndRefuseItsConflictingInputs() => RunAsync(async harness =>
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(30)); var token = budget.Token;
        var store = FailureStore(harness); var facet = FailureFacet(store);
        await store.ScheduleTimeoutAsync(FailureTimeout("same-op"), token);
        var claim = (await store.ClaimDueTimeoutsAsync(DateTimeOffset.UtcNow, 1, token)).Single();
        var observation = FailureObservation(claim); var operation = Guid.NewGuid();
        var held = RequireHeld(await facet.TryHoldFailureAsync(observation, claim, operation, token));
        RequireHeld(await facet.TryHoldFailureAsync(observation, claim, operation, token)).Token.Value.ShouldBe(held.Token.Value);
        // A different host's retained original operation can observe THIS real hold without claiming it committed that operation.
        RequireHeld(await facet.TryHoldFailureAsync(observation, claim, Guid.NewGuid(), token)).Token.Value.ShouldBe(held.Token.Value);
        (await facet.TryHoldFailureAsync(new(claim, MessageFailureKind.Unknown, "conflict"), claim, operation, token)).Outcome.ShouldBe(SagaTimeoutHoldOutcome.Rejected);
        (await facet.TryHoldFailureAsync(observation, claim with { ClaimToken = "not-the-original-operation-claim" }, operation, token)).Outcome.ShouldBe(SagaTimeoutHoldOutcome.Rejected);
        await facet.TryResumeHeldFailureAsync(held.Token, Guid.NewGuid(), token);
        var ended = await facet.TryHoldFailureAsync(observation, claim, operation, token);
        ended.Outcome.ShouldBe(SagaTimeoutHoldOutcome.ObservationEnded);
        ended.Receipt.ShouldNotBeNull().State.ShouldBe(SagaTimeoutHoldState.Resumed);
    });

    [Fact]
    public Task RequireFreshActualSqlClaimForRebindWithoutRewritingOriginalObservation() => RunAsync(async harness =>
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(15)); var token = budget.Token;
        var store = FailureStore(harness, leaseSeconds: 1); var facet = FailureFacet(store);
        await store.ScheduleTimeoutAsync(FailureTimeout("rebound"), token);
        var first = (await store.ClaimDueTimeoutsAsync(DateTimeOffset.UtcNow, 1, token)).Single();
        var observation = FailureObservation(first);
        ClaimedSagaTimeout? next = null;
        // Positive server-clock eligibility witness; no forced ClaimedAt/lease or private store mutation.
        while (next is null)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50), token); // delay-ok: Poll a real fresh server-eligible SQL claim under the owning budget; no synthetic lease mutation.
            next = (await store.ClaimDueTimeoutsAsync(DateTimeOffset.UtcNow, 1, token)).SingleOrDefault();
        }
        next.ClaimToken.ShouldNotBe(first.ClaimToken);
        next.SourceEpoch.ShouldBe(first.SourceEpoch); next.DeliveryEpoch.ShouldBe(first.DeliveryEpoch);
        (await facet.TryHoldFailureAsync(observation, first, Guid.NewGuid(), token)).Outcome.ShouldBe(SagaTimeoutHoldOutcome.RetryWithFreshClaim);
        RequireHeld(await facet.TryHoldFailureAsync(observation, next, Guid.NewGuid(), token)).State.ShouldBe(SagaTimeoutHoldState.Held);
    });

    [Fact]
    public Task RejectOldSqlObservationAfterResumeAndLatestReceiptReplacement() => RunAsync(async harness =>
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(30)); var token = budget.Token;
        var store = FailureStore(harness); var facet = FailureFacet(store);
        await store.ScheduleTimeoutAsync(FailureTimeout("cycles"), token);
        var first = (await store.ClaimDueTimeoutsAsync(DateTimeOffset.UtcNow, 1, token)).Single();
        var original = FailureObservation(first); var operation = Guid.NewGuid();
        var held = RequireHeld(await facet.TryHoldFailureAsync(original, first, operation, token));
        await facet.TryResumeHeldFailureAsync(held.Token, Guid.NewGuid(), token);
        var next = (await store.ClaimDueTimeoutsAsync(DateTimeOffset.UtcNow, 1, token)).Single();
        var second = RequireHeld(await facet.TryHoldFailureAsync(FailureObservation(next), next, Guid.NewGuid(), token));
        await facet.TryResumeHeldFailureAsync(second.Token, Guid.NewGuid(), token);
        var current = (await store.ClaimDueTimeoutsAsync(DateTimeOffset.UtcNow, 1, token)).Single();
        (await facet.TryHoldFailureAsync(original, first, operation, token)).Outcome.ShouldBe(SagaTimeoutHoldOutcome.ObservationEnded);
        (await facet.TryHoldFailureAsync(original, current, Guid.NewGuid(), token)).Outcome.ShouldBe(SagaTimeoutHoldOutcome.ObservationEnded);
        (await facet.TryResumeHeldFailureAsync(held.Token, Guid.NewGuid(), token)).ShouldBeNull();
        RequireHeld(await facet.TryHoldFailureAsync(FailureObservation(current), current, Guid.NewGuid(), token)).State.ShouldBe(SagaTimeoutHoldState.Held);
    });

    [Fact]
    public Task PreserveOwnedSqlReceiverCancellationAndItsUncancelledLivenessPartner() => RunAsync(async harness =>
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(30)); var token = budget.Token;
        await InstallTimerReceiver(harness, token);
        var store = FailureStore(harness); var receiver = Receiver(store); var facet = FailureFacet(store);
        var domain = ReceiverDomain(); var id = "owned-held";
        var journal = ReceiverJournal(domain, await receiver.ObserveFenceAsync(domain, token), Guid.NewGuid(),
            ScheduleIntent(domain, id), ScheduleIntent(domain, "owned-healthy"));
        await receiver.ApplyAsync(journal, token);
        var claims = await store.ClaimDueTimeoutsAsync(DateTimeOffset.UtcNow, 10, token);
        var claim = claims.Single(row => row.Timeout.TimeoutId == id);
        var healthy = claims.Single(row => row.Timeout.TimeoutId == "owned-healthy");
        var held = RequireHeld(await facet.TryHoldFailureAsync(FailureObservation(claim), claim, Guid.NewGuid(), token));
        (await store.MarkDeliveredAsync(healthy, token)).ShouldBe(SagaTimeoutRetirementOutcome.Retired);
        (await receiver.IsTimeoutDeliveryCurrentAsync(healthy.OwnedDelivery!, token)).ShouldBeTrue();
        await store.CancelTimeoutAsync(domain.SagaId.ToString(), id, token);
        (await facet.TryResumeHeldFailureAsync(held.Token, Guid.NewGuid(), token)).ShouldBeNull();
        (await receiver.IsTimeoutDeliveryCurrentAsync(claim.OwnedDelivery!, token)).ShouldBeFalse();
        (await store.GetDueTimeoutsAsync(DateTimeOffset.UtcNow, token)).ShouldBeEmpty();
    });

    [Fact]
    public Task RollBackTheProvisionalSqlHoldWithoutLosingOriginalPayloadOrClaim() => RunAsync(async harness =>
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(30)); var token = budget.Token;
        var store = FailureStore(harness); var facet = FailureFacet(store);
        await store.ScheduleTimeoutAsync(FailureTimeout("rollback-hold"), token);
        var claim = (await store.ClaimDueTimeoutsAsync(DateTimeOffset.UtcNow, 1, token)).Single();
        var observation = FailureObservation(claim); var operation = Guid.NewGuid();
        await ReceiverSql(harness, """
            CREATE TRIGGER dbo.RejectControlledFailureHold ON dbo.SagaTimeouts AFTER UPDATE AS
            BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE FailureHeld=1)
              THROW 51088,'controlled failure-hold rollback',1; END;
            """, token);
        var failure = (await Should.ThrowAsync<OperationFailedException>(async () => await facet.TryHoldFailureAsync(observation, claim, operation, token)))
            .InnerException.ShouldBeOfType<SqlException>();
        failure.Number.ShouldBe(51088); failure.Message.ShouldContain("controlled failure-hold rollback");
        (await facet.GetHeldFailuresAsync(TenantDefaults.DefaultTenantId, null, 10, null, token)).Items.ShouldBeEmpty();
        (await store.GetDueTimeoutsAsync(DateTimeOffset.UtcNow, token)).Single().TimeoutData.ShouldBe(claim.Timeout.TimeoutData);
        await ReceiverSql(harness, "DROP TRIGGER dbo.RejectControlledFailureHold;", token);
        // The same original claim and operation remain valid after the real rollback, no forced lease reset.
        RequireHeld(await facet.TryHoldFailureAsync(observation, claim, operation, token)).State.ShouldBe(SagaTimeoutHoldState.Held);
        await store.ScheduleTimeoutAsync(FailureTimeout("cancel-after-failed-hold"), token);
        var cancelled = (await store.ClaimDueTimeoutsAsync(DateTimeOffset.UtcNow, 1, token)).Single();
        var cancelledObservation = FailureObservation(cancelled); var cancelledOperation = Guid.NewGuid();
        await ReceiverSql(harness, """
            CREATE TRIGGER dbo.RejectControlledFailureHold ON dbo.SagaTimeouts AFTER UPDATE AS
            BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE FailureHeld=1)
              THROW 51088,'controlled failure-hold rollback',1; END;
            """, token);
        var cancelledFault = (await Should.ThrowAsync<OperationFailedException>(async () => await facet.TryHoldFailureAsync(cancelledObservation, cancelled, cancelledOperation, token)))
            .InnerException.ShouldBeOfType<SqlException>();
        cancelledFault.Number.ShouldBe(51088); cancelledFault.Message.ShouldContain("controlled failure-hold rollback");
        await store.CancelTimeoutAsync(cancelled.Timeout.SagaId, cancelled.Timeout.TimeoutId, token);
        await ReceiverSql(harness, "DROP TRIGGER dbo.RejectControlledFailureHold;", token);
        var ended = await facet.TryHoldFailureAsync(cancelledObservation, cancelled, cancelledOperation, token);
        ended.Outcome.ShouldBe(SagaTimeoutHoldOutcome.ObservationEnded); ended.Receipt.ShouldBeNull();
        (await facet.GetHeldFailuresAsync(TenantDefaults.DefaultTenantId, null, 10, null, token)).Items.Single().Timeout.TimeoutId.ShouldBe("rollback-hold");
    });

    [Fact]
    public Task PreserveLegacyManualSqlMethodsThenEnableTheActualCustomPhysicalTarget() => RunAsync(async harness =>
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(30)); var token = budget.Token;
        await ReceiverSql(harness, """
            SELECT TOP(0) TimeoutId,SagaId,SagaType,TimeoutType,TimeoutData,DueAt,ScheduledAt,ClaimedAt,ClaimedBy,TenantId
             INTO dbo.LegacyFailureTimeouts FROM dbo.SagaTimeouts;
            ALTER TABLE dbo.LegacyFailureTimeouts ADD PRIMARY KEY(TimeoutId);
            """, token);
        var legacy = FailureStore(harness, "LegacyFailureTimeouts"); var facet = FailureFacet(legacy);
        var missing = await Should.ThrowAsync<InvalidOperationException>(async () => await facet.ValidateFailureStorageAsync(token));
        missing.Message.ShouldContain("07-SagaTimeoutFailureHolds.sql");
        await legacy.ScheduleTimeoutAsync(FailureTimeout("cancel-before07"), token);
        await legacy.CancelAllTimeoutsAsync("ordinary-failure", token);
        (await legacy.GetDueTimeoutsAsync(DateTimeOffset.UtcNow, token)).ShouldBeEmpty();
        await legacy.ScheduleTimeoutAsync(FailureTimeout("retire-before07"), token);
        var original = (await legacy.ClaimDueTimeoutsAsync(DateTimeOffset.UtcNow, 1, token)).Single();
        original.SourceEpoch.ShouldBeNull(); original.DeliveryEpoch.ShouldBeNull();
        (await legacy.MarkDeliveredAsync(original, token)).ShouldBe(SagaTimeoutRetirementOutcome.Retired);
        // A live legacy row receives persisted winners during the additive migration, not a fresh epoch per read.
        await legacy.ScheduleTimeoutAsync(FailureTimeout("held-custom"), token);
        await InstallFailureSchema(harness, "dbo.LegacyFailureTimeouts", token);
        await facet.ValidateFailureStorageAsync(token);
        var claim = (await legacy.ClaimDueTimeoutsAsync(DateTimeOffset.UtcNow, 1, token)).Single();
        var held = RequireHeld(await facet.TryHoldFailureAsync(FailureObservation(claim), claim, Guid.NewGuid(), token));
        var foreign = FailureFacet(FailureStore(harness));
        (await foreign.TryResumeHeldFailureAsync(held.Token, Guid.NewGuid(), token)).ShouldBeNull();
        (await foreign.TryHoldFailureAsync(FailureObservation(claim), claim, Guid.NewGuid(), token)).Outcome.ShouldBe(SagaTimeoutHoldOutcome.Rejected);
        (await facet.GetHeldFailuresAsync(TenantDefaults.DefaultTenantId, null, 1, null, token)).Items.Single().Timeout.TimeoutId.ShouldBe("held-custom");
        (await legacy.ClaimDueTimeoutsAsync(DateTimeOffset.UtcNow.AddHours(1), 10, token)).ShouldBeEmpty();
    });

    [Fact]
    public Task RefusePaddedIdentityAndOldSqlLifetimeAfterIdenticalReplacement() => RunAsync(async harness =>
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(30)); var token = budget.Token;
        var store = FailureStore(harness); var facet = FailureFacet(store);
        var timeout = FailureTimeout("opaque-id");
        await store.ScheduleTimeoutAsync(timeout, token);
        var first = (await store.ClaimDueTimeoutsAsync(DateTimeOffset.UtcNow, 1, token)).Single();
        var observation = FailureObservation(first);
        var padded = first with { Timeout = first.Timeout with { TimeoutId = "opaque-id " } };
        (await facet.TryHoldFailureAsync(observation, padded, Guid.NewGuid(), token)).Outcome.ShouldBe(SagaTimeoutHoldOutcome.Rejected);
        var operation = Guid.NewGuid();
        var held = RequireHeld(await facet.TryHoldFailureAsync(observation, first, operation, token));
        await store.CancelTimeoutAsync(timeout.SagaId, timeout.TimeoutId, token);
        (await facet.TryResumeHeldFailureAsync(held.Token, Guid.NewGuid(), token)).ShouldBeNull();
        var cancelled = await facet.TryHoldFailureAsync(observation, first, operation, token);
        cancelled.Outcome.ShouldBe(SagaTimeoutHoldOutcome.ObservationEnded);
        cancelled.Receipt.ShouldBeNull("absence releases the original observation without fabricating a cancellation/delivery snapshot");
        await store.ScheduleTimeoutAsync(timeout, token);
        var replacement = (await store.ClaimDueTimeoutsAsync(DateTimeOffset.UtcNow, 1, token)).Single();
        replacement.SourceEpoch.ShouldNotBe(first.SourceEpoch);
        replacement.Timeout.TimeoutData.ShouldBe(first.Timeout.TimeoutData);
        (await facet.TryHoldFailureAsync(observation, replacement, Guid.NewGuid(), token)).Outcome.ShouldBe(SagaTimeoutHoldOutcome.ObservationEnded);
        (await store.MarkDeliveredAsync(first, token)).ShouldBe(SagaTimeoutRetirementOutcome.Superseded);
        (await store.MarkDeliveredAsync(replacement, token)).ShouldBe(SagaTimeoutRetirementOutcome.Retired);
    });

    [Fact]
    public Task PageSqlHeldSnapshotsInTheExactRawTenantAndSagaPartition() => RunAsync(async harness =>
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(30)); var token = budget.Token;
        var store = FailureStore(harness); var facet = FailureFacet(store);
        foreach (var id in new[] { "page-a", "page-b", "page-c" })
        {
            await store.ScheduleTimeoutAsync(FailureTimeout(id), token);
            var claim = (await store.ClaimDueTimeoutsAsync(DateTimeOffset.UtcNow, 1, token)).Single();
            RequireHeld(await facet.TryHoldFailureAsync(FailureObservation(claim), claim, Guid.NewGuid(), token));
        }
        var otherTenant = TenantDefaults.DefaultTenantId + " ";
        var foreign = new SqlServerSagaTimeoutStore(harness.Connection, NullLogger<SqlServerSagaTimeoutStore>.Instance, new ReceiverTenant(otherTenant));
        await foreign.ScheduleTimeoutAsync(FailureTimeout("foreign-page"), token);
        var foreignClaim = (await foreign.ClaimDueTimeoutsAsync(DateTimeOffset.UtcNow, 1, token)).Single();
        RequireHeld(await FailureFacet(foreign).TryHoldFailureAsync(FailureObservation(foreignClaim), foreignClaim, Guid.NewGuid(), token));
        (await facet.TryHoldFailureAsync(FailureObservation(foreignClaim), foreignClaim, Guid.NewGuid(), token)).Outcome.ShouldBe(SagaTimeoutHoldOutcome.Rejected);
        var page = await facet.GetHeldFailuresAsync(TenantDefaults.DefaultTenantId, "ordinary-failure", 2, null, token);
        page.Items.Select(row => row.Timeout.TimeoutId).ShouldBe(new[] { "page-a", "page-b" });
        page.NextCursor.ShouldNotBeNull();
        var last = await facet.GetHeldFailuresAsync(TenantDefaults.DefaultTenantId, "ordinary-failure", 2, page.NextCursor, token);
        last.Items.Single().Timeout.TimeoutId.ShouldBe("page-c"); last.NextCursor.ShouldBeNull();
        await Should.ThrowAsync<ArgumentException>(async () => await facet.GetHeldFailuresAsync(TenantDefaults.DefaultTenantId, null, 2, page.NextCursor, token));
        await Should.ThrowAsync<InvalidOperationException>(async () => await facet.GetHeldFailuresAsync(otherTenant, null, 2, null, token));
        var alteredOriginal = foreignClaim with { Timeout = foreignClaim.Timeout with { SagaId = "different-original-saga" } };
        (await FailureFacet(foreign).TryHoldFailureAsync(FailureObservation(alteredOriginal), alteredOriginal, Guid.NewGuid(), token)).Outcome.ShouldBe(SagaTimeoutHoldOutcome.Rejected);
    });

    [Fact]
    public Task RefuseConstantEpochDefaultsWithoutRewritingTheSelectedCustomSchema() => RunAsync(async harness =>
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(30)); var token = budget.Token;
        await ReceiverSql(harness, """
            SELECT TOP(0) TimeoutId,SagaId,SagaType,TimeoutType,TimeoutData,DueAt,ScheduledAt,ClaimedAt,ClaimedBy,TenantId
             INTO dbo.BadFailureDefaults FROM dbo.SagaTimeouts;
            ALTER TABLE dbo.BadFailureDefaults ADD PRIMARY KEY(TimeoutId);
            """, token);
        await InstallFailureSchema(harness, "dbo.BadFailureDefaults", token);
        await ReceiverSql(harness, """
            DECLARE @Drop NVARCHAR(MAX)=N'';
            SELECT @Drop=@Drop+N'ALTER TABLE dbo.BadFailureDefaults DROP CONSTRAINT '+QUOTENAME(d.name)+N';'
             FROM sys.columns c JOIN sys.default_constraints d ON d.object_id=c.default_object_id
             WHERE c.object_id=OBJECT_ID(N'dbo.BadFailureDefaults',N'U')
              AND c.name IN(N'FailureSourceEpoch',N'FailureDeliveryEpoch');
            EXEC sys.sp_executesql @Drop;
            ALTER TABLE dbo.BadFailureDefaults ADD DEFAULT ('11111111-1111-1111-1111-111111111111') FOR FailureSourceEpoch;
            ALTER TABLE dbo.BadFailureDefaults ADD DEFAULT ('22222222-2222-2222-2222-222222222222') FOR FailureDeliveryEpoch;
            """, token);
        var store = FailureStore(harness, "BadFailureDefaults");
        var startup = (await Should.ThrowAsync<OperationFailedException>(async () => await FailureFacet(store).ValidateFailureStorageAsync(token)))
            .InnerException.ShouldBeOfType<SqlException>();
        startup.Number.ShouldBe(51004); startup.Message.ShouldContain("default definitions");
        var migration = await Should.ThrowAsync<SqlException>(() => InstallFailureSchema(harness, "dbo.BadFailureDefaults", token));
        migration.Number.ShouldBe(51004); migration.Message.ShouldContain("default definitions");
        (await ReceiverCount(harness, """
            SELECT COUNT_BIG(*) FROM sys.columns c JOIN sys.default_constraints d ON d.object_id=c.default_object_id
            WHERE c.object_id=OBJECT_ID(N'dbo.BadFailureDefaults',N'U') AND
             (d.definition LIKE N'%11111111-1111-1111-1111-111111111111%'
              OR d.definition LIKE N'%22222222-2222-2222-2222-222222222222%');
            """, token)).ShouldBe(2);
        (await ReceiverCount(harness, "SELECT COUNT_BIG(*) FROM dbo.BadFailureDefaults;", token)).ShouldBe(0);
    });

    private static async Task InstallFailureSchema(Harness harness, string target, CancellationToken token)
    {
        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var batch in ShippedSchemaScript.ReadSqlCmdBatches("src/Excalibur/Excalibur.Saga.SqlServer/Scripts/07-SagaTimeoutFailureHolds.sql"))
            { await ReceiverSql(harness, batch.Replace("N'dbo.SagaTimeouts'", "N'" + target + "'", StringComparison.Ordinal), token); }
        }
    }

    private static SagaTimeoutHoldReceipt RequireHeld(SagaTimeoutHoldAttempt attempt)
    {
        attempt.Outcome.ShouldBe(SagaTimeoutHoldOutcome.Held);
        return attempt.Receipt.ShouldNotBeNull();
    }

    private static SqlServerSagaTimeoutStore FailureStore(Harness harness, string table = "SagaTimeouts", int leaseSeconds = 120) =>
        new(harness.Connection, global::Microsoft.Extensions.Options.Options.Create(new SqlServerSagaTimeoutStoreOptions
        { TableName = table, LeaseTimeoutSeconds = leaseSeconds }), NullLogger<SqlServerSagaTimeoutStore>.Instance,
            harness.Services.GetRequiredService<ITenantContext>());
    private static ISagaTimeoutFailureStore FailureFacet(ISagaTimeoutStore store) =>
        store.GetService(typeof(ISagaTimeoutFailureStore)).ShouldBeAssignableTo<ISagaTimeoutFailureStore>().ShouldNotBeNull();
    private static SagaTimeoutFailureObservation FailureObservation(ClaimedSagaTimeout claim) => new(claim, MessageFailureKind.Unknown, "original");
    private static SagaTimeout FailureTimeout(string id) => new(id, "ordinary-failure", "Saga", "Timeout", [0x1a,0x2b],
        DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(-2));
}
