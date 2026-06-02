// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using MongoDB.Bson;
using MongoDB.Driver;

namespace Excalibur.Data.MongoDB;

/// <summary>
/// Provides base repository functionality for interacting with MongoDB for a specific document type.
/// </summary>
/// <typeparam name="TDocument">The type of the document to manage in MongoDB.</typeparam>
public interface IMongoDbRepositoryBase<TDocument>
	where TDocument : class
{
	/// <summary>
	/// Retrieves a document from MongoDB by its ID.
	/// </summary>
	/// <param name="documentId">The unique identifier of the document.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The document if found, or <c>null</c> if not found.</returns>
	Task<TDocument?> GetByIdAsync(string documentId, CancellationToken cancellationToken);

	/// <summary>
	/// Adds or updates a document in the MongoDB collection.
	/// </summary>
	/// <param name="documentId">The unique identifier for the document.</param>
	/// <param name="document">The document to be added or updated.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns><see langword="true"/> if the operation was acknowledged.</returns>
	Task<bool> AddOrUpdateAsync(string documentId, TDocument document, CancellationToken cancellationToken);

	/// <summary>
	/// Removes a document from the MongoDB collection.
	/// </summary>
	/// <param name="documentId">The unique identifier of the document to remove.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns><see langword="true"/> if the document was removed; <see langword="false"/> if not found.</returns>
	Task<bool> RemoveAsync(string documentId, CancellationToken cancellationToken);
}

/// <summary>
/// Provides query capabilities for MongoDB repositories. This is an ISP sub-interface
/// of <see cref="IMongoDbRepositoryBase{TDocument}"/> for consumers that need search functionality
/// beyond basic CRUD operations.
/// </summary>
/// <typeparam name="TDocument">The type of the document to query in MongoDB.</typeparam>
public interface IMongoDbRepositoryBaseQuery<TDocument>
	where TDocument : class
{
	/// <summary>
	/// Executes a find query against the MongoDB collection.
	/// </summary>
	/// <param name="filter">The filter definition for the query.</param>
	/// <param name="sort">Optional sort definition.</param>
	/// <param name="skip">Optional number of documents to skip.</param>
	/// <param name="limit">Optional maximum number of documents to return.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A read-only list of matched documents.</returns>
	Task<IReadOnlyList<TDocument>> FindAsync(
		FilterDefinition<BsonDocument> filter,
		SortDefinition<BsonDocument>? sort,
		int? skip,
		int? limit,
		CancellationToken cancellationToken);
}
