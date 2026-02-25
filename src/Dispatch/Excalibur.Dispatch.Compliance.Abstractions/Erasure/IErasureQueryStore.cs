// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Compliance;

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
	/// Gets requests ready for execution (past grace period).
	/// </summary>
	/// <param name="maxResults">Maximum number of results.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>List of requests ready to execute.</returns>
	Task<IReadOnlyList<ErasureStatus>> GetScheduledRequestsAsync(
		int maxResults,
		CancellationToken cancellationToken);

	/// <summary>
	/// Lists erasure requests matching criteria with pagination.
	/// </summary>
	/// <param name="status">Optional status filter.</param>
	/// <param name="tenantId">Optional tenant filter.</param>
	/// <param name="fromDate">Optional start date.</param>
	/// <param name="toDate">Optional end date.</param>
	/// <param name="pageNumber">The 1-based page number (minimum 1).</param>
	/// <param name="pageSize">The number of items per page (minimum 1, maximum 1000).</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>List of matching requests for the requested page.</returns>
	Task<IReadOnlyList<ErasureStatus>> ListRequestsAsync(
		ErasureRequestStatus? status,
		string? tenantId,
		DateTimeOffset? fromDate,
		DateTimeOffset? toDate,
		int pageNumber,
		int pageSize,
		CancellationToken cancellationToken);
}
