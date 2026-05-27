// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Abstractions;

/// <summary>
/// ISP sub-interface for projection stores that support optimized distinct value queries.
/// </summary>
/// <remarks>
/// <para>
/// Providers implement this interface to offer native distinct value extraction
/// (e.g., <c>SELECT DISTINCT</c> in SQL, aggregation pipeline in MongoDB,
/// terms aggregation in Elasticsearch). This avoids the default fallback which
/// loads all matching projections into memory.
/// </para>
/// <para>
/// Consumers use the extension method <see cref="ProjectionStoreExtensions.DistinctValuesAsync{TProjection}"/>
/// which automatically detects and uses this interface when available:
/// <code>
/// var statuses = await store.DistinctValuesAsync("Status", null, ct);
/// // Returns: ["Active", "Completed", "Cancelled"]
/// </code>
/// </para>
/// </remarks>
/// <typeparam name="TProjection">The projection type. Must be a reference type.</typeparam>
public interface IDistinctValuesProjectionStore<TProjection> : IProjectionStore<TProjection>
    where TProjection : class
{
    /// <summary>
    /// Gets the distinct values of a specified property across all projections
    /// matching the given filters, using a provider-native query.
    /// </summary>
    /// <param name="propertyName">The property name to extract distinct values from.</param>
    /// <param name="filters">
    /// Optional dictionary-based filter conditions to scope the distinct values.
    /// Pass <c>null</c> for no filtering.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A read-only list of distinct values for the specified property.</returns>
    Task<IReadOnlyList<object>> DistinctValuesAsync(
        string propertyName,
        IDictionary<string, object>? filters,
        CancellationToken cancellationToken);
}
