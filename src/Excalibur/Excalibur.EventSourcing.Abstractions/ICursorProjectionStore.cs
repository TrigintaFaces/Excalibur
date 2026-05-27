// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.EventSourcing.Abstractions;

/// <summary>
/// Extends <see cref="IProjectionStore{TProjection}"/> with cursor-based (keyset) pagination support.
/// </summary>
/// <remarks>
/// <para>
/// This is an ISP sub-interface following the <c>IBufferDistributedCache</c> precedent.
/// Consumers check for capability via pattern matching:
/// <code>
/// if (store is ICursorProjectionStore&lt;MyProjection&gt; cursorStore)
/// {
///     var result = await cursorStore.QueryCursorAsync(filters, null, 20, ct);
///     // Use result.NextCursor for subsequent pages
/// }
/// </code>
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
    Task<CursorPagedResult<TProjection>> QueryCursorAsync(
        IDictionary<string, object>? filters,
        string? cursor,
        int pageSize,
        CancellationToken cancellationToken);
}
