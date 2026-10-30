// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using Excalibur.Dispatch;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.EventSourcing.Projections;

internal sealed class TransactionalProjectionCoordinator(IProjectionRegistry registry, IServiceScopeFactory scopes)
    : ITransactionalProjectionCoordinator
{
    public bool Handles(string aggregateType) => Registrations(aggregateType).Length != 0;

    public async ValueTask<AppendResult> AppendAsync(IEventStore eventStore, string aggregateId, string aggregateType,
        IReadOnlyList<IDomainEvent> events, long expectedVersion, Func<StoredEvent, IDomainEvent> deserialize,
        Func<IDbTransaction, CancellationToken, ValueTask>? stageOutbox, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var capability = eventStore.GetService(typeof(ITransactionalProjectionEventStore)) as ITransactionalProjectionEventStore
            ?? throw new InvalidOperationException("TransactionalInline requires an event store and decorator chain supporting transactional projections.");
        var registrations = Registrations(aggregateType);
        foreach (var registration in registrations) { registration.ValidateTransactionalStore!(services); }

        return await capability.AppendWithProjectionsAsync(aggregateId, aggregateType, events, expectedVersion,
            async (transaction, token) =>
            {
                var replay = new List<IDomainEvent>(transaction.History.Count + events.Count);
                long next = 0;
                foreach (var stored in transaction.History)
                {
                    if (stored.Version != next++ || ErasedEventMarker.IsErased(stored.EventType))
                    {
                        throw new InvalidOperationException("TransactionalInline requires complete, unerased aggregate history.");
                    }

                    replay.Add(deserialize(stored));
                }

                if (next != expectedVersion + 1)
                {
                    throw new InvalidOperationException("TransactionalInline history does not reach the expected stream version.");
                }

                replay.AddRange(events);
                foreach (var registration in registrations)
                {
                    await registration.TransactionalApply!(transaction, replay, aggregateId, services, token).ConfigureAwait(false);
                }

                if (stageOutbox is not null) { await stageOutbox(transaction.Transaction, token).ConfigureAwait(false); }
            }, cancellationToken).ConfigureAwait(false);
    }

    private ProjectionRegistration[] Registrations(string aggregateType) => registry.GetByMode(ProjectionMode.TransactionalInline)
        .Where(r => string.Equals(r.TransactionalAggregateType, aggregateType, StringComparison.Ordinal))
        .OrderBy(r => r.ProjectionType.FullName, StringComparer.Ordinal).ToArray();
}
