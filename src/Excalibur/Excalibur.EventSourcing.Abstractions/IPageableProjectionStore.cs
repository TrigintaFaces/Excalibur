// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

namespace Excalibur.EventSourcing;

/// <summary>
/// Extends <see cref="IProjectionStore{TProjection}"/> with offset-based pagination support.
/// </summary>
/// <remarks>
/// <para>
/// This is an ISP sub-interface following the <c>IBufferDistributedCache</c> precedent.
/// Consumers reach it through <see cref="ProjectionStoreExtensions.QueryPagedAsync"/>, which uses the
/// provider's native paging when it is available and pages in memory when it is not:
/// <code>
/// var result = await store.QueryPagedAsync(filters, 1, 20, null, ct);
/// </code>
/// </para>
/// <para>
/// Do not test the store's type to detect this capability. A store is commonly reached through a
/// decorator -- tenant scoping, encryption -- whose own interface list is fixed when it is compiled, so
/// the test reports the decorator and not the store beneath it. Ask the store for the capability instead
/// (<see cref="IServiceProvider.GetService(System.Type)"/>), which the extension method above already does.
/// </para>
/// <para>
/// The default implementation falls back to <see cref="IProjectionStore{TProjection}.QueryAsync"/>
/// plus <see cref="IProjectionStore{TProjection}.CountAsync"/> for two-roundtrip pagination.
/// Provider-specific implementations (SQL Server, MongoDB, CosmosDB, DynamoDB, InMemory) override
/// this with single-roundtrip native queries (e.g., SQL OFFSET/FETCH, MongoDB skip/limit).
/// </para>
/// </remarks>
/// <typeparam name="TProjection">The projection type. Must be a reference type.</typeparam>
public interface IPageableProjectionStore<TProjection> : IProjectionStore<TProjection>
	where TProjection : class
{
	/// <summary>
	/// Queries projections with offset-based pagination, returning a page of results
	/// along with total count metadata.
	/// </summary>
	/// <param name="filters">Dictionary-based filter conditions. Pass <c>null</c> for no filtering.</param>
	/// <param name="pageNumber">The 1-based page number to retrieve.</param>
	/// <param name="pageSize">The number of items per page.</param>
	/// <param name="options">Query options for sorting. Pass <c>null</c> for default ordering.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A <see cref="PagedResult{T}"/> containing the page items and pagination metadata.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="pageNumber"/> is less than 1 or <paramref name="pageSize"/> is less than 1.
	/// </exception>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	Task<PagedResult<TProjection>> QueryPagedAsync(
		IDictionary<string, object>? filters,
		int pageNumber,
		int pageSize,
		QueryOptions? options,
		CancellationToken cancellationToken);
}
