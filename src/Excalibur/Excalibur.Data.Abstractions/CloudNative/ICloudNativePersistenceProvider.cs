// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.Persistence;

namespace Excalibur.Data.CloudNative;

/// <summary>
/// Defines the cloud persistence provider types supported by the framework.
/// </summary>
/// <remarks>
/// This enum represents cloud-native <em>persistence</em> providers (Cosmos DB, DynamoDB, Firestore).
/// For cloud-native <em>transport</em> providers (AWS, Azure, Google, Kafka, etc.), see
/// <c>Excalibur.Dispatch.Transport.Abstractions.CloudProviderType</c>.
/// </remarks>
public enum CloudPersistenceProviderType
{
	/// <summary>
	/// Azure Cosmos DB.
	/// </summary>
	CosmosDb = 0,

	/// <summary>
	/// AWS DynamoDB.
	/// </summary>
	DynamoDb = 1,

	/// <summary>
	/// Google Cloud Firestore.
	/// </summary>
	Firestore = 2
}

/// <summary>
/// Defines the types of batch operations.
/// </summary>
public enum CloudBatchOperationType
{
	/// <summary>
	/// Create a new document.
	/// </summary>
	Create = 0,

	/// <summary>
	/// Replace an existing document.
	/// </summary>
	Replace = 1,

	/// <summary>
	/// Upsert a document (create or replace).
	/// </summary>
	Upsert = 2,

	/// <summary>
	/// Delete a document.
	/// </summary>
	Delete = 3,

	/// <summary>
	/// Patch a document with partial updates.
	/// </summary>
	Patch = 4,

	/// <summary>
	/// Read a document (for transactional reads).
	/// </summary>
	Read = 5
}

/// <summary>
/// Provides cloud provider identity metadata for cloud-native persistence providers and event stores.
/// Obtain via <see cref="Excalibur.Data.Persistence.IPersistenceProvider.GetService"/> on an
/// <see cref="ICloudNativePersistenceProvider"/> instance, or via
/// <see cref="ICloudNativeEventStore.GetService"/> on an event store instance.
/// </summary>
public interface ICloudNativeProviderInfo
{
	/// <summary>
	/// Gets the cloud provider type.
	/// </summary>
	CloudPersistenceProviderType CloudProvider { get; }
}

/// <summary>
/// Defines core persistence operations for cloud-native document databases.
/// </summary>
/// <remarks>
/// <para>
/// This interface extends <see cref="IDocumentPersistenceProvider"/> with cloud-native
/// specific capabilities including partition key management and configurable consistency levels.
/// </para>
/// <para>
/// Advanced features are available as ISP sub-interfaces via
/// <see cref="Excalibur.Data.Persistence.IPersistenceProvider.GetService"/>:
/// <list type="bullet">
/// <item><see cref="ICloudNativeProviderInfo"/> -- cloud provider type metadata</item>
/// <item><see cref="ICloudNativePersistenceQueryOperations"/> -- partition-scoped queries</item>
/// <item><see cref="ICloudNativePersistenceBatchOperations"/> -- transactional batch execution</item>
/// <item><see cref="ICloudNativePersistenceChangeFeed"/> -- change feed subscriptions and capability flags</item>
/// </list>
/// </para>
/// <para>
/// <strong>Supported Providers:</strong>
/// <list type="bullet">
/// <item>Azure Cosmos DB</item>
/// <item>AWS DynamoDB</item>
/// <item>Google Cloud Firestore</item>
/// </list>
/// </para>
/// </remarks>
public interface ICloudNativePersistenceProvider : IDocumentPersistenceProvider
{
	/// <summary>
	/// Gets a document by ID with partition key and consistency options.
	/// </summary>
	/// <typeparam name="TDocument">The document type.</typeparam>
	/// <param name="id">The document ID.</param>
	/// <param name="partitionKey">The partition key for the document.</param>
	/// <param name="consistencyOptions">Consistency options for the read.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The document if found, null otherwise.</returns>
	Task<TDocument?> GetByIdAsync<TDocument>(
		string id,
		IPartitionKey partitionKey,
		IConsistencyOptions? consistencyOptions,
		CancellationToken cancellationToken)
		where TDocument : class;

	/// <summary>
	/// Creates a new document with partition key.
	/// </summary>
	/// <typeparam name="TDocument">The document type.</typeparam>
	/// <param name="document">The document to create.</param>
	/// <param name="partitionKey">The partition key for the document.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The operation result with cost information.</returns>
	Task<CloudOperationResult<TDocument>> CreateAsync<TDocument>(
		TDocument document,
		IPartitionKey partitionKey,
		CancellationToken cancellationToken)
		where TDocument : class;

