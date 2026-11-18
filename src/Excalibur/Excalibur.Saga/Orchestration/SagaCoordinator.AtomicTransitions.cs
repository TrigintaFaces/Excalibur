// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Handlers;

using Microsoft.Extensions.DependencyInjection;

using AtomicAttemptScope = Excalibur.Saga.Orchestration.SagaAtomicAttemptExecutor.AtomicAttemptScope;

namespace Excalibur.Saga.Orchestration;

public sealed partial class SagaCoordinator
{
    private readonly AsyncLocal<AtomicAttemptScope?> _atomicAttemptScope = new();

    [RequiresUnreferencedCode("The runtime saga entry instantiates registered saga types and timeout handlers reflectively.")]
    [RequiresDynamicCode("Use the explicit typed atomic entry with generated preparation for NativeAOT.")]
    internal Task HandleAtomicEventRuntimeAsync<TSaga, TState>(SagaAtomicPreparationPolicy policy,
        IMessageContext context, ISagaEvent evt, SagaInfo sagaInfo, CancellationToken cancellationToken)
        where TSaga : SagaBase<TState>
        where TState : SagaState, new()
        => HandleAtomicAttemptAsync<TSaga, TState>(policy, context, evt, sagaInfo,
            state => ActivatorUtilities.CreateInstance<TSaga>(serviceProvider, state),
            (saga, token) => InvokeAtomicRuntimeHandlerAsync<TSaga, TState>(saga, evt, token),
            saga => typeof(ISagaTimeout<>).MakeGenericType(evt.GetType()).IsInstanceOfType(saga), cancellationToken);

    [RequiresUnreferencedCode("Selects runtime timeout handler interfaces through reflection.")]
    [RequiresDynamicCode("Constructs a closed timeout handler interface at runtime.")]
    private static async Task<SagaEventOutcome> InvokeAtomicRuntimeHandlerAsync<TSaga, TState>(
        TSaga saga, ISagaEvent evt, CancellationToken cancellationToken)
        where TSaga : SagaBase<TState>
        where TState : SagaState
    {
        if (TryInvokeTimeoutHandler(saga, evt, cancellationToken, out var timeoutTask))
        {
            await timeoutTask.ConfigureAwait(false);
            return SagaEventOutcome.Handled;
        }

        return saga.HandlesEvent(evt)
            ? await saga.HandleAsync(evt, cancellationToken).ConfigureAwait(false)
            : SagaEventOutcome.Declined;
    }


    private Task HandleAtomicAttemptAsync<TSaga, TState>(
        SagaAtomicPreparationPolicy policy, IMessageContext context, ISagaEvent evt,
        SagaInfo sagaInfo, Func<TState, TSaga> createSaga,
        Func<TSaga, CancellationToken, Task<SagaEventOutcome>> invokeHandler,
        Func<TSaga, bool> isTimeoutHandler, CancellationToken cancellationToken)
        where TSaga : SagaBase<TState>
        where TState : SagaState, new()
        => new SagaAtomicAttemptExecutor(serviceProvider, _sagaStore, ResolveSagaId, ReplayKeyPrefix, logger)
            .HandleAtomicAttemptAsync(_atomicAttemptScope.Value ?? new AtomicAttemptScope(), policy,
                context, evt, sagaInfo, createSaga, invokeHandler, isTimeoutHandler, cancellationToken);
}
