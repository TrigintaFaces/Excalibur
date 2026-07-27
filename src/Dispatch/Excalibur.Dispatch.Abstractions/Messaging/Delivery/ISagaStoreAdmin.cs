// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Messaging;

/// <summary>
/// Administrative and query operations over a saga store: enumerate and filter saga instances, fetch a
/// single instance summary, and compute aggregate statistics.
/// </summary>
/// <remarks>
/// <para>
/// These operations back health checks, operational tooling, and the operations dashboard. They are NOT
/// needed for the normal saga load/save flow, so this is a separate interface following the same ISP
/// pattern as <see cref="IOutboxStoreAdmin"/> / <see cref="IInboxStoreAdmin"/>. Implementations register
/// this sub-interface in DI so consumers can resolve it independently.
/// </para>
/// <para>
/// The MVP providers are the in-memory, SQL Server, and PostgreSQL saga stores. A store that cannot
/// support these queries need not implement this interface; the dashboard fails open when it is absent.
/// </para>
/// </remarks>
public interface ISagaStoreAdmin
{
	/// <summary>
	/// Queries saga instance summaries matching the supplied filter, ordered by store-defined ordering,
	/// with pagination.
	/// </summary>
	/// <param name="filter">The filter criteria (completion state, tenant, pagination).</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>The matching saga instance summaries.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="filter"/> is null.</exception>
	ValueTask<IReadOnlyList<SagaInstanceSummary>> QuerySagasAsync(
		SagaQueryFilter filter,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the summary of a single saga instance by its identifier.
	/// </summary>
	/// <param name="sagaId">The unique identifier of the saga instance.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>The saga instance summary, or <see langword="null"/> when no such saga exists.</returns>
	ValueTask<SagaInstanceSummary?> GetSummaryAsync(Guid sagaId, CancellationToken cancellationToken);

	/// <summary>
	/// Gets aggregate statistics about the sagas in the store (running / completed / total counts).
	/// </summary>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>The saga store statistics.</returns>
	ValueTask<SagaStoreStatistics> GetStatisticsAsync(CancellationToken cancellationToken);
}
