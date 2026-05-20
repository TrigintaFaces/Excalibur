// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.DynamoDBv2.Model;

namespace Excalibur.Data.DynamoDb;

/// <summary>
/// Provides base repository functionality for interacting with DynamoDB for a specific document type.
/// </summary>
/// <typeparam name="TDocument">The type of the document to manage in DynamoDB.</typeparam>
public interface IDynamoDbRepositoryBase<TDocument>
	where TDocument : class
{
	/// <summary>
	/// Retrieves a document from DynamoDB by its partition key.
	/// </summary>
	/// <param name="documentId">The partition key value of the document.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The document if found, or <c>null</c> if not found.</returns>
	Task<TDocument?> GetByIdAsync(string documentId, CancellationToken cancellationToken);

	/// <summary>
	/// Adds or updates a document in the DynamoDB table.
	/// </summary>
	/// <param name="documentId">The partition key value for the document.</param>
	/// <param name="document">The document to be added or updated.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns><see langword="true"/> if the operation succeeded.</returns>
	Task<bool> AddOrUpdateAsync(string documentId, TDocument document, CancellationToken cancellationToken);

	/// <summary>
	/// Removes a document from the DynamoDB table.
	/// </summary>
	/// <param name="documentId">The partition key value of the document to remove.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns><see langword="true"/> if the operation succeeded.</returns>
	Task<bool> RemoveAsync(string documentId, CancellationToken cancellationToken);
}

/// <summary>
/// Provides query capabilities for DynamoDB repositories. This is an ISP sub-interface
/// of <see cref="IDynamoDbRepositoryBase{TDocument}"/> for consumers that need scan functionality
/// beyond basic CRUD operations.
/// </summary>
/// <typeparam name="TDocument">The type of the document to query in DynamoDB.</typeparam>
public interface IDynamoDbRepositoryBaseQuery<TDocument>
	where TDocument : class
{
	/// <summary>
	/// Executes a scan operation against the DynamoDB table.
	/// </summary>
	/// <param name="request">The scan request configuration.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A read-only list of matched documents.</returns>
	Task<IReadOnlyList<TDocument>> ScanAsync(
		ScanRequest request,
		CancellationToken cancellationToken);
}
