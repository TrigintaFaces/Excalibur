// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Delivery;
using Microsoft.Extensions.Time.Testing;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "Concurrency")]
public sealed class InMemoryScheduleFailureAuthorityShould
{
    [Fact]
    public async Task CaptureBudgetBeforeFailureAndRefuseBothWideningAndInheritedProgress()
    {
        var clock = Clock();
        var store = new InMemoryScheduleStore(clock);
        var row = Row(clock);
        await store.StoreAsync(row, CancellationToken.None);
        var snapshot = (await store.GetExecutionSnapshotsAsync(CancellationToken.None)).Single();
        var first = (await store.TryAcquireExecutionAsync(row.Id, snapshot.Generation, row.NextExecutionUtc!.Value, 2, CancellationToken.None)).ShouldNotBeNull();
        first.PolicySelection.ShouldBe(ScheduleAdmissionPolicySelection.CapturedNew);
        first.CapturedMaxAdmissionAttempts.ShouldBe(2);
        (await store.ReleaseExecutionAsync(first.Lease, CancellationToken.None)).ShouldBeTrue();
        var second = (await store.TryAcquireExecutionAsync(row.Id, snapshot.Generation, row.NextExecutionUtc!.Value, 200, CancellationToken.None)).ShouldNotBeNull();
        second.PolicySelection.ShouldBe(ScheduleAdmissionPolicySelection.UsedExisting);
        second.CapturedMaxAdmissionAttempts.ShouldBe(2);
        second.Lease.AdmissionAttempt.ShouldBe(2);
        (await store.TryUpdateProgressAsync(second.Lease, new(null, clock.GetUtcNow().AddDays(1)), CancellationToken.None)).ShouldBeFalse();
        (await store.TryRecordFailureAsync(second.Lease, new(ScheduleFailureReason.HandlerFailed, ScheduleFailureDisposition.Retrying), CancellationToken.None)).ShouldBeTrue();
        var failure = (await store.GetOccurrenceFailureAsync(row.Id, snapshot.Generation, row.NextExecutionUtc!.Value, null, CancellationToken.None)).ShouldNotBeNull();
        failure.MaxAdmissionAttempts.ShouldBe(2);
        failure.AdmissionAttempts.ShouldBe(2);
        (await store.GetAllAsync(CancellationToken.None)).Single().NextExecutionUtc.ShouldBe(row.NextExecutionUtc);
        (await store.TryCompleteExecutionAsync(second.Lease, new(null, clock.GetUtcNow().AddDays(1)),
            ScheduleExecutionCompletionDisposition.PolicySkipped, CancellationToken.None)).ShouldBeFalse();
    }

    [Fact]
    public async Task RefuseConsumedLegacyHistoryInsteadOfGuessingABudget()
    {
        var clock = Clock();
        var store = new InMemoryScheduleStore(clock);
        var row = Row(clock);
        await store.StoreAsync(row, CancellationToken.None);
        var snapshot = (await store.GetExecutionSnapshotsAsync(CancellationToken.None)).Single();
        var legacy = (await store.TryAcquireExecutionAsync(row.Id, snapshot.Generation, row.NextExecutionUtc!.Value, CancellationToken.None)).ShouldNotBeNull();
        await store.ReleaseExecutionAsync(legacy, CancellationToken.None);
        var error = await Should.ThrowAsync<InvalidOperationException>(() => store.TryAcquireExecutionAsync(
            row.Id, snapshot.Generation, row.NextExecutionUtc!.Value, 100, CancellationToken.None));
        error.Message.ShouldContain("legacy");
        (await store.GetAllAsync(CancellationToken.None)).Single().NextExecutionUtc.ShouldBe(row.NextExecutionUtc);
    }

