// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.EventSourcing.Abstractions;

/// <summary>
/// Extends <see cref="IProjectionStore{TProjection}"/> with offset-based pagination support.
/// </summary>
/// <remarks>
/// <para>
/// This is an ISP sub-interface following the <c>IBufferDistributedCache</c> precedent.
/// Consumers check for capability via pattern matching:
/// <code>
/// if (store is IPageableProjectionStore&lt;MyProjection&gt; paged)
/// {
///     var result = await paged.QueryPagedAsync(filters, 1, 20, null, ct);
/// }
/// </code>
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
    Task<PagedResult<TProjection>> QueryPagedAsync(
        IDictionary<string, object>? filters,
        int pageNumber,
        int pageSize,
        QueryOptions? options,
        CancellationToken cancellationToken);
}
