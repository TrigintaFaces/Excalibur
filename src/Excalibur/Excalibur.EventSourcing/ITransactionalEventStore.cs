// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Excalibur.EventSourcing.Abstractions;

namespace Excalibur.EventSourcing;

/// <summary>
/// Optional extension of <see cref="IEventStore"/> that exposes transaction context for
/// atomic coordination between event store appends and outbox staging.
/// </summary>
/// <remarks>
/// <para>
/// Event store providers that support database transactions (e.g., SQL Server, Postgres)
/// should implement this interface to enable transactional outbox staging in
/// <see cref="Implementation.EventSourcedRepository{TAggregate,TKey}"/>.
/// </para>
/// <para>
/// When the injected <see cref="IEventStore"/> also implements this interface,
/// the repository uses the transaction to atomically append events and stage outbox messages
/// in a single database transaction. When this interface is not implemented, the repository
/// falls back to the current behavior (events are appended, outbox staging is handled by
/// the background service).
/// </para>
/// </remarks>
internal interface ITransactionalEventStore : IEventStore
{
	/// <summary>
	/// Begins a database transaction for atomic event store and outbox operations.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// A database transaction that can be passed to both <c>AppendAsync</c> and
	/// <see cref="Outbox.IEventSourcedOutboxStore.AddAsync"/> for atomic coordination,
	/// or <see langword="null"/> if the provider does not support explicit transactions.
	/// </returns>
	Task<IDbTransaction?> BeginTransactionAsync(CancellationToken cancellationToken);
}
