// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Abstractions.Persistence;

namespace Excalibur.Data.Abstractions.CloudNative;

/// <summary>
/// Defines the cloud provider types supported by the framework.
/// </summary>
public enum CloudProviderType
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
/// Defines persistence operations specific to cloud-native document databases.
/// </summary>
/// <remarks>
/// <para>
/// This interface extends <see cref="IDocumentPersistenceProvider"/> with cloud-native
/// specific capabilities including:
/// <list type="bullet">
/// <item>Partition key management for data distribution</item>
/// <item>Configurable consistency levels per operation</item>
/// <item>Change feed subscriptions for real-time updates</item>
/// <item>Cost-aware operations (RU tracking, capacity hints)</item>
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
	/// Gets the cloud provider type.
	/// </summary>
	CloudProviderType CloudProvider { get; }

	/// <summary>
	/// Gets a value indicating whether the provider supports multi-region writes.
	/// </summary>
	bool SupportsMultiRegionWrites { get; }

	/// <summary>
	/// Gets a value indicating whether the provider supports change feed.
	/// </summary>
	bool SupportsChangeFeed { get; }

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

	/// <summary>
	/// Queries documents within a partition.
	/// </summary>
	/// <typeparam name="TDocument">The document type.</typeparam>
	/// <param name="queryText">The query text (provider-specific syntax).</param>
	/// <param name="partitionKey">The partition key to query within.</param>
	/// <param name="parameters">Query parameters.</param>
	/// <param name="consistencyOptions">Consistency options for the query.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The query results with cost information.</returns>
	Task<CloudQueryResult<TDocument>> QueryAsync<TDocument>(
		string queryText,
		IPartitionKey partitionKey,
		IDictionary<string, object>? parameters,
		IConsistencyOptions? consistencyOptions,
		CancellationToken cancellationToken)
		where TDocument : class;

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
