// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Delivery;
using Excalibur.Testing;
using Excalibur.Testing.Conformance;
using Shouldly;
using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "STORE")]
public sealed class InMemoryScheduleFailureConformanceShould : ScheduleFailureStoreConformanceTestKit
{
    protected override IScheduleExecutionFailureStore CreateStore() => new InMemoryScheduleStore();
    [Fact]
    public override Task CapturedBudgetCannotBeWidenedOrErased() => base.CapturedBudgetCannotBeWidenedOrErased();
    [Fact]
    public override Task QuarantineRecoveryPreservesHistoryAndDeduplicates() => base.QuarantineRecoveryPreservesHistoryAndDeduplicates();
    [Fact]
    public override Task UnsettledWorkAndTenantInventoryRemainProtected() => base.UnsettledWorkAndTenantInventoryRemainProtected();
    [Fact]
    public override Task FailureMutationRequiresCurrentUncancelledAuthority() => base.FailureMutationRequiresCurrentUncancelledAuthority();
    [Fact]
    public override Task ConformanceSuite_ShouldWireEveryArm() => base.ConformanceSuite_ShouldWireEveryArm();
}

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "STORE")]
public sealed class ScheduleFailureConformanceMutantsShould
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task RejectBudgetWideningPrematureQuarantineAndDuplicateRecovery(int mutation)
    {
        var probe = new Probe(new FaultStore(new InMemoryScheduleStore(), mutation));
        var error = await Should.ThrowAsync<TestFixtureAssertionException>(() => mutation == 0
            ? probe.CapturedBudgetCannotBeWidenedOrErased() : probe.QuarantineRecoveryPreservesHistoryAndDeduplicates());
        error.Message.ShouldContain(mutation switch
        {
            0 => "widened",
            1 => "before the admission bound",
            _ => "Concurrent replay",
        });
    }

    private sealed class Probe(IScheduleExecutionFailureStore store) : ScheduleFailureStoreConformanceTestKit
    {
        protected override IScheduleExecutionFailureStore CreateStore() => store;
    }

    private sealed class FaultStore(IScheduleExecutionFailureStore inner, int mutation) : IScheduleExecutionFailureStore
    {
        public async Task<ScheduleFailureExecutionAdmission?> TryAcquireExecutionAsync(Guid id, Guid generation,
            DateTimeOffset occurrence, int requested, CancellationToken ct)
        {
            var result = await inner.TryAcquireExecutionAsync(id, generation, occurrence, requested, ct);
            return result is not null && mutation == 0 ? result with { CapturedMaxAdmissionAttempts = requested } : result;
        }
        public Task<bool> TryRecordFailureAsync(ScheduleExecutionLease lease, ScheduleOccurrenceFailure failure, CancellationToken ct) =>
            mutation == 1 && failure.Disposition == ScheduleFailureDisposition.Quarantined
                ? Task.FromResult(true) : inner.TryRecordFailureAsync(lease, failure, ct);
        public async Task<ScheduleQuarantineRecoveryReceipt?> TryResumeQuarantinedOccurrenceAsync(ScheduleQuarantineToken token, Guid command, CancellationToken ct)
        {
            var result = await inner.TryResumeQuarantinedOccurrenceAsync(token, command, ct);
            return result is not null && mutation == 2 ? result with { FreshGeneration = Guid.NewGuid() } : result;
        }
        public Task<bool> TryCompleteExecutionAsync(ScheduleExecutionLease lease, ScheduleExecutionResult result,
            ScheduleExecutionCompletionDisposition disposition, CancellationToken ct) => inner.TryCompleteExecutionAsync(lease, result, disposition, ct);
        public Task<ScheduleOccurrenceFailureSnapshot?> GetOccurrenceFailureAsync(Guid id, Guid generation, DateTimeOffset occurrence,
            string? tenant, CancellationToken ct) => inner.GetOccurrenceFailureAsync(id, generation, occurrence, tenant, ct);
        public Task<IReadOnlyList<ScheduleOccurrenceFailureSnapshot>> GetQuarantinesAsync(string? tenant, Guid? cursor,
            int maximum, CancellationToken ct) => inner.GetQuarantinesAsync(tenant, cursor, maximum, ct);
        public Task<ScheduleExecutionLease?> TryAcquireExecutionAsync(Guid id, Guid generation, DateTimeOffset occurrence,
            CancellationToken ct) => inner.TryAcquireExecutionAsync(id, generation, occurrence, ct);
        public Task<bool> TryUpdateProgressAsync(ScheduleExecutionLease lease, ScheduleExecutionResult result, CancellationToken ct) => inner.TryUpdateProgressAsync(lease, result, ct);
        public Task<bool> ReleaseExecutionAsync(ScheduleExecutionLease lease, CancellationToken ct) => inner.ReleaseExecutionAsync(lease, ct);
        public Task<IReadOnlyList<ScheduleExecutionSnapshot>> GetExecutionSnapshotsAsync(CancellationToken ct) => inner.GetExecutionSnapshotsAsync(ct);
        public Task<IEnumerable<IScheduledMessage>> GetAllAsync(CancellationToken ct) => inner.GetAllAsync(ct);
        public Task StoreAsync(IScheduledMessage row, CancellationToken ct) => inner.StoreAsync(row, ct);
        public Task CompleteAsync(Guid id, CancellationToken ct) => inner.CompleteAsync(id, ct);
    }
}
