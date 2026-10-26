// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.IdentityMap;
using Excalibur.Domain.Model;

namespace Excalibur.EventSourcing;

/// <summary>
/// Resolves externally-identified aggregates through an <see cref="IIdentityMapStore"/> without exposing
/// the window between reserving an identity and writing the event stream.
/// </summary>
public static class IdentityMappedAggregateExtensions
{
	/// <summary>
	/// Resolves an external identifier to its aggregate, creating the aggregate if no event stream exists
	/// for it yet, applies the caller's change, and saves — in one operation.
	/// </summary>
	/// <typeparam name="TAggregate">The aggregate type.</typeparam>
	/// <typeparam name="TKey">The aggregate ID type.</typeparam>
	/// <param name="store">The identity map store.</param>
	/// <param name="repository">The repository that owns the aggregate's event stream.</param>
	/// <param name="externalSystem">The external system name.</param>
	/// <param name="externalId">The external identifier value.</param>
	/// <param name="aggregateType">The aggregate type name used in the mapping.</param>
	/// <param name="candidateId">
	/// The ID to reserve if this external identifier has no mapping yet. Ignored when a mapping already
	/// exists, so it is safe — and expected — to pass a freshly generated ID on every call.
	/// </param>
	/// <param name="create">
	/// Produces the aggregate when no event stream exists for the resolved ID. Receives the resolved ID,
	/// which is the reserved one whether this call created the mapping or found an existing one.
	/// </param>
	/// <param name="update">Applies the change when an event stream already exists.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The result of saving the aggregate.</returns>
	/// <remarks>
	/// <para>
	/// <b>Prefer this over branching on <see cref="IdentityBindResult.MappingWasCreated"/> yourself.</b> A binding
	/// and an event stream are two writes to two stores with nothing atomic between them, so
	/// <see cref="IdentityBindResult.MappingWasCreated"/> answers "did I win the race to reserve this ID" and
	/// never "does a stream exist". Code that treats it as the insert-versus-update discriminator is wrong
	/// in one direction and permanently: if anything interrupts between the bind and the append —
	/// cancellation at a deadline, a crash, an eviction — the mapping is committed, the stream never
	/// arrives, and from then on every call takes the already-exists branch, finds nothing, and either
	/// throws or silently does nothing for that external identifier forever. Nothing downstream learns
	/// of it.
	/// </para>
	/// <para>
	/// This method decides by asking the store that owns the answer, so a torn state heals itself: a
	/// reservation whose append never landed is simply created on the next call. There is no compensating
	/// delete to get right, and there could not be — an unbind in a failure path only runs if the process
	/// survives to run it, which is precisely the case that produced the tear, and it races any concurrent
	/// writer that has already resolved the same mapping.
	/// </para>
	/// <para>
	/// The two-callback shape is
	/// <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey, TValue}"/>'s
	/// <c>AddOrUpdate</c>: the caller supplies what to do in each case and the operation owns the decision
	/// between them, so there is no flag to misread.
	/// </para>
	/// <para>
	/// <b>At-least-once, so <paramref name="update"/> must be idempotent on the event identifier.</b> This
	/// is one operation to its caller, not one transaction: a redelivered change replays
	/// <paramref name="update"/>, and a save that commits but whose acknowledgement is lost is retried.
	/// Concurrent callers for the same external identifier resolve the same ID and contend on the stream's
	/// optimistic concurrency, which surfaces as the repository's usual concurrency failure.
	/// </para>
	/// </remarks>
	public static async Task<SaveResult> CreateOrUpdateAsync<TAggregate, TKey>(
		this IIdentityMapStore store,
		IEventSourcedRepository<TAggregate, TKey> repository,
		string externalSystem,
		string externalId,
		string aggregateType,
		TKey candidateId,
		Func<TKey, TAggregate> create,
		Action<TAggregate> update,
		CancellationToken cancellationToken)
		where TAggregate : class, IAggregateRoot<TKey>, IAggregateSnapshotSupport
		where TKey : struct, IParsable<TKey>
	{
		ArgumentNullException.ThrowIfNull(store);
		ArgumentNullException.ThrowIfNull(repository);
		ArgumentNullException.ThrowIfNull(create);
		ArgumentNullException.ThrowIfNull(update);

		var bind = await store
			.TryBindAsync(externalSystem, externalId, aggregateType, candidateId.ToString()!, cancellationToken)
			.ConfigureAwait(false);

		var aggregateId = TKey.Parse(bind.AggregateId, null);

		// THE STREAM DECIDES, never bind.MappingWasCreated -- see the remarks. A reservation whose append never
		// landed reaches this line with a committed mapping and no aggregate, and is created here.
		var existing = await repository.GetByIdAsync(aggregateId, cancellationToken).ConfigureAwait(false);
		if (existing is null)
		{
			return await repository.SaveAsync(create(aggregateId), cancellationToken).ConfigureAwait(false);
		}

		update(existing);
		return await repository.SaveAsync(existing, cancellationToken).ConfigureAwait(false);
	}
}
