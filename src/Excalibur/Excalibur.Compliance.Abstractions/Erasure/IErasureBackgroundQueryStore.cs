// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Compliance;

/// <summary>Estate-wide discovery for trusted erasure background workers.</summary>
/// <remarks>
/// Exposed through <see cref="IErasureStore.GetService"/> as a separate capability. These queries
/// intentionally ignore ambient tenancy; callers must authorize trusted-worker access themselves.
/// This interface is not an authorization boundary. Ordinary consumer listing remains tenant scoped.
/// Custom stores supporting background completion must expose this capability; absence is refused
/// rather than falling back to consumer listing.
/// <para>
/// <b>Each member names ONE background worker's discovery predicate</b> and returns only rows in that
/// status. That is why there is a member per predicate rather than one member taking a status: a
/// status parameter would promise every provider an estate-wide, tenancy-ignoring read of ANY status,
/// which widens the reach of the very thing the paragraph above warns about, and takes away each
/// provider's freedom to index the predicates it actually serves.
/// </para>
/// <para>
/// <b>WHERE THE CALLER'S ACTION REMOVES A ROW FROM THE PREDICATE IT WAS DISCOVERED BY, THE CALLER
/// COMPLETES DISCOVERY BEFORE IT ACTS.</b> These predicates are SELF-REMOVING: a worker that acts on a
/// row changes the status the query filters on, so the row leaves the result set the query is paging
/// through. Acting on every row of a page then shifts the remainder back by a page's worth and the next
/// page SKIPS that many rows, which are not lost but are deferred to a later poll -- so a steady
/// population takes an unbounded number of polls to drain, and the degradation is in liveness and is
/// silent. Read every page first, then act.
/// </para>
/// <para>
/// <b>A row carries no legal hold.</b> A worker that needs the hold re-reads the hold store under the
/// row's own recorded tenant, which it must do anyway because the hold may have changed since the page
/// was read -- so a value carried on the row would be stale by construction at the moment it mattered.
/// </para>
/// <para>
/// <b>Cancellation and provisioning differ between the reference store and a store over external
/// infrastructure, and a caller treats both as possible.</b> The in-memory reference store signals
/// cancellation eagerly, before any work; a provider signals it at its first I/O, so against a warm
/// connection pool a pre-cancelled token may yield a page where the reference store would have thrown.
/// A provider also initialises its schema on first use and may therefore signal provisioning and
/// provider failures the reference store cannot raise at all.
/// </para>
/// <para>
/// <b>A page is neither a snapshot nor a claim</b>, and nothing here establishes ambient tenancy. A
/// member that reads ambient tenancy is not a behavioural subtype of this contract, and its failure
/// mode is the WORST kind: under multi-tenancy the ambient term matches nothing, so discovery returns
/// an empty set and the work is silently never done, rather than failing.
/// </para>
/// </remarks>
public interface IErasureBackgroundQueryStore
{
	/// <summary>Discovers requests awaiting provider-confirmed key destruction across all tenants.</summary>
	/// <param name="pageNumber">The 1-based page number, at least one.</param>
	/// <param name="pageSize">The page size, from one through 1000.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Only requests in <see cref="ErasureRequestStatus.AwaitingKeyDestruction"/>.</returns>
	/// <remarks>
	/// <para>
	/// Returns full stored ownership, including legacy null ownership for the untenanted partition.
	/// Results use ascending requested time and a unique request-ID tie-breaker, stable within each
	/// provider for an unchanged set. Paging is not a snapshot or a claim; workers revalidate each row
	/// under its recorded tenant, and subsequent polls revisit survivors of concurrent changes.
	/// </para>
	/// <para>
	/// <b>A request held by a LIVE claim is excluded.</b> This status is the only one a confirmation pass
	/// takes custody from, so a live claim means another confirmer already holds the request. Offering it
	/// anyway makes the second confirmer take a claim it cannot get, be refused at its first write, and
	/// report a defect on every poll for as long as the first one holds — a defect report that says
	/// nothing is wrong, which is worse than silence because somebody will act on it.
	/// </para>
	/// <para>
	/// A request whose claim has LAPSED is included, which is what lets a confirmation interrupted by a
	/// host shutdown be picked up again. The lapse is decided against a clock the STORE reads.
	/// </para>
	/// </remarks>
	Task<IReadOnlyList<ErasureStatus>> GetAwaitingKeyDestructionRequestsAsync(
		int pageNumber, int pageSize, CancellationToken cancellationToken);

	/// <summary>Discovers requests a legal hold has deferred, across all tenants.</summary>
	/// <remarks>
	/// <para>
	/// A deferred request did no irreversible work, so returning it to an executable state re-exposes
	/// nothing: both writes of the deferred status happen after the execution claim and before any
	/// destructive act, and the claim is released rather than held.
	/// </para>
	/// <para>
	/// Returns full stored ownership, including legacy null ownership for the untenanted partition.
	/// Results use ascending requested time and a unique request-ID tie-breaker, stable within each
	/// provider for an unchanged set. Paging is not a snapshot or a claim; workers revalidate each row
	/// under its recorded tenant, and subsequent polls revisit survivors of concurrent changes.
	/// </para>
	/// <para>
	/// This member cannot find a request refused AT FILING TIME. That refusal returns a blocked result
	/// without persisting a row, so the caller holds a result whose request ID has no stored request.
	/// Nothing was accepted, and nothing here can discharge it.
	/// </para>
	/// </remarks>
	/// <param name="pageNumber">The 1-based page number, at least one.</param>
	/// <param name="pageSize">The page size, from one through 1000.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Only requests in <see cref="ErasureRequestStatus.BlockedByLegalHold"/>.</returns>
	Task<IReadOnlyList<ErasureStatus>> GetHoldDeferredRequestsAsync(
		int pageNumber, int pageSize, CancellationToken cancellationToken);
}
