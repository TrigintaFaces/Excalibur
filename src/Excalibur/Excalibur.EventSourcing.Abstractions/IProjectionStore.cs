// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


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
/// <b>Tenant confinement.</b> Isolation is declared by the resolved
/// <see cref="IProjectionTenantBoundary"/> capability, not implied by ambient tenant context.
/// A tenant-partitioned or tenant-routing store confines reads, writes and deletion to the selected
/// tenant. An estate-wide store intentionally shares its physical key space; the same identifier
/// addresses the same projection regardless of ambient tenant. Use an appropriate tenant-aware
/// boundary before processing tenant-owned events. A store with no declared boundary does not
/// establish tenant isolation. Query results additionally follow their filters and paging options.
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
	/// Within a tenant-partitioned or tenant-routing store, the same <paramref name="id"/> in another
	/// tenant is reported as not found. Estate-wide stores share their physical key space.
	/// </remarks>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	Task<TProjection?> GetByIdAsync(
		string id,
		CancellationToken cancellationToken);

	/// <summary>
	/// Creates or updates a projection, leaving it with NO PLACEABLE POSITION.
	/// </summary>
	/// <param name="id">The projection identifier.</param>
	/// <param name="projection">The projection to store.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <remarks>
	/// <para>
	/// <b>This is not a positioned write, and a positioned client must not use it.</b> It replaces the
	/// state with a value the store cannot relate to the event stream, so on a store that records
	/// positions the row is left <c>Unplaceable</c>: not a fold over any prefix, and therefore something
	/// no later positioned write may advance from -- as is a row left <c>Unnumbered</c>, so saying it only
	/// of this one would imply the other is advanceable. What separates them is what the row ASSERTS: an
	/// unplaceable state is not a fold over any prefix, where an unnumbered one is a complete fold missing
	/// only its coordinate. That is recorded explicitly, in the same atomic action
	/// that writes the state -- never by silently dropping or resetting the position, which would make a
	/// destroyed position indistinguishable from one that was never established.
	/// </para>
	/// <para>
	/// <b>Calling this on a positioned store makes the projection unadvanceable until it is rebuilt.</b>
	/// A positioned write refuses every row carrying no number, so the next batch is refused rather than
	/// folded, and the repair is
	/// <c>IPositionedProjectionStore&lt;TProjection&gt;.RebuildAtPositionAsync</c> after a whole-stream
	/// replay. A caller that has a complete fold but no position NUMBER wants
	/// <c>IPositionedProjectionStore&lt;TProjection&gt;.UpsertUnnumberedAsync</c> instead: that row is
	/// refused too, but it reads back as a complete fold rather than as a state related to no prefix at
	/// all, which is what a reader and an operator each need. Reaching for this method to express that
	/// makes the stronger and wrong claim.
	/// </para>
	/// <para>
	/// <b>Why the specification says this at all.</b> It is this operation, inherited by the positioned
	/// sub-interface, that can falsify the fold invariant that sub-interface declares -- so a client
	/// holding the base interface can break the subtype's invariant from outside while staying entirely
	/// within its rights. Leaving the effect unstated is what let eight implementations agree by
	/// accident rather than by contract.
	/// </para>
	/// </remarks>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	Task UpsertAsync(
		string id,
		TProjection projection,
		CancellationToken cancellationToken);

	/// <summary>
	/// Permanently deletes a projection key within the store's tenant boundary and physical generation.
	/// </summary>
	/// <param name="id">The projection identifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <remarks>
	/// <para>
	/// This operation is idempotent. Deleting an absent key also records terminal retirement.
	/// The store atomically removes projection payload and retains minimal key/deletion metadata.
	/// Reads, queries and counts hide that metadata. Later writes cannot recreate the key;
	/// conditional writes report Vanished and unconditional writes fail explicitly.
	/// To repopulate the key, rebuild into a new physical generation. Retirement metadata must
	/// remain while the current generation can receive writes; physical removal is not a reset.
	/// This does not erase the source event history or every identifier in retained metadata.
    /// Retirement is local to the destination key. It does not cancel handlers already running,
    /// discovery of alternate projection keys, or external side effects; those must remain idempotent.
	/// </para>
	/// <para>
	/// Tenant isolation follows the store's <see cref="IProjectionTenantBoundary"/> capability.
	/// A tenant-partitioned or tenant-routing store retires only the current tenant's key; the same
	/// <paramref name="id"/> in another tenant remains unchanged. An estate-wide store has a shared
	/// physical key space: ambient tenant context alone does not isolate deletion. Use a tenant-aware
	/// store boundary when separate tenants may use the same projection identifier.
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
	/// Tenant-partitioned and tenant-routing stores exclude other tenants' projections before applying
	/// <paramref name="filters"/> and paging options. Estate-wide stores query their shared destination.
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
