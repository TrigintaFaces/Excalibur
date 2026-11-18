// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.Compliance;

/// <summary>
/// Query operations for erasure requests.
/// </summary>
/// <remarks>
/// <para>
/// This sub-interface of <see cref="IErasureStore"/> isolates query/listing
/// concerns per the Interface Segregation Principle (ISP).
/// </para>
/// <para>
/// Consumers that only need to query erasure requests can depend on this
/// interface directly. Implementations that also implement <see cref="IErasureStore"/>
/// expose this interface via <c>GetService(typeof(IErasureQueryStore))</c>.
/// </para>
/// </remarks>
public interface IErasureQueryStore
{
	/// <summary>
	/// Gets requests ready for execution: those past their grace period, AND those whose execution claim
	/// has lapsed.
	/// </summary>
	/// <param name="maxResults">Maximum number of results.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>List of requests ready to execute.</returns>
	/// <remarks>
	/// <para>
	/// <b>The second half of that is what makes an interrupted erasure come back.</b> A request whose
	/// executor died stays in the claimed status with nothing to advance it, and this query is the only
	/// thing that offers work — so a predicate naming the scheduled status alone excludes it forever,
	/// correctly and silently. The subject's data is then never erased, nothing throws, nothing logs, and
	/// no operator has a row to look at.
	/// </para>
	/// <para>
	/// A request is returned when its claim has LAPSED, never while a claim is live, so a caller may
	/// attempt it without first asking who holds it. The claim itself is still the authority: two callers
	/// handed the same lapsed request will both try, and exactly one is granted.
	/// </para>
	/// <para>
	/// <b>The lapse is decided against a clock the STORE reads</b>, not one a caller supplies. A lapse
	/// decided against a caller's instant is a comparison between two machines' clocks, which is the one
	/// comparison that may never decide anything.
	/// </para>
	/// </remarks>
	Task<IReadOnlyList<ErasureStatus>> GetScheduledRequestsAsync(
		int maxResults,
		CancellationToken cancellationToken);

	/// <summary>
	/// Lists erasure requests matching criteria with pagination.
	/// </summary>
	/// <param name="status">Optional status filter; <see langword="null"/> applies no status filter.</param>
	/// <param name="tenantId">
	/// The owning tenant to narrow the result to, or <see langword="null"/> or empty to apply no tenant
	/// filter. A non-empty value is compared ordinally against each request's recorded owner.
	/// </param>
	/// <param name="fromDate">Optional inclusive lower bound on <c>RequestedAt</c>.</param>
	/// <param name="toDate">Optional inclusive upper bound on <c>RequestedAt</c>.</param>
	/// <param name="pageNumber">The 1-based page number (minimum 1).</param>
	/// <param name="pageSize">The number of items per page (minimum 1, maximum 1000).</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>List of matching requests for the requested page.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="pageNumber"/> is less than 1, or <paramref name="pageSize"/> is less than 1 or
	/// greater than 1000.
	/// </exception>
	/// <remarks>
	/// <para>
	/// <b>Effects.</b> Returns one page of the requests visible to the ambient tenant scope, ordered by
	/// <c>RequestedAt</c> descending, where the page window is taken over the <em>filtered and ordered</em>
	/// set — never over an unfiltered page that is then filtered, which silently loses rows once the
	/// matching set is larger than <paramref name="pageSize"/>. Nothing is modified.
	/// </para>
	/// <para>
	/// <b>Which tenant fact <paramref name="tenantId"/> means.</b> It is matched against
	/// <see cref="ErasureStatus.TenantId"/> — the owner the store itself resolved when the request was
	/// saved — and never against <see cref="ErasureStatus.TenantPartition"/>. The two answer different
	/// questions and an implementation that filters on the partition instead will appear to work on a
	/// deployment where they coincide. The owner is the term the per-subject key handle is composed from,
	/// so it is the one that decides which key an erasure destroys.
	/// </para>
	/// <para>
	/// <b>The comparison is ordinal.</b> No case folding, no trimming, no prefix or pattern matching, and
	/// no canonicalisation of a missing owner onto a reserved sentinel. Two identifiers differing only in
	/// case are two tenants, and an implementation that folds them hands one tenant's erasure requests to
	/// another. Where the comparison is delegated to a database, the column holding the owner needs a
	/// binary or otherwise deterministic collation, or the engine folds the distinction beneath
	/// correct-looking code. A whitespace-only argument is a filter term rather than an absent one, and it
	/// matches nothing: a recorded owner is never empty or whitespace.
	/// </para>
	/// <para>
	/// <b>Invariant — a supplied filter can only narrow.</b> The result for any
	/// <paramref name="tenantId"/> is a subset of the result for <see langword="null"/>, and no argument
	/// can admit a request the ambient tenant scope excludes. A caller naming another tenant receives the
	/// empty set rather than that tenant's requests; a caller naming none still receives only their own
	/// ambient scope. Widening is not an expressible outcome of this parameter.
	/// </para>
	/// <para>
	/// Under multi-tenancy with no ambient tenant resolved, implementations fail closed — they raise
	/// rather than reach an unfiltered read.
	/// </para>
	/// </remarks>
	Task<IReadOnlyList<ErasureStatus>> ListRequestsAsync(
		ErasureRequestStatus? status,
		string? tenantId,
		DateTimeOffset? fromDate,
		DateTimeOffset? toDate,
		int pageNumber,
		int pageSize,
		CancellationToken cancellationToken);
}
