// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using Excalibur.Dispatch;

namespace Excalibur.EventSourcing;

/// <summary>Coordinates registered transactional projections for repository saves.</summary>
public interface ITransactionalProjectionCoordinator
{
    /// <summary>Determines whether the aggregate has transactional registrations.</summary>
    /// <param name="aggregateType">Aggregate type name.</param>
    /// <returns>Whether a coordinated append is required.</returns>
    bool Handles(string aggregateType);

    /// <summary>Persists events, registered models, and optional outbox staging atomically.</summary>
    /// <param name="eventStore">The repository's decorated event store.</param>
    /// <param name="aggregateId">Aggregate identifier.</param>
    /// <param name="aggregateType">Aggregate type.</param>
    /// <param name="events">Pending events.</param>
    /// <param name="expectedVersion">Expected stream version.</param>
    /// <param name="deserialize">Repository's canonical deserialization and upcasting pipeline.</param>
    /// <param name="stageOutbox">Optional transaction-enlisted outbox staging.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The append outcome.</returns>
    ValueTask<AppendResult> AppendAsync(IEventStore eventStore, string aggregateId, string aggregateType,
        IReadOnlyList<IDomainEvent> events, long expectedVersion, Func<StoredEvent, IDomainEvent> deserialize,
        Func<IDbTransaction, CancellationToken, ValueTask>? stageOutbox, CancellationToken cancellationToken);
}
