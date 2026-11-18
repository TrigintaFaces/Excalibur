// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Runtime.ExceptionServices;

using Excalibur.Dispatch;

namespace Excalibur.Saga.Orchestration;

public abstract partial class SagaBase<TSagaState>
{
    private readonly List<Task<IPreparedTransactionalOutboxWrite>> _pendingPreparedWrites = [];

    private bool _emissionCaptureSealed;

    // Selected before the handler. The owning provider freezes encoded metadata before
    // its first transform await; this layer never reconstructs the provider's wire.
    internal Func<IDispatchMessage, IMessageContext, CancellationToken,
        ValueTask<IPreparedTransactionalOutboxWrite>>? AtomicEmissionPreparer { get; set; }

    internal async Task<IReadOnlyList<IPreparedTransactionalOutboxWrite>> JoinPreparedEmissionsAsync()
    {
        _emissionCaptureSealed = true;
        var prepared = new List<IPreparedTransactionalOutboxWrite>(_pendingPreparedWrites.Count);
        ExceptionDispatchInfo? primary = null;
        foreach (var operation in _pendingPreparedWrites)
        {
            try
            {
                prepared.Add(await operation.ConfigureAwait(false));
            }
            catch (Exception error)
            {
                // Join every operation started by the handler, including calls it did not await.
                primary ??= ExceptionDispatchInfo.Capture(error);
            }
        }

        primary?.Throw();
        return prepared.AsReadOnly();
    }

    private Task BufferEmissionAsync(IDispatchMessage message, CancellationToken cancellationToken)
    {
        if (_emissionCaptureSealed)
        {
            return Task.FromException(new InvalidOperationException("Saga emission capture is already sealed."));
        }

        var context = CreateSagaCorrelatedContext();
        if (AtomicEmissionPreparer is not { } prepare)
        {
            _pendingDispatches.Add((message, context));
            return Task.CompletedTask;
        }

        // Reserve FIFO position BEFORE invoking caller/provider code: synchronous serialization
        // may reenter Send/Publish. Unwrap preserves the actual operation's result/fault/status.
        var slot = new TaskCompletionSource<Task<IPreparedTransactionalOutboxWrite>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingPreparedWrites.Add(slot.Task.Unwrap());
        Task<IPreparedTransactionalOutboxWrite> operation;
        try
        {
            operation = RequirePreparedWriteAsync(prepare(message, context, cancellationToken));
        }
        catch (Exception error)
        {
            operation = Task.FromException<IPreparedTransactionalOutboxWrite>(error);
        }

        slot.SetResult(operation);
        return operation;
    }

    private static async Task<IPreparedTransactionalOutboxWrite> RequirePreparedWriteAsync(
        ValueTask<IPreparedTransactionalOutboxWrite> operation)
    {
        return await operation.ConfigureAwait(false)
            ?? throw new InvalidOperationException("The owning outbox preparer returned no prepared write.");
    }
}
