// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch;

using System.Diagnostics.CodeAnalysis;

namespace Excalibur.EventSourcing;

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
/// <para>
/// <b>Tenant confinement.</b> Every operation is confined to the ambient tenant established for this
/// store instance: <see cref="QueryAsync"/> and <see cref="CountAsync"/> return none of another
/// tenant's projections and every one of the caller's own that matches the filter, <see cref="GetByIdAsync"/>
/// reports a projection stored under another tenant as not found, <see cref="UpsertAsync"/> can
/// neither read nor overwrite another tenant's projection under the same <c>id</c>, and
/// <see cref="DeleteAsync"/> cannot remove another tenant's projection under the same <c>id</c>.
/// The confinement applies to writes and deletes as well as reads: scoping only the read paths turns
/// a disclosure into silent data loss rather than removing the problem. Which mechanism a
/// given provider uses to hold that boundary is declared by its capability marker —
/// <see cref="ITenantScopingCapability{TContract}"/> for a store that reads an ambient tenant — and the
/// package's own <c>ARCHITECTURE.md</c> states the falsifiable guarantee and how it is verified. A store
/// presenting no marker is not confined by the framework.
/// </para>
/// </remarks>
/// <typeparam name="TProjection">The projection type. Must be a reference type.</typeparam>
[TenantOwned]
public interface IProjectionStore<TProjection> : IServiceProvider
	where TProjection : class
{
	/// <summary>
	/// Resolves an optional projection-store capability, or <see langword="null"/> when it is unavailable.
	/// </summary>
	/// <param name="serviceType">
	/// The capability interface to resolve, for example <see cref="IPageableProjectionStore{TProjection}"/>.
	/// </param>
	/// <returns>
	/// An instance assignable to <paramref name="serviceType"/> when this store provides the capability;
	/// otherwise <see langword="null"/>.
	/// </returns>
	/// <remarks>
	/// <para>
	/// Resolve capabilities through this method rather than testing the store's type. A store is frequently
	/// reached through a decorator -- tenant scoping, encryption -- and a decorator's interface list is fixed
	/// when it is compiled, while the capabilities of the store it wraps are known only at run time. A type
	/// test therefore reports the decorator's own list and hides every capability the store beneath it
	/// provides.
	/// </para>
	/// <para>
	/// The default implementation answers for any capability this instance itself implements, so a store that
	/// implements a capability directly need not override it. Decorators override it to answer for the store
	/// they wrap.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="serviceType"/> is null. </exception>
	object? IServiceProvider.GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		return serviceType.IsInstanceOfType(this) ? this : null;
	}

	/// <summary>
	/// Gets a projection by its unique identifier.
	/// </summary>
	/// <param name="id">The projection identifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The projection if found; otherwise, <c>null</c>.</returns>
	/// <remarks>
	/// Confined to the ambient tenant established for this store instance: a projection stored under
	/// another tenant under the same <paramref name="id"/> is reported as not found.
	/// </remarks>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
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
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
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
	/// <para>
	/// This operation is idempotent - deleting a non-existent projection succeeds silently.
	/// </para>
	/// <para>
	/// Confined to the ambient tenant, like every other operation on this store: a projection stored
	/// under another tenant with the same <paramref name="id"/> is not deleted, and the call succeeds
	/// silently because that projection is not visible to this caller. An implementation that omits the
	/// tenant term here deletes another tenant's row -- silent data loss rather than a disclosure.
	/// </para>
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
	/// <para>
	/// Confined to the ambient tenant established for this store instance: returns none of another
	/// tenant's projections, and every one of the caller's own that matches <paramref name="filters"/>.
	/// </para>
	/// </remarks>
	/// <param name="filters">Dictionary-based filter conditions. Pass <c>null</c> for no filtering.</param>
	/// <param name="options">Query options for pagination and sorting. Pass <c>null</c> for defaults.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The matching projections.</returns>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
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
