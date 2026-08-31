// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.Persistence;

/// <summary>
/// Specialized persistence provider for document stores that handles DocumentDataRequest execution
/// with document-specific capabilities. Implementation-specific services are available via
/// <see cref="IPersistenceProvider.GetService"/>.
/// </summary>
public interface IDocumentPersistenceProvider : IPersistenceProvider
{
	/// <summary>
	/// Gets the document store type (e.g., "MongoDB", "ElasticSearch", "CosmosDB").
	/// </summary>
	/// <value>
	/// The document store type (e.g., "MongoDB", "ElasticSearch", "CosmosDB").
	/// </value>
	string DocumentStoreType { get; }

	/// <summary>
	/// Executes a DocumentDataRequest with document store-specific optimizations.
	/// </summary>
	/// <typeparam name="TConnection"> The type of the document database connection. </typeparam>
	/// <typeparam name="TResult"> The type of the result. </typeparam>
	/// <param name="documentRequest"> The document data request to execute. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The result of the document request execution. </returns>
	Task<TResult> ExecuteDocumentAsync<TConnection, TResult>(
		IDocumentDataRequest<TConnection, TResult> documentRequest,
		CancellationToken cancellationToken);

	/// <summary>
	/// Executes a DocumentDataRequest within a transaction scope (if supported by the document store).
	/// </summary>
	/// <typeparam name="TConnection"> The type of the document database connection. </typeparam>
	/// <typeparam name="TResult"> The type of the result. </typeparam>
	/// <param name="documentRequest"> The document data request to execute. </param>
	/// <param name="transactionScope"> The transaction scope to use. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The result of the document request execution. </returns>
	Task<TResult> ExecuteDocumentInTransactionAsync<TConnection, TResult>(
		IDocumentDataRequest<TConnection, TResult> documentRequest,
		ITransactionScope transactionScope,
		CancellationToken cancellationToken);

	/// <summary>
	/// Executes a batch of DocumentDataRequests as a single unit for improved performance.
	/// </summary>
	/// <typeparam name="TConnection"> The type of the document database connection. </typeparam>
	/// <param name="documentRequests"> The collection of DocumentDataRequests to execute as a batch. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A collection of results corresponding to each request in the batch. </returns>
	Task<IEnumerable<object>> ExecuteDocumentBatchAsync<TConnection>(
		IEnumerable<IDocumentDataRequest<TConnection, object>> documentRequests,
		CancellationToken cancellationToken);

	/// <summary>
	/// Validates that a DocumentDataRequest is compatible with this document store provider.
	/// </summary>
	/// <typeparam name="TConnection"> The type of the document database connection. </typeparam>
	/// <typeparam name="TResult"> The type of the result. </typeparam>
	/// <param name="documentRequest"> The DocumentDataRequest to validate. </param>
	/// <returns> True if the request is valid for this provider; otherwise, false. </returns>
	bool ValidateDocumentRequest<TConnection, TResult>(IDocumentDataRequest<TConnection, TResult> documentRequest);
}
