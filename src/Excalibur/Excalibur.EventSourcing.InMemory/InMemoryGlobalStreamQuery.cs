// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data;
using Excalibur.EventSourcing.Queries;

namespace Excalibur.EventSourcing.InMemory;

/// <summary>
/// In-memory implementation of <see cref="IGlobalStreamQuery"/> that reads events from the in-memory
/// event store in global order.
/// </summary>
/// <remarks>
/// <para>
/// Registered automatically by <c>UseInMemory()</c>. Reads from the same store instance the event store
/// contract resolves to, not a second copy: two instances would each carry their own position counter and
/// their own events, and a projection would silently read an empty or stale stream.
/// </para>
/// <para>
/// This exists so that projections, materialized views and projection rebuilds can be exercised without
/// standing up a database -- in tests, in samples, and in single-process hosts. Its ordering guarantee is
/// the same one the persistent providers give: positions are allocated as a contiguous block under the
/// store's lock, so the committed stream is always a contiguous prefix and a reader may treat the highest
/// position it has seen as a high-water mark.
/// </para>
/// </remarks>
[NoTenantTerm(
	TenantConfinement.EstateWide,
	"The global stream is cross-aggregate and cross-tenant by definition: it is the substrate that " +
	"cross-tenant projections, materialized views, projection rebuilds and the lag read-model are built " +
	"from. Its result carries each event's own tenant on the row, so a consumer that needs confinement " +
	"filters there. Adding a tenant predicate here would not harden the read -- it would silently narrow " +
	"the stream to one partition and make every projection built on it incomplete.")]
internal sealed class InMemoryGlobalStreamQuery : IGlobalStreamQuery
{
	private readonly InMemoryEventStore _store;

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryGlobalStreamQuery"/> class.
	/// </summary>
	/// <param name="store">
	/// The store to read from. This must be the same singleton the <c>IEventStore</c> contract resolves
	/// to; a separately constructed store would hold neither the events nor the position counter.
	/// </param>
	internal InMemoryGlobalStreamQuery(InMemoryEventStore store)
	{
		ArgumentNullException.ThrowIfNull(store);

		_store = store;
	}

	/// <inheritdoc />
	public ValueTask<IReadOnlyList<StoredEvent>> ReadAllAsync(
		GlobalStreamPosition position,
		int maxCount,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(position);
		cancellationToken.ThrowIfCancellationRequested();

		return new ValueTask<IReadOnlyList<StoredEvent>>(
			_store.ReadGlobalStream(position.Position, maxCount, eventType: null));
	}

	/// <inheritdoc />
	public ValueTask<IReadOnlyList<StoredEvent>> ReadByEventTypeAsync(
		string eventType,
		GlobalStreamPosition position,
		int maxCount,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrEmpty(eventType);
		ArgumentNullException.ThrowIfNull(position);
		cancellationToken.ThrowIfCancellationRequested();

		return new ValueTask<IReadOnlyList<StoredEvent>>(
			_store.ReadGlobalStream(position.Position, maxCount, eventType));
	}

	/// <inheritdoc />
	public ValueTask<long> GetHeadPositionAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		return new ValueTask<long>(_store.GetHeadPosition());
	}
}
