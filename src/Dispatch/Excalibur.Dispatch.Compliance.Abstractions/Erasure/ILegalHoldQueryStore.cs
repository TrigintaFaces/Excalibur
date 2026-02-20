// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

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
/// </remarks>
public interface ILegalHoldQueryStore
{
	/// <summary>
	/// Gets all active holds for a data subject.
	/// </summary>
	/// <param name="dataSubjectIdHash">The hashed data subject identifier.</param>
	/// <param name="tenantId">Optional tenant ID.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>List of active holds.</returns>
	Task<IReadOnlyList<LegalHold>> GetActiveHoldsForDataSubjectAsync(
		string dataSubjectIdHash,
		string? tenantId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets all active holds for a tenant (tenant-wide holds).
	/// </summary>
	/// <param name="tenantId">The tenant ID.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>List of active tenant-wide holds.</returns>
	Task<IReadOnlyList<LegalHold>> GetActiveHoldsForTenantAsync(
		string tenantId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Lists all active holds.
	/// </summary>
	/// <param name="tenantId">Optional tenant filter.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>List of active holds.</returns>
	Task<IReadOnlyList<LegalHold>> ListActiveHoldsAsync(
		string? tenantId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Lists all holds (active and released).
	/// </summary>
	/// <param name="tenantId">Optional tenant filter.</param>
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
