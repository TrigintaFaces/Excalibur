// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Dapper;
using Excalibur.Dispatch;
using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Oracle;
using Microsoft.Extensions.Logging.Abstractions;
using Oracle.ManagedDataAccess.Client;
using Shouldly;
using Xunit;

namespace Excalibur.Integration.Tests.Data.Saga;

public sealed partial class OracleSagaTimeoutStoreConformanceShould
{
    private static readonly string[] ExpectedFirstHeldPageIds = ["hold-a", "hold-b"];

    [Theory]
    [InlineData(MessageFailureKind.Permanent)]
    [InlineData(MessageFailureKind.Poison)]
    [InlineData(MessageFailureKind.Unknown)]
    public async Task HoldOriginalOracleFailureAndQueryDetachedPayloadFromIndependentStore(MessageFailureKind kind)
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(90)); var token = budget.Token;
        await EnsureFailureSchemaAsync(token);
        var store = NewFailureStore(); var facet = FailureFacet(store);
        await facet.ValidateFailureStorageAsync(token);
        facet.HoldsSurviveProcessRestart.ShouldBeTrue();
        var timeout = FailureTimeout();
        await store.ScheduleTimeoutAsync(timeout, token);
        var claim = (await store.ClaimDueTimeoutsAsync(DateTimeOffset.MaxValue, 10, token)).ShouldHaveSingleItem();
        var observation = new SagaTimeoutFailureObservation(claim, kind, "actual classified fault");
        var held = RequireFailureHeld(await facet.TryHoldFailureAsync(observation, claim, Guid.NewGuid(), token));
        (await store.MarkDeliveredAsync(claim, token)).ShouldBe(SagaTimeoutRetirementOutcome.Superseded);
        var independent = NewFailureStore();
        (await independent.ClaimDueTimeoutsAsync(DateTimeOffset.MaxValue, 10, token)).ShouldBeEmpty();
        var saved = (await FailureFacet(independent).GetHeldFailuresAsync("hold-tenant", null, 10, null, token)).Items.ShouldHaveSingleItem();
        saved.Timeout.TimeoutId.ShouldBe(timeout.TimeoutId); saved.Timeout.DueAt.ShouldBe(timeout.DueAt);
        saved.Timeout.TimeoutData.ShouldBe(timeout.TimeoutData); saved.Kind.ShouldBe(kind);
        saved.Timeout.TimeoutData![0] = 99;
        (await FailureFacet(independent).GetHeldFailuresAsync("hold-tenant", null, 10, null, token)).Items[0].Timeout.TimeoutData.ShouldBe(timeout.TimeoutData);
        (await independent.GetDueTimeoutsAsync(DateTimeOffset.MaxValue, token)).ShouldHaveSingleItem().TimeoutId.ShouldBe(timeout.TimeoutId);
        var healthy = FailureTimeout(); await independent.ScheduleTimeoutAsync(healthy, token);
        var live = (await independent.ClaimDueTimeoutsAsync(DateTimeOffset.MaxValue, 10, token)).ShouldHaveSingleItem();
        live.Timeout.TimeoutId.ShouldBe(healthy.TimeoutId);
        (await independent.MarkDeliveredAsync(live, token)).ShouldBe(SagaTimeoutRetirementOutcome.Retired);
        held.State.ShouldBe(SagaTimeoutHoldState.Held);
    }

    [Fact]
    public async Task ReconcileOracleHoldAndObserveOtherOperationWithoutClaimingItsCommit()
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(90)); var token = budget.Token;
        await EnsureFailureSchemaAsync(token); var store = NewFailureStore(); var facet = FailureFacet(store);
        await store.ScheduleTimeoutAsync(FailureTimeout(), token);
        var claim = (await store.ClaimDueTimeoutsAsync(DateTimeOffset.MaxValue, 1, token)).ShouldHaveSingleItem();
        var observation = new SagaTimeoutFailureObservation(claim, MessageFailureKind.Unknown, "original");
        var operation = Guid.NewGuid();
        var held = RequireFailureHeld(await facet.TryHoldFailureAsync(observation, claim, operation, token));
        RequireFailureHeld(await facet.TryHoldFailureAsync(observation, claim, operation, token)).Token.Value.ShouldBe(held.Token.Value);
        RequireFailureHeld(await facet.TryHoldFailureAsync(observation, claim, Guid.NewGuid(), token)).Token.Value.ShouldBe(held.Token.Value);
        (await facet.TryHoldFailureAsync(new(claim, MessageFailureKind.Unknown, "conflict"), claim, operation, token)).Outcome.ShouldBe(SagaTimeoutHoldOutcome.Rejected);
        (await facet.TryHoldFailureAsync(observation, claim with { ClaimToken = "not-the-recorded-operation" }, operation, token)).Outcome.ShouldBe(SagaTimeoutHoldOutcome.Rejected);
        (await facet.GetHeldFailuresAsync("hold-tenant", null, 10, null, token)).Items.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ResumeOracleCycleOnceAndRejectOldObservationAfterReceiptTurnover()
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(90)); var token = budget.Token;
        await EnsureFailureSchemaAsync(token); var store = NewFailureStore(); var facet = FailureFacet(store);
        await store.ScheduleTimeoutAsync(FailureTimeout(), token);
        var first = (await store.ClaimDueTimeoutsAsync(DateTimeOffset.MaxValue, 1, token)).ShouldHaveSingleItem();
        var original = new SagaTimeoutFailureObservation(first, MessageFailureKind.Unknown, "original"); var op = Guid.NewGuid();
        var held = RequireFailureHeld(await facet.TryHoldFailureAsync(original, first, op, token)); var resume = Guid.NewGuid();
        (await facet.TryResumeHeldFailureAsync(held.Token, resume, token)).ShouldNotBeNull().State.ShouldBe(SagaTimeoutHoldState.Resumed);
        var second = (await store.ClaimDueTimeoutsAsync(DateTimeOffset.MaxValue, 1, token)).ShouldHaveSingleItem();
        second.SourceEpoch.ShouldBe(first.SourceEpoch); second.DeliveryEpoch.ShouldNotBe(first.DeliveryEpoch);
        await facet.TryResumeHeldFailureAsync(held.Token, resume, token);
        // Replay did not clear the fresh claim or rotate a second time: it can still retire/hold.
        var secondHeld = RequireFailureHeld(await facet.TryHoldFailureAsync(new(second, MessageFailureKind.Unknown, "second"), second, Guid.NewGuid(), token));
        await facet.TryResumeHeldFailureAsync(secondHeld.Token, Guid.NewGuid(), token);
        var current = (await store.ClaimDueTimeoutsAsync(DateTimeOffset.MaxValue, 1, token)).ShouldHaveSingleItem();
        (await facet.TryHoldFailureAsync(original, first, op, token)).Outcome.ShouldBe(SagaTimeoutHoldOutcome.ObservationEnded);
        (await facet.TryHoldFailureAsync(original, current, Guid.NewGuid(), token)).Outcome.ShouldBe(SagaTimeoutHoldOutcome.ObservationEnded);
        (await facet.TryResumeHeldFailureAsync(held.Token, Guid.NewGuid(), token)).ShouldBeNull();
        (await store.MarkDeliveredAsync(current, token)).ShouldBe(SagaTimeoutRetirementOutcome.Retired);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NeverResurrectCanceledOracleSourceOrIdenticalReplacement(bool cancelAll)
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(90)); var token = budget.Token;
        await EnsureFailureSchemaAsync(token); var store = NewFailureStore(); var facet = FailureFacet(store);
        var timeout = FailureTimeout(); await store.ScheduleTimeoutAsync(timeout, token);
        var first = (await store.ClaimDueTimeoutsAsync(DateTimeOffset.MaxValue, 1, token)).ShouldHaveSingleItem();
        var observation = new SagaTimeoutFailureObservation(first, MessageFailureKind.Unknown, "original"); var op = Guid.NewGuid();
        var held = RequireFailureHeld(await facet.TryHoldFailureAsync(observation, first, op, token));
        if (cancelAll) { await store.CancelAllTimeoutsAsync(timeout.SagaId, token); }
        else { await store.CancelTimeoutAsync(timeout.SagaId, timeout.TimeoutId, token); }
        (await facet.TryResumeHeldFailureAsync(held.Token, Guid.NewGuid(), token)).ShouldBeNull();
        var ended = await facet.TryHoldFailureAsync(observation, first, op, token);
        ended.Outcome.ShouldBe(SagaTimeoutHoldOutcome.ObservationEnded); ended.Receipt.ShouldBeNull();
        await store.ScheduleTimeoutAsync(timeout, token);
        var replacement = (await store.ClaimDueTimeoutsAsync(DateTimeOffset.MaxValue, 1, token)).ShouldHaveSingleItem();
        replacement.SourceEpoch.ShouldNotBe(first.SourceEpoch); replacement.Timeout.TimeoutData.ShouldBe(first.Timeout.TimeoutData);
        (await facet.TryHoldFailureAsync(observation, replacement, Guid.NewGuid(), token)).Outcome.ShouldBe(SagaTimeoutHoldOutcome.ObservationEnded);
        (await store.MarkDeliveredAsync(first, token)).ShouldBe(SagaTimeoutRetirementOutcome.Superseded);
        (await store.MarkDeliveredAsync(replacement, token)).ShouldBe(SagaTimeoutRetirementOutcome.Retired);
    }

    [Fact]
    public async Task RefuseWrongOraclePartitionAddressAndLeaseWithoutFalseEndProof()
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(90)); var token = budget.Token;
        await EnsureFailureSchemaAsync(token); var store = NewFailureStore(); var facet = FailureFacet(store);
        await store.ScheduleTimeoutAsync(FailureTimeout(), token);
        var claim = (await store.ClaimDueTimeoutsAsync(DateTimeOffset.MaxValue, 1, token)).ShouldHaveSingleItem();
        var observation = new SagaTimeoutFailureObservation(claim, MessageFailureKind.Unknown, "original");
        (await FailureFacet(NewFailureStore(tenant: "hold-tenant ")).TryHoldFailureAsync(observation, claim, Guid.NewGuid(), token)).Outcome.ShouldBe(SagaTimeoutHoldOutcome.Rejected);
        var padded = claim with { Timeout = claim.Timeout with { TimeoutId = claim.Timeout.TimeoutId + " " } };
        (await facet.TryHoldFailureAsync(new(padded, MessageFailureKind.Unknown, "original"), padded, Guid.NewGuid(), token)).Outcome.ShouldBe(SagaTimeoutHoldOutcome.Rejected);
        var saga = claim with { Timeout = claim.Timeout with { SagaId = "another-saga" } };
        (await facet.TryHoldFailureAsync(new(saga, MessageFailureKind.Unknown, "original"), saga, Guid.NewGuid(), token)).Outcome.ShouldBe(SagaTimeoutHoldOutcome.Rejected);
        (await facet.TryHoldFailureAsync(observation, claim with { ClaimToken = "stale-token" }, Guid.NewGuid(), token)).Outcome.ShouldBe(SagaTimeoutHoldOutcome.RetryWithFreshClaim);
        RequireFailureHeld(await facet.TryHoldFailureAsync(observation, claim, Guid.NewGuid(), token));
    }

    [Fact]
    public async Task RollBackOracleHoldAndResolveCanceledPendingWithoutInventingReceipt()
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(90)); var token = budget.Token;
        await EnsureFailureSchemaAsync(token); var store = NewFailureStore(); var facet = FailureFacet(store);
        await store.ScheduleTimeoutAsync(FailureTimeout(), token);
        var claim = (await store.ClaimDueTimeoutsAsync(DateTimeOffset.MaxValue, 1, token)).ShouldHaveSingleItem();
        var observation = new SagaTimeoutFailureObservation(claim, MessageFailureKind.Unknown, "original"); var op = Guid.NewGuid();
        await using var connection = _fixture.CreateConnection(); await connection.OpenAsync(token);
        await connection.ExecuteAsync(new CommandDefinition(
            "CREATE OR REPLACE TRIGGER DISPATCH.REJECT_TFH BEFORE UPDATE OF FailureHeld ON DISPATCH.SAGATIMEOUTS FOR EACH ROW BEGIN IF :NEW.FailureHeld=1 THEN RAISE_APPLICATION_ERROR(-20132,'controlled Oracle hold rollback'); END IF; END;", cancellationToken: token));
        Exception? failure;
        try { failure = await Record.ExceptionAsync(() => facet.TryHoldFailureAsync(observation, claim, op, token).AsTask()); }
        finally { await connection.ExecuteAsync(new CommandDefinition("DROP TRIGGER DISPATCH.REJECT_TFH", cancellationToken: token)); }
        var actual = FindRecoveryOracleError(failure.ShouldNotBeNull()).ShouldNotBeNull();
        actual.Number.ShouldBe(20132); actual.Message.ShouldContain("controlled Oracle hold rollback");
        (await facet.GetHeldFailuresAsync("hold-tenant", null, 10, null, token)).Items.ShouldBeEmpty();
        (await store.GetDueTimeoutsAsync(DateTimeOffset.MaxValue, token)).ShouldHaveSingleItem().TimeoutData.ShouldBe(claim.Timeout.TimeoutData);
        await store.CancelTimeoutAsync(claim.Timeout.SagaId, claim.Timeout.TimeoutId, token);
        var ended = await facet.TryHoldFailureAsync(observation, claim, op, token);
        ended.Outcome.ShouldBe(SagaTimeoutHoldOutcome.ObservationEnded); ended.Receipt.ShouldBeNull();
        var healthy = FailureTimeout(); await store.ScheduleTimeoutAsync(healthy, token);
        var next = (await store.ClaimDueTimeoutsAsync(DateTimeOffset.MaxValue, 1, token)).ShouldHaveSingleItem();
        (await store.MarkDeliveredAsync(next, token)).ShouldBe(SagaTimeoutRetirementOutcome.Retired);
    }

    [Fact]
    public async Task PreserveLegacyOracleManualTableThenMigrateItsActualHeldTarget()
    {
        await WithSelectedCustomTableAsync(async (table, connection, token) =>
        {
            // Explicit legacy fixture: remove ONLY the new hold profile columns from this owned table.
            await connection.ExecuteAsync(new CommandDefinition($"ALTER TABLE DISPATCH.{table} DROP (FailureSourceEpoch,FailureDeliveryEpoch,FailureHeld,FailureVersion,FailureReceipt)", cancellationToken: token));
            var store = NewFailureStore(table); var facet = FailureFacet(store);
            (await Should.ThrowAsync<InvalidOperationException>(() => facet.ValidateFailureStorageAsync(token).AsTask())).Message.ShouldContain("script04");
            await store.ScheduleTimeoutAsync(FailureTimeout(), token);
            var legacy = (await store.ClaimDueTimeoutsAsync(DateTimeOffset.MaxValue, 1, token)).ShouldHaveSingleItem();
            legacy.SourceEpoch.ShouldBeNull(); legacy.DeliveryEpoch.ShouldBeNull();
            (await store.MarkDeliveredAsync(legacy, token)).ShouldBe(SagaTimeoutRetirementOutcome.Retired);
            await store.ScheduleTimeoutAsync(FailureTimeout(), token); // Real live legacy row before migration.
            var script = ReadRecoveryScript("04-SagaTimeoutFailureHolds.sql").Replace("'SAGATIMEOUTS'", "'" + table + "'", StringComparison.Ordinal);
            await connection.ExecuteAsync(new CommandDefinition(script, cancellationToken: token));
            await connection.ExecuteAsync(new CommandDefinition(script, cancellationToken: token));
            await facet.ValidateFailureStorageAsync(token);
            var current = (await store.ClaimDueTimeoutsAsync(DateTimeOffset.MaxValue, 1, token)).ShouldHaveSingleItem();
            current.SourceEpoch.ShouldNotBeNullOrEmpty(); current.DeliveryEpoch.ShouldNotBeNullOrEmpty();
            var held = RequireFailureHeld(await facet.TryHoldFailureAsync(new(current, MessageFailureKind.Unknown, "custom"), current, Guid.NewGuid(), token));
            var foreign = FailureFacet(NewFailureStore());
            (await foreign.TryHoldFailureAsync(new(current, MessageFailureKind.Unknown, "custom"), current, Guid.NewGuid(), token)).Outcome.ShouldBe(SagaTimeoutHoldOutcome.Rejected);
            (await foreign.TryResumeHeldFailureAsync(held.Token, Guid.NewGuid(), token)).ShouldBeNull();
            (await store.ClaimDueTimeoutsAsync(DateTimeOffset.MaxValue, 10, token)).ShouldBeEmpty();
        });
    }

    [Fact]
    public async Task PageOracleHeldSourcesInExactPartitionAndFilter()
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(90)); var token = budget.Token;
        await EnsureFailureSchemaAsync(token); var store = NewFailureStore(); var facet = FailureFacet(store);
        foreach (var id in new[] { "hold-a", "hold-b", "hold-c" })
        {
            await store.ScheduleTimeoutAsync(FailureTimeout(id), token);
            var claim = (await store.ClaimDueTimeoutsAsync(DateTimeOffset.MaxValue, 1, token)).ShouldHaveSingleItem();
            RequireFailureHeld(await facet.TryHoldFailureAsync(new(claim, MessageFailureKind.Unknown, "page"), claim, Guid.NewGuid(), token));
        }
        var page = await facet.GetHeldFailuresAsync("hold-tenant", null, 2, null, token);
        page.Items.Select(item => item.Timeout.TimeoutId).ShouldBe(ExpectedFirstHeldPageIds);
        var next = await facet.GetHeldFailuresAsync("hold-tenant", null, 2, page.NextCursor.ShouldNotBeNull(), token);
        next.Items.ShouldHaveSingleItem().Timeout.TimeoutId.ShouldBe("hold-c"); next.NextCursor.ShouldBeNull();
        await Should.ThrowAsync<ArgumentException>(() => facet.GetHeldFailuresAsync("hold-tenant", "different-filter", 2, page.NextCursor, token).AsTask());
        await Should.ThrowAsync<InvalidOperationException>(() => facet.GetHeldFailuresAsync("hold-tenant ", null, 2, null, token).AsTask());
    }

    [Fact]
    public async Task PreserveOwnedOracleCancellationAuthorityAcrossHoldAndResumeRefusal()
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(90)); var token = budget.Token;
        await EnsureFailureSchemaAsync(token); await EnsureRecoverySchemasAsync(token);
        using var harness = NewRecoveryHarness("hold-tenant");
        var id = Guid.NewGuid().ToString(); await harness.HandleAsync(new(id, "one"), Guid.NewGuid().ToString("N"), token);
        var claim = (await harness.Timers.ClaimDueTimeoutsAsync(DateTimeOffset.MaxValue, 1, token)).ShouldHaveSingleItem();
        var delivery = claim.OwnedDelivery.ShouldNotBeNull();
        var facet = FailureFacet(harness.Timers);
        var held = RequireFailureHeld(await facet.TryHoldFailureAsync(new(claim, MessageFailureKind.Unknown, "owned"), claim, Guid.NewGuid(), token));
        await harness.Timers.CancelAllTimeoutsAsync(id, token);
        (await facet.TryResumeHeldFailureAsync(held.Token, Guid.NewGuid(), token)).ShouldBeNull();
        (await harness.Receiver.IsTimeoutDeliveryCurrentAsync(delivery, token)).ShouldBeFalse();
        (await harness.Timers.GetDueTimeoutsAsync(DateTimeOffset.MaxValue, token)).ShouldBeEmpty();
    }

    [Fact]
    public async Task RebindOracleFailureOnlyAfterARealFreshServerEligibleClaim()
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(20)); var token = budget.Token;
        await EnsureFailureSchemaAsync(token); var store = NewFailureStore(leaseSeconds: 1); var facet = FailureFacet(store);
        await store.ScheduleTimeoutAsync(FailureTimeout(), token);
        var first = (await store.ClaimDueTimeoutsAsync(DateTimeOffset.MaxValue, 1, token)).ShouldHaveSingleItem();
        var observation = new SagaTimeoutFailureObservation(first, MessageFailureKind.Unknown, "original");
        ClaimedSagaTimeout? next = null;
        while (next is null)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50), token); // delay-ok: Poll an actual fresh Oracle server-eligible claim under the owning budget, preserving claim and epoch assertions.
            next = (await store.ClaimDueTimeoutsAsync(DateTimeOffset.MaxValue, 1, token)).SingleOrDefault();
        }
        next.ClaimToken.ShouldNotBe(first.ClaimToken); next.SourceEpoch.ShouldBe(first.SourceEpoch); next.DeliveryEpoch.ShouldBe(first.DeliveryEpoch);
        (await facet.TryHoldFailureAsync(observation, first, Guid.NewGuid(), token)).Outcome.ShouldBe(SagaTimeoutHoldOutcome.RetryWithFreshClaim);
        RequireFailureHeld(await facet.TryHoldFailureAsync(observation, next, Guid.NewGuid(), token));
    }

    [Fact]
    public async Task RefuseConstantOracleEpochDefaultsAtStartupAndMigrationWithoutChangingThem()
    {
        await WithSelectedCustomTableAsync(async (table, connection, token) =>
        {
            await connection.ExecuteAsync(new CommandDefinition($"ALTER TABLE DISPATCH.{table} MODIFY (FailureSourceEpoch DEFAULT HEXTORAW('11111111111111111111111111111111'),FailureDeliveryEpoch DEFAULT HEXTORAW('22222222222222222222222222222222'))", cancellationToken: token));
            var facet = FailureFacet(NewFailureStore(table));
            (await Should.ThrowAsync<InvalidOperationException>(() => facet.ValidateFailureStorageAsync(token).AsTask())).Message.ShouldContain("defaults");
            var script = ReadRecoveryScript("04-SagaTimeoutFailureHolds.sql").Replace("'SAGATIMEOUTS'", "'" + table + "'", StringComparison.Ordinal);
            var error = await Record.ExceptionAsync(() => connection.ExecuteAsync(new CommandDefinition(script, cancellationToken: token)));
            var actual = FindRecoveryOracleError(error.ShouldNotBeNull()).ShouldNotBeNull();
            actual.Number.ShouldBe(20131); actual.Message.ShouldContain("default definitions");
            // Both constant definitions remain: migration refused before rewriting consumer metadata.
            var definitions = (await connection.QueryAsync<string>(new CommandDefinition(
                "SELECT DATA_DEFAULT FROM ALL_TAB_COLUMNS WHERE OWNER='DISPATCH' AND TABLE_NAME=:TableName AND COLUMN_NAME IN ('FAILURESOURCEEPOCH','FAILUREDELIVERYEPOCH')",
                new { TableName = table }, cancellationToken: token))).ToArray();
            definitions.Length.ShouldBe(2); definitions.Any(value => value.Contains("11111111111111111111111111111111", StringComparison.Ordinal)).ShouldBeTrue();
            definitions.Any(value => value.Contains("22222222222222222222222222222222", StringComparison.Ordinal)).ShouldBeTrue();
        });
    }

    private async Task EnsureFailureSchemaAsync(CancellationToken token)
    {
        _fixture.DockerAvailable.ShouldBeTrue("Real Oracle hold conformance never skips unavailable infrastructure.");
        await _fixture.EnsureInitializedAsync();
        await using var connection = _fixture.CreateConnection(); await connection.OpenAsync(token);
        var script = ReadRecoveryScript("04-SagaTimeoutFailureHolds.sql");
        await connection.ExecuteAsync(new CommandDefinition(script, cancellationToken: token));
        await connection.ExecuteAsync(new CommandDefinition(script, cancellationToken: token));
    }
    private OracleSagaTimeoutStore NewFailureStore(string table = "SAGATIMEOUTS", string tenant = "hold-tenant", int leaseSeconds = 120) =>
        new(_fixture.ConnectionString, global::Microsoft.Extensions.Options.Options.Create(new OracleSagaTimeoutStoreOptions { TableName = table, LeaseTimeoutSeconds = leaseSeconds }),
            NullLogger<OracleSagaTimeoutStore>.Instance, new Excalibur.Integration.Tests.Infrastructure.FixedTestTenantContext(tenant));
    private static ISagaTimeoutFailureStore FailureFacet(ISagaTimeoutStore store) =>
        ((IServiceProvider)store).GetService(typeof(ISagaTimeoutFailureStore)) as ISagaTimeoutFailureStore
        ?? throw new InvalidOperationException("Actual selected Oracle timeout store lost its hold capability.");
    private static SagaTimeout FailureTimeout(string? id = null) => new(id ?? Guid.NewGuid().ToString("N"), "manual-failure", "Saga", "Timeout", [1, 2, 3],
        DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
    private static SagaTimeoutHoldReceipt RequireFailureHeld(SagaTimeoutHoldAttempt attempt)
    { attempt.Outcome.ShouldBe(SagaTimeoutHoldOutcome.Held); return attempt.Receipt.ShouldNotBeNull(); }
}
