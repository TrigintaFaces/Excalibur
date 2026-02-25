// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.DynamoDb.Caching;

/// <summary>
/// Defines the contract for DynamoDB Accelerator (DAX) caching operations.
/// </summary>
/// <remarks>
/// <para>
/// Reference: <c>Microsoft.Extensions.Caching.IDistributedCache</c> pattern --
/// minimal CRUD surface (3 methods) for cache operations.
/// DAX provides a write-through/read-through cache for DynamoDB tables.
/// </para>
/// </remarks>
public interface IDaxCacheProvider
{
	/// <summary>
	/// Retrieves a cached item by its key.
	/// </summary>
	/// <typeparam name="T">The type of the cached item.</typeparam>
	/// <param name="tableName">The DynamoDB table name.</param>
	/// <param name="partitionKey">The partition key value.</param>
	/// <param name="sortKey">The optional sort key value.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The cached item, or <see langword="null"/> if not found in cache.</returns>
	Task<T?> GetItemAsync<T>(string tableName, string partitionKey, string? sortKey, CancellationToken cancellationToken)
		where T : class;

	/// <summary>
	/// Stores an item in the DAX cache.
	/// </summary>
	/// <typeparam name="T">The type of the item to cache.</typeparam>
	/// <param name="tableName">The DynamoDB table name.</param>
	/// <param name="partitionKey">The partition key value.</param>
	/// <param name="sortKey">The optional sort key value.</param>
	/// <param name="item">The item to cache.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	Task PutItemAsync<T>(string tableName, string partitionKey, string? sortKey, T item, CancellationToken cancellationToken)
		where T : class;

	/// <summary>
	/// Invalidates a cached item, removing it from the cache.
	/// </summary>
	/// <param name="tableName">The DynamoDB table name.</param>
	/// <param name="partitionKey">The partition key value.</param>
	/// <param name="sortKey">The optional sort key value.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	Task InvalidateAsync(string tableName, string partitionKey, string? sortKey, CancellationToken cancellationToken);
}
