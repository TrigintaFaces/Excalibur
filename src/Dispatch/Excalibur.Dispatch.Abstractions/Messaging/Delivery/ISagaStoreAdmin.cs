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
	/// Gets aggregate statistics about the calling tenant's sagas (running / completed / total counts).
	/// </summary>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>The saga store statistics for the ambient tenant partition.</returns>
	/// <remarks>
	/// <para>
	/// The counts are <b>tenant-scoped</b>: they cover the ambient partition and no other. A host that
	/// established no tenant operates on the untenanted partition, which is a real partition holding the rows
	/// that carry no tenant — not a wildcard, and not every tenant's rows. Estate-wide counts are a
	/// deliberately separate operation, <see cref="GetAllTenantsStatisticsAsync"/>.
	/// </para>
	/// </remarks>
	ValueTask<SagaStoreStatistics> GetStatisticsAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Gets aggregate statistics about the sagas of <b>every</b> tenant (running / completed / total counts).
	/// </summary>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>The saga store statistics across all tenant partitions.</returns>
	/// <remarks>
	/// <para>
	/// This is the operator diagnostic: no tenant discriminator is applied, so every tenant's sagas are counted.
	/// It is the only statistics read that runs without a resolved tenant, because it is the only one that does
	/// not need to name one.
	/// </para>
	/// <para>
	/// <b>The name is the safety control.</b> Estate-wide counts are reachable only by writing "AllTenants" at
	/// the call site, never by omitting a scope or by passing a value that happens to mean "everything" — the
	/// same discipline as <see cref="ISagaStore.PurgeAllTenantsCompletedBeforeAsync"/>. A caller cannot arrive
	/// here by forgetting something, and a reviewer reading the call site sees the intent without tracing where
	/// a scope came from.
	/// </para>
	/// <para>
	/// Aggregate counts disclose no saga identifiers, types, or tenant names — only totals — so this is a
	/// weaker disclosure than <see cref="QuerySagasAsync"/>, which stays tenant-scoped and has no estate-wide
	/// counterpart.
	/// </para>
	/// <para>
	/// Like the estate-wide purge this is an <b>optional capability</b> whose default implementation throws. A
	/// store that supports it overrides this method; <b>a decorator must override it to forward to its inner
	/// store</b> — a decorator that does not override inherits this throwing default, so a decorated store
	/// would report the capability as missing even though the store underneath supports it.
	/// </para>
	/// </remarks>
	/// <exception cref="NotSupportedException">Thrown by stores that do not support estate-wide statistics.</exception>
	ValueTask<SagaStoreStatistics> GetAllTenantsStatisticsAsync(CancellationToken cancellationToken) =>
		throw new NotSupportedException(
			$"This saga store does not support estate-wide statistics. Store type: '{GetType().FullName}'. " +
			"Use a store that implements GetAllTenantsStatisticsAsync (the in-memory store and the SqlServer, " +
			"Postgres, and Oracle providers), or call GetStatisticsAsync for the calling tenant's counts.");
}
