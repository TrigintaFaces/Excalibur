// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

namespace Excalibur.EventSourcing;

/// <summary>
/// Extends <see cref="IProjectionStore{TProjection}"/> with cursor-based (keyset) pagination support.
/// </summary>
/// <remarks>
/// <para>
/// This is an ISP sub-interface following the <c>IBufferDistributedCache</c> precedent.
/// Consumers reach it through <see cref="ProjectionStoreExtensions.QueryCursorAsync"/>, which returns
/// <see langword="null"/> when the store does not support cursor pagination:
/// <code>
/// var result = await store.QueryCursorAsync(filters, null, 20, ct);
/// if (result is not null)
/// {
///     // Use result.NextCursor for subsequent pages
/// }
/// </code>
/// </para>
/// <para>
/// Do not test the store's type to detect this capability. A store is commonly reached through a
/// decorator -- tenant scoping, encryption -- whose own interface list is fixed when it is compiled, so
/// the test reports the decorator and not the store beneath it. Ask the store for the capability instead
/// (<see cref="IServiceProvider.GetService(System.Type)"/>), which the extension method above already does.
/// </para>
/// <para>
/// Cursor-based pagination provides stable results under concurrent writes and better
/// performance on large datasets compared to offset-based pagination. The cursor is an
/// opaque string produced by <see cref="CursorEncoder"/> — consumers must not parse it.
/// </para>
/// <para>
/// Provider-specific implementations encode cursor values appropriate to their backend
/// (e.g., Elasticsearch <c>search_after</c>, SQL Server keyset queries, DynamoDB
/// <c>ExclusiveStartKey</c>).
/// </para>
/// </remarks>
/// <typeparam name="TProjection">The projection type. Must be a reference type.</typeparam>
public interface ICursorProjectionStore<TProjection> : IProjectionStore<TProjection>
	where TProjection : class
{
	/// <summary>
	/// Queries projections with cursor-based pagination, returning a page of results
	/// along with a continuation token for the next page.
	/// </summary>
	/// <param name="filters">Dictionary-based filter conditions. Pass <c>null</c> for no filtering.</param>
	/// <param name="cursor">
	/// An opaque continuation token from a previous call's <see cref="CursorPagedResult{T}.NextCursor"/>.
	/// Pass <c>null</c> to start from the beginning.
	/// </param>
	/// <param name="pageSize">The number of items per page.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// A <see cref="CursorPagedResult{T}"/> containing the page items and a continuation cursor.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="pageSize"/> is less than 1.
	/// </exception>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	Task<CursorPagedResult<TProjection>> QueryCursorAsync(
		IDictionary<string, object>? filters,
		string? cursor,
		int pageSize,
		CancellationToken cancellationToken);
}
