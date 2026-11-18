// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Messaging;

namespace Excalibur.Saga.Orchestration;

public abstract partial class SagaBase<TSagaState>
{
    // Captures actual buffered timer operations without calling a separately committing timer
    // store. The selected participant/domain/table must already have passed admission.
    internal IReadOnlyList<SagaPreparedTimerMutation> CapturePreparedTimers(long generation)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(generation);
        var mutations = new List<SagaPreparedTimerMutation>(_pendingTimerIntents.Count);
        foreach (var intent in _pendingTimerIntents)
        {
            switch (intent.Action)
            {
                case SagaTimerAction.Schedule:
                    var timeout = intent.Timeout
                        ?? throw new InvalidOperationException("A scheduled timer has no original timer envelope.");
                    mutations.Add(new SagaPreparedTimerMutation(SagaPreparedTimerAction.Schedule,
                        intent.TimeoutId, generation, timeout.TimeoutType, timeout.TimeoutData,
                        timeout.DueAt, timeout.ScheduledAt, timeout.SagaType));
                    break;
                case SagaTimerAction.Cancel:
                    mutations.Add(new SagaPreparedTimerMutation(SagaPreparedTimerAction.Cancel,
                        intent.TimeoutId, generation, null, null, null, null));
                    break;
                case SagaTimerAction.CancelAll:
                    mutations.Add(new SagaPreparedTimerMutation(SagaPreparedTimerAction.CancelAll,
                        string.Empty, generation, null, null, null, null));
                    // Cancellation retires the current scheduling generation. A deliberately
                    // scheduled later timer belongs to the next generation, not a resurrected old one.
                    generation = checked(generation + 1);
                    break;
                default:
                    throw new InvalidOperationException("Unknown buffered saga timer operation.");
            }
        }

        return mutations.AsReadOnly();
    }
}
