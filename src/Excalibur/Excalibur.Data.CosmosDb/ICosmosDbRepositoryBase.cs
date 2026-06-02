// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.CosmosDb;

/// <summary>
/// Provides base repository functionality for interacting with Cosmos DB for a specific document type.
/// </summary>
/// <typeparam name="TDocument">The type of the document to manage in Cosmos DB.</typeparam>
public interface ICosmosDbRepositoryBase<TDocument>
	where TDocument : class
{
	/// <summary>
	/// Retrieves a document from Cosmos DB by its ID and partition key.
	/// </summary>
	/// <param name="documentId">The unique identifier of the document.</param>
	/// <param name="partitionKey">The partition key value for the document.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The document if found, or <c>null</c> if not found.</returns>
	Task<TDocument?> GetByIdAsync(string documentId, string partitionKey, CancellationToken cancellationToken);

	/// <summary>
	/// Adds or updates a document in the Cosmos DB container.
	/// </summary>
	/// <param name="documentId">The unique identifier for the document.</param>
	/// <param name="document">The document to be added or updated.</param>
	/// <param name="partitionKey">The partition key value for the document.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns><see langword="true"/> if the operation succeeded.</returns>
	Task<bool> AddOrUpdateAsync(string documentId, TDocument document, string partitionKey, CancellationToken cancellationToken);

	/// <summary>
	/// Removes a document from the Cosmos DB container.
	/// </summary>
	/// <param name="documentId">The unique identifier of the document to remove.</param>
	/// <param name="partitionKey">The partition key value for the document.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns><see langword="true"/> if the document was removed; <see langword="false"/> if not found.</returns>
	Task<bool> RemoveAsync(string documentId, string partitionKey, CancellationToken cancellationToken);
}

/// <summary>
/// Provides query capabilities for Cosmos DB repositories. This is an ISP sub-interface
/// of <see cref="ICosmosDbRepositoryBase{TDocument}"/> for consumers that need search functionality
/// beyond basic CRUD operations.
/// </summary>
/// <typeparam name="TDocument">The type of the document to query in Cosmos DB.</typeparam>
public interface ICosmosDbRepositoryBaseQuery<TDocument>
	where TDocument : class
{
	/// <summary>
	/// Executes a Cosmos SQL query against the container.
	/// </summary>
	/// <param name="sql">The Cosmos SQL query text.</param>
	/// <param name="parameters">Optional query parameters.</param>
	/// <param name="partitionKey">The partition key to scope the query.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A read-only list of matched documents.</returns>
	Task<IReadOnlyList<TDocument>> QueryAsync(
		string sql,
		IReadOnlyDictionary<string, object>? parameters,
		string partitionKey,
		CancellationToken cancellationToken);
}
