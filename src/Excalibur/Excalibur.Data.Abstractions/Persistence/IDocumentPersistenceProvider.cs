// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.Abstractions.Persistence;

/// <summary>
/// Specialized persistence provider for document stores that handles DocumentDataRequest execution
/// with document-specific capabilities. Advanced features (bulk operations, aggregation, indexing,
/// statistics) are available via <see cref="GetService"/>.
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

	/// <summary>
	/// Gets an implementation-specific service. Use to access advanced document features
	/// such as <see cref="IDocumentBulkOperations"/>, <see cref="IDocumentAggregation"/>,
	/// <see cref="IDocumentIndexing"/>, or <see cref="IDocumentStatistics"/>.
	/// </summary>
	/// <param name="serviceType">The type of the requested service.</param>
	/// <returns>The service instance, or <see langword="null"/> if not supported.</returns>
	new object? GetService(Type serviceType) => null;
}

/// <summary>
/// Provides document bulk operation capabilities. Obtain via
/// <see cref="IDocumentPersistenceProvider.GetService"/>.
/// </summary>
public interface IDocumentBulkOperations
{
	/// <summary>
	/// Executes a bulk DocumentDataRequest optimized for large document operations.
	/// </summary>
	/// <typeparam name="TConnection"> The type of the document database connection. </typeparam>
	/// <typeparam name="TResult"> The type of the result. </typeparam>
	/// <param name="bulkDocumentRequest"> The bulk document data request to execute. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The result of the bulk document operation. </returns>
	Task<TResult> ExecuteBulkDocumentAsync<TConnection, TResult>(
		IDocumentDataRequest<TConnection, TResult> bulkDocumentRequest,
		CancellationToken cancellationToken);
}

/// <summary>
/// Provides document aggregation pipeline capabilities. Obtain via
/// <see cref="IDocumentPersistenceProvider.GetService"/>.
/// </summary>
public interface IDocumentAggregation
{
	/// <summary>
	/// Executes an aggregation DocumentDataRequest with document store-specific pipeline optimizations.
	/// </summary>
	/// <typeparam name="TConnection"> The type of the document database connection. </typeparam>
	/// <typeparam name="TResult"> The type of the aggregation result. </typeparam>
	/// <param name="aggregationRequest"> The aggregation document data request to execute. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The result of the aggregation operation. </returns>
	Task<TResult> ExecuteAggregationAsync<TConnection, TResult>(
		IDocumentDataRequest<TConnection, TResult> aggregationRequest,
		CancellationToken cancellationToken);
}

/// <summary>
/// Provides document index management capabilities. Obtain via
/// <see cref="IDocumentPersistenceProvider.GetService"/>.
/// </summary>
public interface IDocumentIndexing
{
	/// <summary>
	/// Creates or manages an index using a DocumentDataRequest.
	/// </summary>
	/// <typeparam name="TConnection"> The type of the document database connection. </typeparam>
	/// <param name="indexRequest"> The index management document data request. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The result of the index operation (e.g., index name, status). </returns>
	Task<string> ExecuteIndexOperationAsync<TConnection>(
		IDocumentDataRequest<TConnection, string> indexRequest,
		CancellationToken cancellationToken);
}

/// <summary>
/// Provides document store statistics and schema information. Obtain via
/// <see cref="IDocumentPersistenceProvider.GetService"/>.
/// </summary>
public interface IDocumentStatistics
{
	/// <summary>
	/// Gets comprehensive document store statistics and performance metrics.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> Document store statistics including collection stats, index usage, and resource utilization. </returns>
	Task<IDictionary<string, object>> GetDocumentStoreStatisticsAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Gets collection schema information and statistics.
	/// </summary>
	/// <param name="collectionName"> The name of the collection. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> Collection information including schema, indexes, and performance metrics. </returns>
	Task<IDictionary<string, object>> GetCollectionInfoAsync(
		string collectionName,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the supported document operation types for this provider.
	/// </summary>
	/// <returns> A collection of supported operation types (e.g., "Insert", "Find", "Update", "Delete", "Aggregate"). </returns>
	IEnumerable<string> GetSupportedOperationTypes();
}
