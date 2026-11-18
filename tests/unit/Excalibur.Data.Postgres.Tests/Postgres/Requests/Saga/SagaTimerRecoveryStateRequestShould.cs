// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;
using Excalibur.Saga.Postgres;

namespace Excalibur.Data.Tests.Postgres.Requests.Saga;

[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class SagaTimerRecoveryStateRequestShould
{
    [Fact]
    public void BindLifetimeNonceForUntrackedInsertAndConditionalUpdate()
    {
        using var serializer = new DispatchJsonSerializer();
        var state = new ProbeState { SagaId = Guid.NewGuid() };
        var inserted = new SaveSagaRequest<ProbeState>(state, new PostgresSagaOptions { ConnectionString = "Host=localhost;Database=unused;" }, serializer, TenantScope.Scoped("tenant-a"), CancellationToken.None);
        inserted.Command.CommandText.ShouldContain("timer_recovery_incarnation");
        inserted.Parameters.Get<string>("RecoveryIncarnation").ShouldBe(state.TimerRecoveryIncarnationId.ToString("N"));
        state.Version = 1;
        var update = new SaveSagaRequest<ProbeState>(state, new PostgresSagaOptions { ConnectionString = "Host=localhost;Database=unused;" }, serializer, TenantScope.Scoped("tenant-a"), CancellationToken.None);
        update.Command.CommandText.ShouldContain("timer_recovery_incarnation IS NULL OR timer_recovery_incarnation = @RecoveryIncarnation");
        update.Parameters.Get<string>("RecoveryIncarnation").ShouldBe(state.TimerRecoveryIncarnationId.ToString("N"));
        update.Parameters.Get<long>("ExpectedVersion").ShouldBe(1);
    }

    [Fact]
    public void BindFullPendingIdentityAndExactAcknowledgementWithVersionAndTenant()
    {
        using var serializer = new DispatchJsonSerializer();
        var state = new ProbeState { SagaId = Guid.NewGuid(), Version = 1, TimerRecoverySequence = 1 };
        var journal = new SagaTimerRecoveryJournal(new SagaScopeIdentity("tenant-a", nameof(ProbeState), state.SagaId),
            state.TimerRecoveryIncarnationId, 1, Guid.NewGuid(), Guid.NewGuid(), 0, 1, [1, 0, 0, 0, 1, 0, 0, 0, 2, 0, 0, 0, 0]);
        state.PendingTimerRecovery = journal;
        var update = new SaveSagaRequest<ProbeState>(state, new PostgresSagaOptions { ConnectionString = "Host=localhost;Database=unused;" }, serializer, TenantScope.Scoped("tenant-a"), CancellationToken.None);
        update.Command.CommandText.ShouldContain("timer_recovery_pending_batch IS NULL OR");
        update.Command.CommandText.ShouldContain("timer_recovery_pending_digest = @RecoveryDigest");
        update.Parameters.Get<string>("RecoveryBatch").ShouldBe(journal.BatchId.ToString("N"));
        update.Parameters.Get<string>("RecoveryDigest").ShouldBe(Convert.ToHexString(journal.GetAuthorityDigest()));
        update.Parameters.Get<string>("TenantId").ShouldBe("tenant-a");
        update.Parameters.Get<long>("ExpectedVersion").ShouldBe(1);
        state.PendingTimerRecovery = null;
        state.LastTimerRecoveryBatchId = journal.BatchId;
        var ack = new SaveSagaRequest<ProbeState>(state, new PostgresSagaOptions { ConnectionString = "Host=localhost;Database=unused;" }, serializer, TenantScope.Scoped("tenant-a"), CancellationToken.None);
        ack.Parameters.Get<string>("RecoveryLastAck").ShouldBe(journal.BatchId.ToString("N"));
        ack.Parameters.Get<string?>("RecoveryBatch").ShouldBeNull();
    }

    private sealed class ProbeState : SagaState
    {
        public ProbeState() { }
    }
}
