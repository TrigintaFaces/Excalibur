// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace Excalibur.EventSourcing;

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
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public static async Task<bool> ExistsAsync<TProjection>(
		this IProjectionStore<TProjection> store,
		string id,
		CancellationToken cancellationToken)
		where TProjection : class
	{
		ArgumentNullException.ThrowIfNull(store);
		ArgumentException.ThrowIfNullOrEmpty(id);

		// Provider escape hatch: optimized existence check without full deserialization
		if (store.GetService(typeof(IExistsProjectionStore<TProjection>)) is IExistsProjectionStore<TProjection> optimized)
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
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
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
		if (store.GetService(typeof(IDistinctValuesProjectionStore<TProjection>)) is IDistinctValuesProjectionStore<TProjection> optimized)
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

	/// <summary>
	/// Queries projections with offset-based pagination.
	/// </summary>
	/// <typeparam name="TProjection">The projection type.</typeparam>
	/// <param name="store">The projection store.</param>
	/// <param name="filters">Dictionary-based filter conditions. Pass <c>null</c> for no filtering.</param>
	/// <param name="pageNumber">The 1-based page number to retrieve.</param>
	/// <param name="pageSize">The number of items per page.</param>
	/// <param name="options">Query options for sorting. Pass <c>null</c> for default ordering.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A <see cref="PagedResult{T}"/> containing the page items and pagination metadata.</returns>
	/// <remarks>
	/// <para>
	/// Uses the provider's native pagination when the store offers
	/// <see cref="IPageableProjectionStore{TProjection}"/>; otherwise pages the result of
	/// <see cref="IProjectionStore{TProjection}.QueryAsync"/> in memory.
	/// </para>
	/// <para>
	/// Prefer this over testing the store's type. A store reached through a decorator -- tenant scoping,
	/// encryption -- does not itself implement the capability interface even when the store beneath it does,
	/// so a type test silently selects the in-memory fallback. This method asks the store for the capability
	/// instead, which a decorator answers on behalf of the store it wraps.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="pageNumber"/> or <paramref name="pageSize"/> is less than 1.
	/// </exception>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public static async Task<PagedResult<TProjection>> QueryPagedAsync<TProjection>(
		this IProjectionStore<TProjection> store,
		IDictionary<string, object>? filters,
		int pageNumber,
		int pageSize,
		QueryOptions? options,
		CancellationToken cancellationToken)
		where TProjection : class
	{
		ArgumentNullException.ThrowIfNull(store);
		ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);
		ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

		// Provider escape hatch: native paging, resolved through the store so decoration cannot hide it
		if (store.GetService(typeof(IPageableProjectionStore<TProjection>)) is IPageableProjectionStore<TProjection> optimized)
		{
			return await optimized.QueryPagedAsync(filters, pageNumber, pageSize, options, cancellationToken)
				.ConfigureAwait(false);
		}

		// Fallback: query and page in memory
		var all = await store.QueryAsync(filters, options, cancellationToken).ConfigureAwait(false);
		var page = all.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

		return new PagedResult<TProjection>(page, pageNumber, pageSize, all.Count);
	}

	/// <summary>
	/// Queries projections with cursor-based pagination.
	/// </summary>
	/// <typeparam name="TProjection">The projection type.</typeparam>
	/// <param name="store">The projection store.</param>
	/// <param name="filters">Dictionary-based filter conditions. Pass <c>null</c> for no filtering.</param>
	/// <param name="cursor">
	/// An opaque continuation token from a previous call's <see cref="CursorPagedResult{T}.NextCursor"/>.
	/// Pass <c>null</c> to start from the beginning.
	/// </param>
	/// <param name="pageSize">The number of items per page.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// A <see cref="CursorPagedResult{T}"/> when the store supports cursor pagination; otherwise
	/// <see langword="null"/>.
	/// </returns>
	/// <remarks>
	/// <para>
	/// Cursor pagination has no correct in-memory fallback: the cursor is an opaque provider token, and
	/// synthesising one would produce a token the provider cannot honour. This method therefore reports the
	/// absence of the capability as <see langword="null"/> rather than inventing a page, so a caller can
	/// choose offset pagination instead.
	/// </para>
	/// <para>
	/// Prefer this over testing the store's type, for the reason given on
	/// <see cref="QueryPagedAsync"/>: a decorator does not implement the capability interface even when the
	/// store it wraps does.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="pageSize"/> is less than 1.</exception>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public static async Task<CursorPagedResult<TProjection>?> QueryCursorAsync<TProjection>(
		this IProjectionStore<TProjection> store,
		IDictionary<string, object>? filters,
		string? cursor,
		int pageSize,
		CancellationToken cancellationToken)
		where TProjection : class
	{
		ArgumentNullException.ThrowIfNull(store);
		ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

		if (store.GetService(typeof(ICursorProjectionStore<TProjection>)) is not ICursorProjectionStore<TProjection> optimized)
		{
			return null;
		}

		return await optimized.QueryCursorAsync(filters, cursor, pageSize, cancellationToken).ConfigureAwait(false);
	}
}
