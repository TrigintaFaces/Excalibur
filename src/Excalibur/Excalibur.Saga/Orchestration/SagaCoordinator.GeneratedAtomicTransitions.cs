// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Abstractions;

using Microsoft.Extensions.DependencyInjection;

using AtomicAttemptScope = Excalibur.Saga.Orchestration.SagaAtomicAttemptExecutor.AtomicAttemptScope;

namespace Excalibur.Saga.Orchestration;

public sealed partial class SagaCoordinator
{
    /// <summary>Processes one exactly typed event using explicit generated preparation and compiled timeout dispatch.</summary>
    /// <typeparam name="TSaga">The registered saga with its public constructors preserved.</typeparam>
    /// <typeparam name="TState">The registered saga state present in the selected metadata.</typeparam>
    /// <typeparam name="TEvent">The exact runtime event type known to the compiled caller.</typeparam>
    /// <param name="context">The original delivery context.</param>
    /// <param name="evt">The original event.</param>
    /// <param name="cancellationToken">The caller cancellation token.</param>
    /// <returns>The known outcome processing operation; uncertain authority is not treated as rollback.</returns>
    /// <remarks>
    /// Requires explicit generated durable registration, a metadata-capable configured payload codec,
    /// and selected stores supporting mediated atomic preparation. State metadata alone does not certify
    /// arbitrary custom codecs. No reflection serialization or runtime timeout-interface fallback is selected.
    /// This API does not promise physical duplicate arrival or downstream completion ordering.
    /// </remarks>
    public Task ProcessAtomicEventAsync<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TSaga, TState, TEvent>(
        IMessageContext context, TEvent evt, CancellationToken cancellationToken)
        where TSaga : SagaBase<TState>
        where TState : SagaState, new()
        where TEvent : ISagaEvent
        => ProcessGeneratedAtomicCoreAsync<TSaga, TState, TEvent>(context, evt, manualTimeout: false, cancellationToken);

    /// <summary>Explicitly admits a new nonowned timeout through compiled generated dispatch.</summary>
    /// <typeparam name="TSaga">The registered typed timeout handler.</typeparam>
    /// <typeparam name="TState">The generated saga state.</typeparam>
    /// <typeparam name="TEvent">The exact timeout event.</typeparam>
    /// <param name="context">The fresh nonowned delivery context.</param>
    /// <param name="evt">The exact registered timeout event.</param>
    /// <param name="cancellationToken">The caller cancellation token.</param>
    /// <returns>The atomic manual-timeout processing operation.</returns>
    public Task ProcessManualAtomicTimeoutAsync<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TSaga, TState, TEvent>(
        IMessageContext context, TEvent evt, CancellationToken cancellationToken)
        where TSaga : SagaBase<TState>, ISagaTimeout<TEvent>
        where TState : SagaState, new()
        where TEvent : ISagaEvent
        => ProcessGeneratedAtomicCoreAsync<TSaga, TState, TEvent>(context, evt, manualTimeout: true, cancellationToken);

    private async Task ProcessGeneratedAtomicCoreAsync<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TSaga, TState, TEvent>(
        IMessageContext context, TEvent evt, bool manualTimeout, CancellationToken cancellationToken)
        where TSaga : SagaBase<TState>
        where TState : SagaState, new()
        where TEvent : ISagaEvent
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(evt);
        if (evt.GetType() != typeof(TEvent)
            || SagaRegistry.GetSagaTypeForEvent(typeof(TEvent)) != typeof(TSaga))
        {
            throw new InvalidOperationException("Typed atomic dispatch requires the exact registered runtime event and saga types.");
        }

        var info = SagaRegistry.GetSagaInfo(typeof(TSaga))
            ?? throw new InvalidOperationException("The typed saga has no registered routing metadata.");
        var policy = serviceProvider.GetRequiredService<SagaGeneratedPreparationPolicy>().Policy;
        await _concurrencyGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var previous = _atomicAttemptScope.Value;
        try
        {
            _atomicAttemptScope.Value = new AtomicAttemptScope { ManualTimeout = manualTimeout };
            await RunWithTimeoutAndRetryAsync(token => HandleAtomicAttemptAsync<TSaga, TState>(
                policy, context, evt, info,
                state => ActivatorUtilities.CreateInstance<TSaga>(serviceProvider, state),
                (saga, handlerToken) => InvokeGeneratedAtomicHandlerAsync<TSaga, TState, TEvent>(saga, evt, handlerToken),
                saga => saga is ISagaTimeout<TEvent>, token), typeof(TEvent).Name, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _atomicAttemptScope.Value = previous;
            _ = _concurrencyGate.Release();
        }
    }

    private static async Task<SagaEventOutcome> InvokeGeneratedAtomicHandlerAsync<TSaga, TState, TEvent>(
        TSaga saga, TEvent evt, CancellationToken cancellationToken)
        where TSaga : SagaBase<TState>
        where TState : SagaState
        where TEvent : ISagaEvent
    {
        if (saga is ISagaTimeout<TEvent> timeout)
        {
            await timeout.HandleTimeoutAsync(evt, cancellationToken).ConfigureAwait(false);
            return SagaEventOutcome.Handled;
        }

        return saga.HandlesEvent(evt)
            ? await saga.HandleAsync(evt, cancellationToken).ConfigureAwait(false)
            : SagaEventOutcome.Declined;
    }
}
