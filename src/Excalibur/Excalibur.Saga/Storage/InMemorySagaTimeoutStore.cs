// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Collections.Concurrent;

using Excalibur.Dispatch;
using Excalibur.Saga.Abstractions;

namespace Excalibur.Saga.Storage;

/// <summary>
/// In-memory implementation of <see cref="ISagaTimeoutStore"/> for testing and development.
/// </summary>
/// <remarks>
/// <para>
/// This implementation uses a <see cref="ConcurrentDictionary{TKey, TValue}"/> for thread-safe
/// storage with an additional lock for consistent reads and atomic claims in
/// <see cref="ClaimDueTimeoutsAsync"/>.
/// </para>
/// <para>
/// <b>Warning:</b> Timeouts are lost on process restart. Use a persistent implementation
/// (e.g., SQL Server, Redis) for production deployments.
/// </para>
/// <para>
/// <b>Tenancy mirrors the relational stores exactly, including what deliberately stays unscoped.</b> The
/// owning tenant is stamped from the tenant context on schedule, and cancel / mark-delivered bind it, so a
/// cancel-by-saga-id cannot reach another tenant's pending timeouts. <see cref="ClaimDueTimeoutsAsync"/> and
/// <see cref="GetDueTimeoutsAsync"/> stay <b>estate-wide</b>: the delivery service runs with no tenant
/// established and re-establishes each row's tenant from the row before dispatching. Scoping the claim path
/// would lease only the untenanted partition, leaving every tenant's timeouts due forever - a total stall
/// that presents as silence and that a safety-only isolation test still passes.
/// </para>
/// </remarks>
internal sealed class InMemorySagaTimeoutStore(ITenantContext tenantContext) : ISagaTimeoutStore
{
	private static readonly TimeSpan LeaseTimeout = TimeSpan.FromSeconds(120);