    [Fact]
    public async Task QuarantineAtTheBoundAndResumeIdempotentlyWithoutErasingHistory()
    {
        var clock = Clock();
        var store = new InMemoryScheduleStore(clock);
        var row = Row(clock);
        await store.StoreAsync(row, CancellationToken.None);
        var original = (await store.GetExecutionSnapshotsAsync(CancellationToken.None)).Single();
        var first = (await store.TryAcquireExecutionAsync(row.Id, original.Generation, row.NextExecutionUtc!.Value, 1, CancellationToken.None)).ShouldNotBeNull();
        var retry = new ScheduleOccurrenceFailure(ScheduleFailureReason.MalformedPayload, ScheduleFailureDisposition.Retrying);
        (await store.TryRecordFailureAsync(first.Lease, retry, CancellationToken.None)).ShouldBeTrue();
        var beforeReplay = (await store.GetOccurrenceFailureAsync(row.Id, original.Generation, row.NextExecutionUtc!.Value, null, CancellationToken.None)).ShouldNotBeNull();
        (await store.TryRecordFailureAsync(first.Lease, retry, CancellationToken.None)).ShouldBeTrue();
        (await store.GetOccurrenceFailureAsync(row.Id, original.Generation, row.NextExecutionUtc!.Value, null, CancellationToken.None)).ShouldNotBeNull().Token.ShouldBe(beforeReplay.Token);
        await store.ReleaseExecutionAsync(first.Lease, CancellationToken.None);
        var bound = (await store.TryAcquireExecutionAsync(row.Id, original.Generation, row.NextExecutionUtc!.Value, 100, CancellationToken.None)).ShouldNotBeNull();
        bound.Lease.AdmissionAttempt.ShouldBe(2);
        bound.CapturedMaxAdmissionAttempts.ShouldBe(1);
        (await store.TryRecordFailureAsync(bound.Lease, new(ScheduleFailureReason.AdmissionBudgetExhausted, ScheduleFailureDisposition.Quarantined), CancellationToken.None)).ShouldBeTrue();
        await store.ReleaseExecutionAsync(bound.Lease, CancellationToken.None);
        var quarantine = (await store.GetOccurrenceFailureAsync(row.Id, original.Generation, row.NextExecutionUtc!.Value, null, CancellationToken.None)).ShouldNotBeNull();
        quarantine.Message.MessageBody.ShouldBe(row.MessageBody);
        quarantine.Message.NextExecutionUtc.ShouldBe(row.NextExecutionUtc);
        (await store.GetAllAsync(CancellationToken.None)).Single().Enabled.ShouldBeFalse();
        await Should.ThrowAsync<InvalidOperationException>(() => store.StoreAsync(row, CancellationToken.None));
        await store.CompleteAsync(row.Id, CancellationToken.None);
        (await store.GetOccurrenceFailureAsync(row.Id, original.Generation, row.NextExecutionUtc!.Value, null, CancellationToken.None)).ShouldNotBeNull().Token.ShouldBe(quarantine.Token);
        var command = Guid.NewGuid();
        var receipts = await Task.WhenAll(store.TryResumeQuarantinedOccurrenceAsync(quarantine.Token, command, CancellationToken.None),
            store.TryResumeQuarantinedOccurrenceAsync(quarantine.Token, command, CancellationToken.None));
        var receipt = receipts[0].ShouldNotBeNull();
        receipts[1].ShouldBe(receipt);
        receipt.FreshGeneration.ShouldNotBe(original.Generation);
        receipt.MaxAdmissionAttempts.ShouldBe(1);
        (await store.TryResumeQuarantinedOccurrenceAsync(quarantine.Token, Guid.NewGuid(), CancellationToken.None)).ShouldBeNull();
        (await store.TryRecordFailureAsync(bound.Lease, retry, CancellationToken.None)).ShouldBeFalse();
        var resumed = (await store.TryAcquireExecutionAsync(row.Id, receipt.FreshGeneration, row.NextExecutionUtc!.Value, 100, CancellationToken.None)).ShouldNotBeNull();
        resumed.Lease.AdmissionAttempt.ShouldBe(1);
        resumed.CapturedMaxAdmissionAttempts.ShouldBe(1);
        (await store.TryCompleteExecutionAsync(resumed.Lease, new(clock.GetUtcNow(), clock.GetUtcNow().AddMinutes(1)),
            ScheduleExecutionCompletionDisposition.Fulfilled, CancellationToken.None)).ShouldBeTrue();
        await store.ReleaseExecutionAsync(resumed.Lease, CancellationToken.None);
        await store.StoreAsync(row, CancellationToken.None);
        (await store.TryResumeQuarantinedOccurrenceAsync(quarantine.Token, command, CancellationToken.None)).ShouldBe(receipt);
        (await store.GetOccurrenceFailureAsync(row.Id, original.Generation, row.NextExecutionUtc!.Value, null, CancellationToken.None)).ShouldNotBeNull().RecoveryReceipt.ShouldBe(receipt);
    }

