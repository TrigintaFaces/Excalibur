// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.Compliance;

/// <summary>
/// Query operations for legal holds.
/// </summary>
/// <remarks>
/// <para>
/// This sub-interface of <see cref="ILegalHoldStore"/> isolates query/listing
/// concerns per the Interface Segregation Principle (ISP).
/// </para>
/// <para>
/// Consumers that only need to query legal holds can depend on this
/// interface directly. Implementations that also implement <see cref="ILegalHoldStore"/>
/// expose this interface via <c>GetService(typeof(ILegalHoldQueryStore))</c>.
/// </para>
/// <para>
/// <b>IN FORCE IS THE CONJUNCTION OF TWO INDEPENDENT FACTS, and every member named for active holds
/// means in force.</b> <see cref="LegalHold.IsActive"/> answers whether the hold was RELEASED -- an act,
/// with an actor, a time and a reason, recorded alongside it. <see cref="LegalHold.ExpiresAt"/> answers
/// whether the stated period has LAPSED -- the passage of time, with no actor. Neither fact implies the
/// other, and <b>an implementation may not use one as a proxy for the other</b>: a hold is in force when
/// it is neither released NOR lapsed. A hold carrying no expiry has not lapsed and stays in force until
/// it is released, which is the indefinite hold and the commonest kind.
/// </para>
/// <para>
/// <b>A member's predicate follows from the question it asks</b>, so this is not a rule that every
/// statement carries an expiry term. The in-force members ask "not released AND not lapsed";
/// <see cref="GetExpiredHoldsAsync"/> asks the complementary question, "not released AND lapsed", and
/// adding an expiry term to it uniformly would ask for holds that have both lapsed and not lapsed.
/// </para>
/// <para>
/// <b>A lapsed hold must not be reported in force even when no expiration sweep is running.</b> The
/// sweep RELEASES lapsed holds and is registered separately from hold storage, so a deployment that
/// stores holds without it is a supported composition. A correctness property may not depend on an
/// optional background actor having run: reported in force forever, a lapsed hold refuses an erasure
/// that should proceed and leaves the obligation permanently undischarged with nothing recording why.
/// </para>
/// <para>
/// <b>In-force results are ordered by <see cref="LegalHold.CreatedAt"/> descending</b> -- newest first --
/// so a caller reading the first row reads the most recent order. The hold carries the basis, case
/// reference and description an operator acts on, so an arbitrary order is not a usable contract.
/// </para>
/// </remarks>
public interface ILegalHoldQueryStore
{
	/// <summary>
	/// Gets all active holds for a data subject.
	/// </summary>
	/// <param name="dataSubjectIdHash">The hashed data subject identifier.</param>
	/// <param name="tenantId">
	/// Optional tenant ID. Narrows the result to that tenant's holds plus every global hold; it never
	/// widens the result to another tenant's holds.
	/// </param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>List of active holds.</returns>
	/// <remarks>
	/// A hold that belongs to no tenant is a <em>global</em> preservation order and is returned to every
	/// caller, including one who names a tenant. A legal hold blocks erasure, so a global order that a
	/// scoped read omits is one that has silently stopped blocking, and the deletion it should have
	/// prevented is irreversible. The tenant argument still excludes every OTHER tenant's holds.
	/// </remarks>
	/// <param name="asOf">
	/// The instant the question is asked at. A hold is in force at <paramref name="asOf" /> when it was
	/// released at no point and has not lapsed by then. The caller supplies it so the answer is decidable
	/// and so several reads that must agree can be pinned to one instant.
	/// </param>
	Task<IReadOnlyList<LegalHold>> GetActiveHoldsForDataSubjectAsync(
		string dataSubjectIdHash,
		string? tenantId,
		DateTimeOffset asOf,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets all active holds for a tenant (tenant-wide holds).
	/// </summary>
	/// <param name="tenantId">The tenant ID.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>List of active tenant-wide holds.</returns>
	/// <remarks>
	/// A hold that belongs to no tenant is a <em>global</em> preservation order and is returned to every
	/// caller, including one who names a tenant. A legal hold blocks erasure, so a global order that a
	/// scoped read omits is one that has silently stopped blocking, and the deletion it should have
	/// prevented is irreversible. The tenant argument still excludes every OTHER tenant's holds.
	/// </remarks>
	/// <param name="asOf">
	/// The instant the question is asked at. A hold is in force at <paramref name="asOf" /> when it was
	/// released at no point and has not lapsed by then. The caller supplies it so the answer is decidable
	/// and so several reads that must agree can be pinned to one instant.
	/// </param>
	Task<IReadOnlyList<LegalHold>> GetActiveHoldsForTenantAsync(
		string tenantId,
		DateTimeOffset asOf,
		CancellationToken cancellationToken);

	/// <summary>
	/// Lists all active holds.
	/// </summary>
	/// <param name="tenantId">
	/// Optional tenant filter. Narrows the result to that tenant's holds plus every global hold.
	/// </param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>List of active holds.</returns>
	/// <remarks>
	/// A hold that belongs to no tenant is a <em>global</em> preservation order and is returned to every
	/// caller, including one who names a tenant. A legal hold blocks erasure, so a global order that a
	/// scoped read omits is one that has silently stopped blocking, and the deletion it should have
	/// prevented is irreversible. The tenant argument still excludes every OTHER tenant's holds.
	/// </remarks>
	/// <param name="asOf">
	/// The instant the question is asked at. A hold is in force at <paramref name="asOf" /> when it was
	/// released at no point and has not lapsed by then. The caller supplies it so the answer is decidable
	/// and so several reads that must agree can be pinned to one instant.
	/// </param>
	Task<IReadOnlyList<LegalHold>> ListActiveHoldsAsync(
		string? tenantId,
		DateTimeOffset asOf,
		CancellationToken cancellationToken);

	/// <summary>
	/// Lists all holds (active and released).
	/// </summary>
	/// <param name="tenantId">
	/// Optional tenant filter. Narrows the result to that tenant's holds plus every global hold.
	/// </param>
	/// <param name="fromDate">Optional start date filter.</param>
	/// <param name="toDate">Optional end date filter.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>List of all holds.</returns>
	Task<IReadOnlyList<LegalHold>> ListAllHoldsAsync(
		string? tenantId,
		DateTimeOffset? fromDate,
		DateTimeOffset? toDate,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets holds that have expired and should be auto-released.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>List of expired holds.</returns>
	Task<IReadOnlyList<LegalHold>> GetExpiredHoldsAsync(
		CancellationToken cancellationToken);
}