	/// <summary>
	/// Updates a document with optimistic concurrency using ETag.
	/// </summary>
	/// <typeparam name="TDocument">The document type.</typeparam>
	/// <param name="document">The document to update.</param>
	/// <param name="partitionKey">The partition key for the document.</param>
	/// <param name="etag">The ETag for optimistic concurrency (null to skip check).</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The operation result with new ETag and cost information.</returns>
	Task<CloudOperationResult<TDocument>> UpdateAsync<TDocument>(
		TDocument document,
		IPartitionKey partitionKey,
		string? etag,
		CancellationToken cancellationToken)
		where TDocument : class;

	/// <summary>
	/// Deletes a document with optimistic concurrency using ETag.
	/// </summary>
	/// <param name="id">The document ID to delete.</param>
	/// <param name="partitionKey">The partition key for the document.</param>
	/// <param name="etag">The ETag for optimistic concurrency (null to skip check).</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The operation result with cost information.</returns>
	Task<CloudOperationResult> DeleteAsync(
		string id,
		IPartitionKey partitionKey,
		string? etag,
		CancellationToken cancellationToken);
}

/// <summary>
/// Provides partition-scoped query capabilities for cloud-native document databases.
/// Obtain via <see cref="Excalibur.Data.Persistence.IPersistenceProvider.GetService"/> on an
/// <see cref="ICloudNativePersistenceProvider"/> instance.
/// </summary>
public interface ICloudNativePersistenceQueryOperations
{
	/// <summary>
	/// Queries one page of documents within a partition.
	/// </summary>
	/// <typeparam name="TDocument">The document type.</typeparam>
	/// <param name="query">The query to run, including the continuation token that resumes a previous page.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The query results with cost information, and the continuation token for the next page.</returns>
	/// <remarks>
	/// <para>
	/// This returns a <b>single page</b>. When
	/// <see cref="CloudQueryResult{TDocument}.HasMoreResults"/> is <see langword="true"/> the result set is
	/// incomplete; copy <see cref="CloudQueryResult{TDocument}.ContinuationToken"/> into
	/// <see cref="CloudQueryRequest.ContinuationToken"/> and call again to obtain the next page.
	/// </para>
	/// <para>
	/// Paging is server-side and happens whether or not the caller models it, so a caller that reads one
	/// page and stops silently processes part of its data. Loop until
	/// <see cref="CloudQueryResult{TDocument}.HasMoreResults"/> is <see langword="false"/>.
	/// </para>
	/// <para>
	/// A continuation token is opaque and belongs to the provider that issued it. Passing one to a
	/// different provider is rejected rather than silently misread — a token interpreted as a position it
	/// does not name resumes from nowhere and returns an empty page that looks like the end of the data.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentException">
	/// <see cref="CloudQueryRequest.ContinuationToken"/> was supplied and was not issued by this provider.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <see cref="CloudQueryRequest.MaxItemCount"/> was supplied and is not greater than zero.
	/// </exception>
	Task<CloudQueryResult<TDocument>> QueryAsync<TDocument>(
		CloudQueryRequest query,
		CancellationToken cancellationToken)
		where TDocument : class;
}

/// <summary>
/// Provides transactional batch operation capabilities for cloud-native document databases.
/// Obtain via <see cref="Excalibur.Data.Persistence.IPersistenceProvider.GetService"/> on an
/// <see cref="ICloudNativePersistenceProvider"/> instance.
/// </summary>
public interface ICloudNativePersistenceBatchOperations
{
	/// <summary>
	/// Executes a transactional batch of operations within a partition.
	/// </summary>
	/// <param name="partitionKey">The partition key for all operations.</param>
	/// <param name="operations">The operations to execute.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The batch result with cost information.</returns>
	Task<CloudBatchResult> ExecuteBatchAsync(
		IPartitionKey partitionKey,
		IEnumerable<ICloudBatchOperation> operations,
		CancellationToken cancellationToken);
}

/// <summary>
/// Provides change feed subscription capabilities and advanced capability flags
/// for cloud-native document databases. Obtain via
/// <see cref="Excalibur.Data.Persistence.IPersistenceProvider.GetService"/> on an
/// <see cref="ICloudNativePersistenceProvider"/> instance.
/// </summary>
public interface ICloudNativePersistenceChangeFeed
{
	/// <summary>
	/// Gets a value indicating whether the provider supports change feed.
	/// </summary>
	bool SupportsChangeFeed { get; }

	/// <summary>
	/// Gets a value indicating whether the provider supports multi-region writes.
	/// </summary>
	bool SupportsMultiRegionWrites { get; }

