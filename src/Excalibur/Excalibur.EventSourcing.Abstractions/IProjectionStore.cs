// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.EventSourcing.Abstractions;

/// <summary>
/// Defines the contract for projection storage operations.
/// </summary>
/// <remarks>
/// <para>
/// Projections are read-optimized views of event-sourced data. This interface
/// provides CRUD operations plus dictionary-based querying that can be translated
/// to any backend (SQL, NoSQL, Search engines).
/// </para>
/// <para>
/// Uses dictionary-based filters instead of Expression trees per Dapper constraint.
/// Filter keys support operator suffixes for comparison operations:
/// <list type="bullet">
/// <item><c>["Status"] = "Active"</c> - Equality (default)</item>
/// <item><c>["Amount:gt"] = 100</c> - Greater than</item>
/// <item><c>["Tags:in"] = new[] { "A", "B" }</c> - In collection</item>
/// </list>
/// </para>
/// </remarks>
/// <typeparam name="TProjection">The projection type. Must be a reference type.</typeparam>
public interface IProjectionStore<TProjection>
	where TProjection : class
{
	/// <summary>
	/// Gets a projection by its unique identifier.
	/// </summary>
	/// <param name="id">The projection identifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The projection if found; otherwise, <c>null</c>.</returns>
	Task<TProjection?> GetByIdAsync(
		string id,
		CancellationToken cancellationToken);

	/// <summary>
	/// Creates or updates a projection.
	/// </summary>
	/// <param name="id">The projection identifier.</param>
	/// <param name="projection">The projection to store.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	Task UpsertAsync(
		string id,
		TProjection projection,
		CancellationToken cancellationToken);

	/// <summary>
	/// Deletes a projection by its identifier.
	/// </summary>
	/// <param name="id">The projection identifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <remarks>
	/// This operation is idempotent - deleting a non-existent projection succeeds silently.
	/// </remarks>
	Task DeleteAsync(
		string id,
		CancellationToken cancellationToken);

	/// <summary>
	/// Queries projections using dictionary-based filters.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Filters use property names as keys with optional operator suffixes:
	/// <list type="bullet">
	/// <item><c>["Status"] = "Active"</c> - Equality (default)</item>
	/// <item><c>["Amount:gt"] = 100</c> - Greater than</item>
	/// <item><c>["Amount:gte"] = 100</c> - Greater than or equal</item>
	/// <item><c>["Amount:lt"] = 1000</c> - Less than</item>
	/// <item><c>["Amount:lte"] = 1000</c> - Less than or equal</item>
	/// <item><c>["Status:neq"] = "Deleted"</c> - Not equals</item>
	/// <item><c>["Tags:in"] = new[] { "A", "B" }</c> - In collection</item>
	/// <item><c>["Name:contains"] = "test"</c> - String contains</item>
	/// </list>
	/// </para>
	/// <para>
	/// Multiple filters are combined with AND logic. Providers translate these filters
	/// to their native query syntax (SQL WHERE, MongoDB filters, CosmosDb queries, etc.).
	/// </para>
	/// </remarks>
	/// <param name="filters">Dictionary-based filter conditions. Pass <c>null</c> for no filtering.</param>
	/// <param name="options">Query options for pagination and sorting. Pass <c>null</c> for defaults.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The matching projections.</returns>
	Task<IReadOnlyList<TProjection>> QueryAsync(
		IDictionary<string, object>? filters,
		QueryOptions? options,
		CancellationToken cancellationToken);

	/// <summary>
	/// Counts projections matching the specified filters.
	/// </summary>
	/// <param name="filters">Dictionary-based filter conditions. Pass <c>null</c> for total count.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The count of matching projections.</returns>
	Task<long> CountAsync(
		IDictionary<string, object>? filters,
		CancellationToken cancellationToken);
}