    [Fact]
    public async Task KeepFailureSnapshotsDetachedAndQuarantinePagesTenantBounded()
    {
        var clock = Clock();
        var store = new InMemoryScheduleStore(clock);
        var row = Row(clock);
        row.TenantId = "Tenant-A";
        await store.StoreAsync(row, CancellationToken.None);
        var snapshot = (await store.GetExecutionSnapshotsAsync(CancellationToken.None)).Single();
        var admission = (await store.TryAcquireExecutionAsync(row.Id, snapshot.Generation, row.NextExecutionUtc!.Value, 2, CancellationToken.None)).ShouldNotBeNull();
        await store.TryRecordFailureAsync(admission.Lease, new(ScheduleFailureReason.HandlerFailed, ScheduleFailureDisposition.Retrying), CancellationToken.None);
        await store.CompleteAsync(row.Id, CancellationToken.None);
        var failure = (await store.GetOccurrenceFailureAsync(row.Id, snapshot.Generation, row.NextExecutionUtc!.Value, "Tenant-A", CancellationToken.None)).ShouldNotBeNull();
        failure.Message.MessageBody = "changed observation";
        (await store.GetOccurrenceFailureAsync(row.Id, snapshot.Generation, row.NextExecutionUtc!.Value, "Tenant-A", CancellationToken.None)).ShouldNotBeNull().Message.MessageBody.ShouldBe(row.MessageBody);
        (await store.GetQuarantinesAsync("Tenant-B", null, 1, CancellationToken.None)).ShouldBeEmpty();
        (await store.GetQuarantinesAsync("Tenant-A", null, 1, CancellationToken.None)).Count.ShouldBe(1);
        (await store.GetQuarantinesAsync("Tenant-A", failure.Token.QuarantineId, 1, CancellationToken.None)).ShouldBeEmpty();
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => store.GetQuarantinesAsync("Tenant-A", null, 257, CancellationToken.None));
    }

    [Theory]
    [InlineData(ScheduleExecutionCompletionDisposition.PolicySkipped)]
    [InlineData(ScheduleExecutionCompletionDisposition.PolicyDisabled)]
    public async Task PreserveFirstAdmissionCatchupDispositionWithoutCallingItFulfilled(ScheduleExecutionCompletionDisposition disposition)
    {
        var clock = Clock();
        var store = new InMemoryScheduleStore(clock);
        var row = Row(clock);
        await store.StoreAsync(row, CancellationToken.None);
        var snapshot = (await store.GetExecutionSnapshotsAsync(CancellationToken.None)).Single();
        var admission = (await store.TryAcquireExecutionAsync(row.Id, snapshot.Generation, row.NextExecutionUtc!.Value, 2, CancellationToken.None)).ShouldNotBeNull();
        var disabled = disposition == ScheduleExecutionCompletionDisposition.PolicyDisabled;
        (await store.TryCompleteExecutionAsync(admission.Lease,
            new(null, disabled ? row.NextExecutionUtc : clock.GetUtcNow().AddMinutes(1), disabled), disposition, CancellationToken.None)).ShouldBeTrue();
        var current = (await store.GetAllAsync(CancellationToken.None)).Single();
        current.LastExecutionUtc.ShouldBeNull();
        current.Enabled.ShouldBe(!disabled);
    }

    private static FakeTimeProvider Clock() => new(new DateTimeOffset(3000, 1, 1, 0, 0, 0, TimeSpan.Zero));
    private static ScheduledMessage Row(FakeTimeProvider clock) => new()
    {
        Id = Guid.NewGuid(), Enabled = true, NextExecutionUtc = clock.GetUtcNow(), Interval = TimeSpan.FromMinutes(1),
        MessageName = "retained-exact-type", MessageBody = "{malformed original payload", CorrelationId = "producer-correlation",
    };
}