	/// <summary>
	/// Creates a change feed subscription for real-time updates.
	/// </summary>
	/// <typeparam name="TDocument">The document type.</typeparam>
	/// <param name="containerName">The container or collection name.</param>
	/// <param name="options">Change feed options.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A change feed subscription.</returns>
	Task<IChangeFeedSubscription<TDocument>> CreateChangeFeedSubscriptionAsync<TDocument>(
		string containerName,
		IChangeFeedOptions? options,
		CancellationToken cancellationToken)
		where TDocument : class;
}

/// <summary>
/// Represents a batch operation in a transactional batch.
/// </summary>
public interface ICloudBatchOperation
{
	/// <summary>
	/// Gets the operation type.
	/// </summary>
	CloudBatchOperationType OperationType { get; }

	/// <summary>
	/// Gets the document ID for the operation.
	/// </summary>
	string DocumentId { get; }
}

/// <summary>
/// Represents the result of a cloud-native database operation.
/// </summary>
public class CloudOperationResult
{
	/// <summary>
	/// Initializes a new instance of the <see cref="CloudOperationResult"/> class.
	/// </summary>
	/// <param name="success">Whether the operation succeeded.</param>
	/// <param name="statusCode">The HTTP status code.</param>
	/// <param name="requestCharge">The request charge (RUs for Cosmos, WCUs for DynamoDB).</param>
	/// <param name="etag">The new ETag after the operation.</param>
	/// <param name="sessionToken">The session token for session consistency.</param>
	/// <param name="errorMessage">Error message if the operation failed.</param>
	public CloudOperationResult(
		bool success,
		int statusCode,
		double requestCharge,
		string? etag = null,
		string? sessionToken = null,
		string? errorMessage = null)
	{
		Success = success;
		StatusCode = statusCode;
		RequestCharge = requestCharge;
		ETag = etag;
		SessionToken = sessionToken;
		ErrorMessage = errorMessage;
	}

	/// <summary>
	/// Gets a value indicating whether the operation succeeded.
	/// </summary>
	public bool Success { get; }

	/// <summary>
	/// Gets the HTTP status code.
	/// </summary>
	public int StatusCode { get; }

	/// <summary>
	/// Gets the request charge (RUs for Cosmos DB, WCUs for DynamoDB).
	/// </summary>
	public double RequestCharge { get; }

	/// <summary>
	/// Gets the ETag for optimistic concurrency.
	/// </summary>
	public string? ETag { get; }

	/// <summary>
	/// Gets the session token for session consistency.
	/// </summary>
	public string? SessionToken { get; }

	/// <summary>
	/// Gets the error message if the operation failed.
	/// </summary>
	public string? ErrorMessage { get; }

	/// <summary>
	/// Gets a value indicating whether the failure was due to a concurrency conflict (412).
	/// </summary>
	public bool IsConcurrencyConflict => StatusCode == 412;

	/// <summary>
	/// Gets a value indicating whether the item was not found (404).
	/// </summary>
	public bool IsNotFound => StatusCode == 404;
}

/// <summary>
/// Represents the result of a cloud-native database operation with a document.
/// </summary>
/// <typeparam name="TDocument">The document type.</typeparam>
public class CloudOperationResult<TDocument> : CloudOperationResult
	where TDocument : class
{
	/// <summary>
	/// Initializes a new instance of the <see cref="CloudOperationResult{TDocument}"/> class.
	/// </summary>
	public CloudOperationResult(
		bool success,
		int statusCode,
		double requestCharge,
		TDocument? document = null,
		string? etag = null,
		string? sessionToken = null,
		string? errorMessage = null)
		: base(success, statusCode, requestCharge, etag, sessionToken, errorMessage)
	{
		Document = document;
	}

	/// <summary>
	/// Gets the document returned by the operation.
	/// </summary>
	public TDocument? Document { get; }
}

/// <summary>
/// Describes one page of a partition-scoped cloud-native query.
/// </summary>
/// <remarks>
/// <para>
/// This type exists so that a query can be <i>resumed</i>. The provider returns a
/// <see cref="CloudQueryResult{TDocument}.ContinuationToken"/> when more results remain, and that token is
/// handed back through <see cref="ContinuationToken"/> to fetch the next page — the same shape the
/// underlying vendor SDKs use.
/// </para>
/// <para>
/// It also keeps the query surface to two parameters. Passing the query, partition key, parameters,
/// consistency options and continuation positionally would exceed the contract's parameter budget and make
/// every future addition another breaking change.
/// </para>
/// </remarks>
public sealed class CloudQueryRequest
{
	/// <summary>
	/// Gets the query text, in the provider's own query syntax.
	/// </summary>
	/// <value>The provider-specific query text.</value>
	public required string QueryText { get; init; }

