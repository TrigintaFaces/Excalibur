// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace Excalibur.EventSourcing;

/// <summary>Optional store-owned atomic append with aggregate-local projections.</summary>
/// <remarks>
/// A confirmed success includes both the append and all callback writes. A pre-commit callback fault
/// rolls back the transaction. An uncertain commit must not be reported as a proven rollback.
/// An old append's event identities alone do not establish that its projection workset ran.
/// </remarks>
public interface ITransactionalProjectionEventStore
{
    /// <summary>Appends events and projection changes in one transaction.</summary>
    /// <param name="aggregateId">Stream identifier.</param>
    /// <param name="aggregateType">Stream type.</param>
    /// <param name="events">New events.</param>
    /// <param name="expectedVersion">Expected last persisted version, or -1 for a new stream.</param>
    /// <param name="apply">Callback before append, using authoritative history and the supplied transaction.</param>
    /// <param name="cancellationToken">Cancellation token. Cancellation does not prove rollback after commit dispatch.</param>
    /// <returns>The confirmed or uncertain append outcome.</returns>
    ValueTask<AppendResult> AppendWithProjectionsAsync(string aggregateId, string aggregateType,
        IReadOnlyList<IDomainEvent> events, long expectedVersion,
        Func<ProjectionTransaction, CancellationToken, ValueTask> apply, CancellationToken cancellationToken);
}