	private readonly ITenantContext _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));

	private readonly ConcurrentDictionary<string, SagaTimeout> _timeouts = new();
	// The claim records WHO holds it, not merely WHEN it was taken. A map to a bare timestamp can answer
	// "is this lease stale" and cannot answer "is this caller still the owner", so retirement had no way to
	// refuse a processor whose lease had already been taken over. The relational stores keep an owner
	// column for exactly this; this store mirrored their tenant term faithfully and their claim term not at
	// all, which made it the one implementation where the ownership arm could not be exercised.
	private readonly Dictionary<string, (string Owner, DateTimeOffset ClaimedAt)> _claims = new(StringComparer.Ordinal);
	private readonly Lock _dueLock = new();

	/// <summary>
	/// Resolves the tenant partition this store writes under, from its required tenant context.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Resolved through the context fold, which fails closed, and never through the fold that rehydrates a
	/// value read back from storage. Those two folds both take a tenant term and differ in what an absent one
	/// means, so feeding an ambient read into the storage fold turned <em>"no tenant context was established
	/// here"</em> into <em>"this row belongs to no tenant"</em>. Nothing in the type could tell them apart.
	/// </para>
	/// <para>
	/// The consequences that substitution had here were both silent. On a single-tenant host the ambient value
	/// is unset, so the timeout was stamped with the reserved untenanted sentinel while every other store in
	/// the framework stamps the default tenant identity — a partition nothing else addresses. On a
	/// multi-tenant host with no scope established it was the same sentinel, so the timeout fired against a
	/// partition the saga does not live in and the saga never completed. Resolving from the context instead
	/// yields the default tenant identity on a single-tenant host, the established tenant on a multi-tenant
	/// one, and refuses outright when a multi-tenant host has established none.
	/// </para>
	/// </remarks>
	/// <returns>The partition named by the current tenant context.</returns>
	private KeyedTenantPartition CurrentPartition() =>
		KeyedTenantPartition.FromContext(_tenantContext);

	/// <inheritdoc />
	public Task ScheduleTimeoutAsync(SagaTimeout timeout, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(timeout);

		// The owning tenant is stamped from the TENANT CONTEXT, never from timeout.TenantId: a scheduled timeout
		// must not be able to claim a tenant its caller never established. timeout.TenantId is an output-of-read
		// on this type and is deliberately ignored on the write, exactly as in the relational stores.
		var stamped = timeout with { TenantId = CurrentPartition().TenantId };

		lock (_dueLock)
		{
			_timeouts[stamped.TimeoutId] = stamped;
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task CancelTimeoutAsync(string sagaId, string timeoutId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sagaId);
		ArgumentException.ThrowIfNullOrWhiteSpace(timeoutId);

		// Binds (TenantId, SagaId, TimeoutId), matching the relational DELETE. Both identifiers are
		// caller-supplied - SagaId is parsed off an inbound event - so the tenant term is an authorization
		// control, not defence in depth: without it a colliding SagaId across tenants cancels the other
		// tenant's timeout. Cancellation stays idempotent: a miss is not an error.
		var partition = CurrentPartition();

		lock (_dueLock)
		{
			if (_timeouts.TryGetValue(timeoutId, out var existing)
				&& OwnedBy(existing, partition)
				&& string.Equals(existing.SagaId, sagaId, StringComparison.Ordinal))
			{
				_ = _timeouts.TryRemove(timeoutId, out _);
				_ = _claims.Remove(timeoutId);
			}
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task CancelAllTimeoutsAsync(string sagaId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sagaId);

		// Binds the current tenant alongside the caller-supplied SagaId, matching the relational
		// `WHERE TenantId = @TenantId AND SagaId = @SagaId`. This is the statement the SQL DDL comment names:
		// without the tenant term, a cancel-by-SagaId deletes another tenant's pending timeouts.
		// Lock for consistent read-then-remove (same as ClaimDueTimeoutsAsync)
		lock (_dueLock)
		{
			var partition = CurrentPartition();
			var keysToRemove = _timeouts
				.Where(kvp => OwnedBy(kvp.Value, partition)
					&& string.Equals(kvp.Value.SagaId, sagaId, StringComparison.Ordinal))
				.Select(kvp => kvp.Key)
				.ToList();

			foreach (var key in keysToRemove)
			{
				_ = _timeouts.TryRemove(key, out _);
				_ = _claims.Remove(key);
			}
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<ClaimedSagaTimeout>> ClaimDueTimeoutsAsync(DateTimeOffset asOf, int batchSize, CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);

		// DELIBERATELY ESTATE-WIDE - no tenant predicate. The delivery service leases with no tenant
		// established and re-establishes each row's tenant from its TenantId before dispatching. Scoping this
		// would lease only the untenanted partition and stall every tenant's timeouts silently.
		//
		// Atomically select+claim under a single lock: two concurrent callers can never observe
		// (and therefore never claim) the same due-and-unclaimed-or-stale timeout, because the
		// claim map is updated before the lock is released.
		lock (_dueLock)
		{
			var due = _timeouts.Values
				.Where(t => t.DueAt <= asOf
					&& (!_claims.TryGetValue(t.TimeoutId, out var existing) || existing.ClaimedAt + LeaseTimeout < asOf))
				.OrderBy(t => t.DueAt)
				.Take(batchSize)
				.ToList();

			var claimed = new List<ClaimedSagaTimeout>(due.Count);
			foreach (var timeout in due)
			{
				// A token per CLAIM, not per store or per process. Re-claiming the same timeout after a
				// lease expiry must produce a different token, because that difference is the only thing
				// that lets retirement tell a stale holder from the live one. An identifier that were
				// constant across claims would satisfy the ownership predicate for both and enforce
				// nothing. This is an ownership identity and not a secret, so a GUID is the right tool.
				var token = Guid.NewGuid().ToString("N");
				_claims[timeout.TimeoutId] = (token, asOf);
				claimed.Add(new ClaimedSagaTimeout(timeout, token));
			}

			return Task.FromResult<IReadOnlyList<ClaimedSagaTimeout>>(claimed);
		}
	}

	/// <inheritdoc />
	public Task<IReadOnlyList<SagaTimeout>> GetDueTimeoutsAsync(DateTimeOffset asOf, CancellationToken cancellationToken)
	{
		// DELIBERATELY ESTATE-WIDE, for the same reason as ClaimDueTimeoutsAsync.
		// Pure read snapshot: no claim/lease is recorded, unlike ClaimDueTimeoutsAsync.
		lock (_dueLock)
		{
			var due = _timeouts.Values
				.Where(t => t.DueAt <= asOf)
				.OrderBy(t => t.DueAt)
				.ToList();

			return Task.FromResult<IReadOnlyList<SagaTimeout>>(due);
		}
	}

	/// <inheritdoc />
	public Task<SagaTimeoutRetirementOutcome> MarkDeliveredAsync(ClaimedSagaTimeout claim, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(claim);

		var timeoutId = claim.Timeout.TimeoutId;

		// Binds (TenantId, TimeoutId), matching the relational DELETE. The delivery service re-establishes the
		// row's own tenant around the WHOLE of its delivery method precisely so this terminal mark matches -
		// a mark that selects nothing leaves the row pending and it redelivers forever.
		var partition = CurrentPartition();

		// The ownership test and the removal happen inside ONE lock, which is this store's equivalent of the
		// relational stores' single conditional DELETE. Reading the owner, releasing, and then removing would
		// leave a window in which the lease is taken over between the two, and the stale holder's removal
		// would still land.
		lock (_dueLock)
		{
			if (!_timeouts.TryGetValue(timeoutId, out var existing) || !OwnedBy(existing, partition))
			{
				return Task.FromResult(SagaTimeoutRetirementOutcome.Superseded);
			}

			// The row is only this caller's to retire while the claim it presents is the CURRENT one. A
			// processor that stalled past its lease finds its token replaced here and is refused, which is
			// what stops it destroying the live claimant's retry.
			if (!_claims.TryGetValue(timeoutId, out var held)
				|| !string.Equals(held.Owner, claim.ClaimToken, StringComparison.Ordinal))
			{
				return Task.FromResult(SagaTimeoutRetirementOutcome.Superseded);
			}

			_ = _timeouts.TryRemove(timeoutId, out _);
			_ = _claims.Remove(timeoutId);
		}

		return Task.FromResult(SagaTimeoutRetirementOutcome.Retired);
	}

	/// <summary>
	/// Determines whether a stored timeout belongs to the supplied partition.
	/// </summary>
	/// <remarks>
	/// Ordinal comparison, matching the relational stores' binary collation: SQL Server pins
	/// <c>Latin1_General_BIN2</c> on the tenant column precisely so tenant terms differing only in case are
	/// different tenants.
	/// </remarks>
	/// <param name="timeout">The stored timeout.</param>
	/// <param name="partition">The partition named by the current tenant context.</param>
	/// <returns><see langword="true"/> when the timeout belongs to <paramref name="partition"/>.</returns>
	private static bool OwnedBy(SagaTimeout timeout, KeyedTenantPartition partition) =>
		string.Equals(timeout.TenantId, partition.TenantId, StringComparison.Ordinal);

	/// <summary>
	/// Gets the count of pending timeouts. Used for testing.
	/// </summary>
	/// <returns>The number of pending timeouts.</returns>
	public int GetPendingCount()
	{
		lock (_dueLock)
		{
			return _timeouts.Count;
		}
	}

	/// <summary>
	/// Clears all pending timeouts. Used for testing cleanup.
	/// </summary>
	public void Clear()
	{
		lock (_dueLock)
		{
			_timeouts.Clear();

			// The claims go with the timeouts. Clearing only the rows left this map populated, so a later
			// timeout reusing a cleared identifier inherited the old claim and could not be retired by the
			// processor that actually claimed it.
			_claims.Clear();
		}
	}
}