	/// <summary>
	/// Gets the partition to query within.
	/// </summary>
	/// <value>The partition key that scopes the query.</value>
	public required IPartitionKey PartitionKey { get; init; }

	/// <summary>
	/// Gets the query parameters, if the query text is parameterized.
	/// </summary>
	/// <value>The parameter values, or <see langword="null"/> when the query takes none.</value>
	public IDictionary<string, object>? Parameters { get; init; }

	/// <summary>
	/// Gets the consistency options for this query.
	/// </summary>
	/// <value>The consistency options, or <see langword="null"/> to use the provider's default.</value>
	public IConsistencyOptions? ConsistencyOptions { get; init; }

	/// <summary>
	/// Gets the maximum number of documents to return in this page.
	/// </summary>
	/// <value>
	/// The page size, or <see langword="null"/> to let the provider choose.
	/// </value>
	/// <remarks>
	/// <para>
	/// This bounds how much the caller must hold in memory at once. It does not bound the result set: when
	/// a page is capped, the remainder is reached through
	/// <see cref="CloudQueryResult{TDocument}.ContinuationToken"/>, so a caller that keeps paging still
	/// sees every document.
	/// </para>
	/// <para>
	/// Every provider honours it. An earlier revision of this contract allowed a provider to refuse a page
	/// size instead, which made a caller bounding its own memory work against some providers and throw
	/// against others — the caller could not write one correct loop, and there was no way to ask in advance
	/// which it had.
	/// </para>
	/// </remarks>
	public int? MaxItemCount { get; init; }

	/// <summary>
	/// Gets the continuation token that resumes a previous page.
	/// </summary>
	/// <value>
	/// A token taken from a prior <see cref="CloudQueryResult{TDocument}.ContinuationToken"/>, or
	/// <see langword="null"/> to start from the first page.
	/// </value>
	/// <remarks>
	/// The token is <b>opaque</b> and is only meaningful to the provider that issued it. A provider given a
	/// token it did not issue throws <see cref="ArgumentException"/> — it never ignores one, because
	/// silently restarting at page one would return the first page forever while a paging loop appeared to
	/// make progress.
	/// </remarks>
	public string? ContinuationToken { get; init; }
}

/// <summary>
/// Represents the result of a cloud-native query operation.
/// </summary>
/// <typeparam name="TDocument">The document type.</typeparam>
public sealed class CloudQueryResult<TDocument>
	where TDocument : class
{
	/// <summary>
	/// Initializes a new instance of the <see cref="CloudQueryResult{TDocument}"/> class.
	/// </summary>
	public CloudQueryResult(
		IReadOnlyList<TDocument> documents,
		double requestCharge,
		string? continuationToken = null,
		string? sessionToken = null)
	{
		Documents = documents;
		RequestCharge = requestCharge;
		ContinuationToken = continuationToken;
		SessionToken = sessionToken;
	}

	/// <summary>
	/// Gets the query result documents.
	/// </summary>
	public IReadOnlyList<TDocument> Documents { get; }

	/// <summary>
	/// Gets the total request charge for the query.
	/// </summary>
	public double RequestCharge { get; }

	/// <summary>
	/// Gets the continuation token for pagination.
	/// </summary>
	public string? ContinuationToken { get; }

	/// <summary>
	/// Gets the session token for session consistency.
	/// </summary>
	public string? SessionToken { get; }

	/// <summary>
	/// Gets a value indicating whether there are more results.
	/// </summary>
	public bool HasMoreResults => ContinuationToken != null;
}

/// <summary>
/// Represents the result of a batch operation.
/// </summary>
public sealed class CloudBatchResult
{
	/// <summary>
	/// Initializes a new instance of the <see cref="CloudBatchResult"/> class.
	/// </summary>
	public CloudBatchResult(
		bool success,
		double requestCharge,
		IReadOnlyList<CloudOperationResult> operationResults,
		string? sessionToken = null,
		string? errorMessage = null)
	{
		Success = success;
		RequestCharge = requestCharge;
		OperationResults = operationResults;
		SessionToken = sessionToken;
		ErrorMessage = errorMessage;
	}

	/// <summary>
	/// Gets a value indicating whether all operations succeeded.
	/// </summary>
	public bool Success { get; }

	/// <summary>
	/// Gets the total request charge for the batch.
	/// </summary>
	public double RequestCharge { get; }

	/// <summary>
	/// Gets the results for each operation in the batch.
	/// </summary>
	public IReadOnlyList<CloudOperationResult> OperationResults { get; }

	/// <summary>
	/// Gets the session token for session consistency.
	/// </summary>
	public string? SessionToken { get; }

	/// <summary>
	/// Gets the error message if the batch failed.
	/// </summary>
	public string? ErrorMessage { get; }
}
