// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace Excalibur.EventSourcing.Abstractions;

/// <summary>
/// Extension methods for <see cref="IProjectionStore{TProjection}"/> providing
/// common query operations with provider-optimized escape hatches.
/// </summary>
public static class ProjectionStoreExtensions
{
    /// <summary>
    /// Checks whether a projection with the specified identifier exists.
    /// </summary>
    /// <typeparam name="TProjection">The projection type.</typeparam>
    /// <param name="store">The projection store.</param>
    /// <param name="id">The projection identifier to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <see langword="true"/> if a projection with the specified <paramref name="id"/> exists;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// If the store implements <see cref="IExistsProjectionStore{TProjection}"/>,
    /// the provider-optimized path is used (e.g., <c>SELECT TOP 1 1</c> in SQL,
    /// <c>HEAD</c> request in CosmosDB). Otherwise, falls back to
    /// <see cref="IProjectionStore{TProjection}.GetByIdAsync"/> with a null check.
    /// </para>
    /// </remarks>
    public static async Task<bool> ExistsAsync<TProjection>(
        this IProjectionStore<TProjection> store,
        string id,
        CancellationToken cancellationToken)
        where TProjection : class
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrEmpty(id);

        // Provider escape hatch: optimized existence check without full deserialization
        if (store is IExistsProjectionStore<TProjection> optimized)
        {
            return await optimized.ExistsAsync(id, cancellationToken).ConfigureAwait(false);
        }

        // Fallback: load full projection and null-check
        var result = await store.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return result is not null;
    }

    /// <summary>
    /// Gets the distinct values of a specified property across all projections
    /// matching the given filters. Useful for populating filter dropdown options.
    /// </summary>
    /// <typeparam name="TProjection">The projection type.</typeparam>
    /// <param name="store">The projection store.</param>
    /// <param name="propertyName">The property name to extract distinct values from.</param>
    /// <param name="filters">
    /// Optional dictionary-based filter conditions to scope the distinct values.
    /// Pass <c>null</c> for no filtering.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A read-only list of distinct values for the specified property.</returns>
    /// <remarks>
    /// <para>
    /// If the store implements <see cref="IDistinctValuesProjectionStore{TProjection}"/>,
    /// the provider-optimized path is used (e.g., <c>SELECT DISTINCT</c> in SQL,
    /// aggregation pipeline in MongoDB). Otherwise, falls back to
    /// <see cref="IProjectionStore{TProjection}.QueryAsync"/> and extracts distinct values
    /// via reflection.
    /// </para>
    /// <para>
    /// The fallback path loads all matching projections into memory and is not suitable
    /// for large datasets. Providers should implement the escape hatch for production use.
    /// </para>
    /// </remarks>
    [RequiresUnreferencedCode("Fallback uses reflection to extract property values. Implement IDistinctValuesProjectionStore<T> for AOT-safe usage.")]
    public static async Task<IReadOnlyList<object>> DistinctValuesAsync<TProjection>(
        this IProjectionStore<TProjection> store,
        string propertyName,
        IDictionary<string, object>? filters,
        CancellationToken cancellationToken)
        where TProjection : class
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrEmpty(propertyName);

        // Provider escape hatch: native distinct query
        if (store is IDistinctValuesProjectionStore<TProjection> optimized)
        {
            return await optimized.DistinctValuesAsync(propertyName, filters, cancellationToken)
                .ConfigureAwait(false);
        }

        // Fallback: load all matching projections and extract distinct values via reflection
        var projections = await store.QueryAsync(filters, null, cancellationToken).ConfigureAwait(false);

        var property = typeof(TProjection).GetProperty(propertyName)
            ?? throw new ArgumentException(
                $"Property '{propertyName}' not found on type '{typeof(TProjection).Name}'.",
                nameof(propertyName));

        var distinctValues = new HashSet<object>();
        foreach (var projection in projections)
        {
            var value = property.GetValue(projection);
            if (value is not null)
            {
                distinctValues.Add(value);
            }
        }

        return distinctValues.ToList().AsReadOnly();
    }
}
