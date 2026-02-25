// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;

using Excalibur.Data.Abstractions;
using Excalibur.Data.Abstractions.CloudNative;
using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.Abstractions.Resilience;

using Google.Cloud.Firestore;

using Grpc.Core;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Firestore;

/// <summary>
/// Internal interface for create batch operations.
/// </summary>
internal interface IFirestoreBatchCreateOperation
{
	/// <summary>
	/// Gets the document to create.
	/// </summary>
	object Document { get; }
}

/// <summary>
/// Google Cloud Firestore implementation of the cloud-native persistence provider.
/// </summary>
[SuppressMessage(
	"Maintainability",
	"CA1506:Avoid excessive class coupling",
	Justification = "Cloud persistence providers inherently couple with many SDK and abstraction types.")]
public sealed partial class FirestorePersistenceProvider : ICloudNativePersistenceProvider, IPersistenceProviderHealth, IPersistenceProviderTransaction, IAsyncDisposable
{
	private readonly FirestoreOptions _options;
	private readonly ILogger<FirestorePersistenceProvider> _logger;
	private readonly SemaphoreSlim _initLock = new(1, 1);
	private FirestoreDb? _db;
	private bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestorePersistenceProvider"/> class.
	/// </summary>
	/// <param name="options">The Firestore options.</param>
	/// <param name="logger">The logger instance.</param>
	public FirestorePersistenceProvider(
		IOptions<FirestoreOptions> options,
		ILogger<FirestorePersistenceProvider> logger)
	{
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_options.Validate();

		Name = _options.Name;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestorePersistenceProvider"/> class with an existing Firestore database.
	/// </summary>
	/// <param name="db">The Firestore database.</param>
	/// <param name="options">The Firestore options.</param>
	/// <param name="logger">The logger instance.</param>
	public FirestorePersistenceProvider(
		FirestoreDb db,
		IOptions<FirestoreOptions> options,
		ILogger<FirestorePersistenceProvider> logger)
	{
		_db = db ?? throw new ArgumentNullException(nameof(db));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_initialized = true;

		Name = _options.Name;
	}

	/// <inheritdoc/>
	public string Name { get; }

	/// <inheritdoc/>
	public string ProviderType => "CloudNative";

	/// <inheritdoc/>
	public bool IsAvailable => _initialized && !_disposed && _db != null;

	/// <inheritdoc/>
	public string DocumentStoreType => "Firestore";

	/// <inheritdoc/>
	public CloudProviderType CloudProvider => CloudProviderType.Firestore;

	/// <inheritdoc/>
	public bool SupportsMultiRegionWrites => false;

	/// <inheritdoc/>
	public bool SupportsChangeFeed => true;

	/// <inheritdoc/>
	public string ConnectionString => $"projects/{_options.ProjectId}";

	/// <inheritdoc/>
	public IDataRequestRetryPolicy RetryPolicy => FirestoreRetryPolicy.Instance;

	/// <summary>
	/// Initializes the Firestore client.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	public async Task InitializeAsync(CancellationToken cancellationToken)
	{
		if (_initialized)
		{
			return;
		}

		await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (_initialized)
			{
				return;
			}

			LogInitializing(Name);

			_db = await CreateDatabaseAsync().ConfigureAwait(false);
			_initialized = true;
		}
		finally
		{
			_ = _initLock.Release();
		}
	}

	/// <inheritdoc/>
	public async Task<TDocument?> GetByIdAsync<TDocument>(
		string id,
		IPartitionKey partitionKey,
		IConsistencyOptions? consistencyOptions,
		CancellationToken cancellationToken)
		where TDocument : class
	{
		EnsureInitialized();

		var collectionPath = GetCollectionPath(partitionKey);
		var docRef = _db.Collection(collectionPath).Document(id);

		try
		{
			var snapshot = await docRef.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

			LogOperationCompleted("GetById");

			if (!snapshot.Exists)
			{
				return null;
			}

			return DeserializeDocument<TDocument>(snapshot);
		}
		catch (Exception ex)
		{
			LogOperationFailed("GetById", ex.Message, ex);
			throw;
		}
	}

	/// <inheritdoc/>
	public async Task<CloudOperationResult<TDocument>> CreateAsync<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TDocument>(
		TDocument document,
		IPartitionKey partitionKey,
		CancellationToken cancellationToken)
		where TDocument : class
	{
		EnsureInitialized();

		var documentId = GetDocumentId(document);
		var collectionPath = GetCollectionPath(partitionKey);
		var docRef = _db.Collection(collectionPath).Document(documentId);

		try
		{
			var data = SerializeDocument(document);
			_ = await docRef.CreateAsync(data, cancellationToken).ConfigureAwait(false);

			LogOperationCompleted("Create");
			return new CloudOperationResult<TDocument>(
				success: true,
				statusCode: 200,
				requestCharge: 0,
				document: document);
		}
		catch (RpcException ex) when (ex.StatusCode == StatusCode.AlreadyExists)
		{
			LogOperationFailed("Create", "Document already exists", ex);
			return new CloudOperationResult<TDocument>(
				success: false,
				statusCode: (int)HttpStatusCode.Conflict,
				requestCharge: 0,
				errorMessage: "Document already exists");
		}
		catch (Exception ex)
		{
			LogOperationFailed("Create", ex.Message, ex);
			return new CloudOperationResult<TDocument>(
				success: false,
				statusCode: 500,
				requestCharge: 0,
				errorMessage: ex.Message);
		}
	}

	/// <inheritdoc/>
	public async Task<CloudOperationResult<TDocument>> UpdateAsync<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TDocument>(
		TDocument document,
		IPartitionKey partitionKey,
		string? etag,
		CancellationToken cancellationToken)
		where TDocument : class
	{
		EnsureInitialized();

		var documentId = GetDocumentId(document);
		var collectionPath = GetCollectionPath(partitionKey);
		var docRef = _db.Collection(collectionPath).Document(documentId);

		try
		{
			var data = SerializeDocument(document);

			if (!string.IsNullOrEmpty(etag))
			{
				// Use transaction for optimistic concurrency
				var notFound = false;
				var versionMismatch = false;

				await _db.RunTransactionAsync(async transaction =>
				{
					var snapshot = await transaction.GetSnapshotAsync(docRef, cancellationToken)
						.ConfigureAwait(false);

					if (!snapshot.Exists)
					{
						notFound = true;
						return;
					}

					var currentETag = snapshot.UpdateTime?.ToDateTimeOffset().Ticks.ToString();
					if (currentETag != etag)
					{
						versionMismatch = true;
						return;
					}

					transaction.Set(docRef, data);
				}, cancellationToken: cancellationToken).ConfigureAwait(false);

				if (notFound)
				{
					return new CloudOperationResult<TDocument>(
						success: false,
						statusCode: (int)HttpStatusCode.NotFound,
						requestCharge: 0,
						errorMessage: "Document not found");
				}

				if (versionMismatch)
				{
					return new CloudOperationResult<TDocument>(
						success: false,
						statusCode: (int)HttpStatusCode.PreconditionFailed,
						requestCharge: 0,
						errorMessage: "Version mismatch");
				}
			}
			else
			{
				_ = await docRef.SetAsync(data, cancellationToken: cancellationToken).ConfigureAwait(false);
			}

			LogOperationCompleted("Update");
			return new CloudOperationResult<TDocument>(
				success: true,
				statusCode: 200,
				requestCharge: 0,
				document: document);
		}
		catch (Exception ex)
		{
			LogOperationFailed("Update", ex.Message, ex);
			return new CloudOperationResult<TDocument>(
				success: false,
				statusCode: 500,
				requestCharge: 0,
				errorMessage: ex.Message);
		}
	}

	/// <inheritdoc/>
	public async Task<CloudOperationResult> DeleteAsync(
		string id,
		IPartitionKey partitionKey,
		string? etag,
		CancellationToken cancellationToken)
	{
		EnsureInitialized();

		var collectionPath = GetCollectionPath(partitionKey);
		var docRef = _db.Collection(collectionPath).Document(id);

		try
		{
			if (!string.IsNullOrEmpty(etag))
			{
				var notFound = false;
				var versionMismatch = false;

				await _db.RunTransactionAsync(async transaction =>
				{
					var snapshot = await transaction.GetSnapshotAsync(docRef, cancellationToken)
						.ConfigureAwait(false);

					if (!snapshot.Exists)
					{
						notFound = true;
						return;
					}

					var currentETag = snapshot.UpdateTime?.ToDateTimeOffset().Ticks.ToString();
					if (currentETag != etag)
					{
						versionMismatch = true;
						return;
					}

					transaction.Delete(docRef);
				}, cancellationToken: cancellationToken).ConfigureAwait(false);

				if (notFound)
				{
					return new CloudOperationResult(
						success: false,
						statusCode: (int)HttpStatusCode.NotFound,
						requestCharge: 0,
						errorMessage: "Document not found");
				}

				if (versionMismatch)
				{
					return new CloudOperationResult(
						success: false,
						statusCode: (int)HttpStatusCode.PreconditionFailed,
						requestCharge: 0,
						errorMessage: "Version mismatch");
				}
			}
			else
			{
				_ = await docRef.DeleteAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
			}

			LogOperationCompleted("Delete");
			return new CloudOperationResult(
				success: true,
				statusCode: 200,
				requestCharge: 0);
		}
		catch (Exception ex)
		{
			LogOperationFailed("Delete", ex.Message, ex);
			return new CloudOperationResult(
				success: false,
				statusCode: 500,
				requestCharge: 0,
				errorMessage: ex.Message);
		}
	}

	/// <inheritdoc/>
	public async Task<CloudQueryResult<TDocument>> QueryAsync<TDocument>(
		string queryText,
		IPartitionKey partitionKey,
		IDictionary<string, object>? parameters,
		IConsistencyOptions? consistencyOptions,
		CancellationToken cancellationToken)
		where TDocument : class
	{
		EnsureInitialized();

		var collectionPath = GetCollectionPath(partitionKey);
		var collectionRef = _db.Collection(collectionPath);
		var documents = new List<TDocument>();

		try
		{
			Query query = collectionRef;

			var snapshot = await query.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

			foreach (var doc in snapshot.Documents)
			{
				var document = DeserializeDocument<TDocument>(doc);
				if (document != null)
				{
					documents.Add(document);
				}
			}

			LogOperationCompleted("Query");
			return new CloudQueryResult<TDocument>(documents, 0, null);
		}
		catch (Exception ex)
		{
			LogOperationFailed("Query", ex.Message, ex);
			return new CloudQueryResult<TDocument>(documents, 0, null);
		}
	}

	/// <inheritdoc/>
	public async Task<CloudBatchResult> ExecuteBatchAsync(
		IPartitionKey partitionKey,
		IEnumerable<ICloudBatchOperation> operations,
		CancellationToken cancellationToken)
	{
		EnsureInitialized();

		var operationsList = operations.ToList();
		if (operationsList.Count == 0)
		{
			return new CloudBatchResult(
				success: true,
				requestCharge: 0,
				operationResults: []);
		}

		try
		{
			var batch = _db.StartBatch();

			foreach (var operation in operationsList)
			{
				var collectionPath = GetCollectionPath(partitionKey);
				var docRef = _db.Collection(collectionPath).Document(operation.DocumentId);

				switch (operation.OperationType)
				{
					case CloudBatchOperationType.Create:
						if (operation is IFirestoreBatchCreateOperation createOp)
						{
							var data = SerializeDocument(createOp.Document);
							_ = batch.Create(docRef, data);
						}

						break;

					case CloudBatchOperationType.Replace:
					case CloudBatchOperationType.Upsert:
						if (operation is IFirestoreBatchReplaceOperation replaceOp)
						{
							var data = SerializeDocument(replaceOp.Document);
							_ = batch.Set(docRef, data);
						}

						break;

					case CloudBatchOperationType.Delete:
						_ = batch.Delete(docRef);
						break;

					case CloudBatchOperationType.Patch:
						break;

					case CloudBatchOperationType.Read:
						break;

					default:
						break;
				}
			}

			_ = await batch.CommitAsync(cancellationToken).ConfigureAwait(false);

			LogOperationCompleted("Batch");

			var operationResults = operationsList.Select(_ => new CloudOperationResult(
				success: true,
				statusCode: 200,
				requestCharge: 0)).ToList();

			return new CloudBatchResult(
				success: true,
				requestCharge: 0,
				operationResults: operationResults);
		}
		catch (Exception ex)
		{
			LogOperationFailed("Batch", ex.Message, ex);
			return new CloudBatchResult(
				success: false,
				requestCharge: 0,
				operationResults: [],
				errorMessage: ex.Message);
		}
	}

	/// <inheritdoc/>
	public async Task<IChangeFeedSubscription<TDocument>> CreateChangeFeedSubscriptionAsync<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TDocument>(
		string containerName,
		IChangeFeedOptions? options,
		CancellationToken cancellationToken)
		where TDocument : class
	{
		EnsureInitialized();

		var subscription = new FirestoreListenerSubscription<TDocument>(
			_db,
			containerName,
			options ?? ChangeFeedOptions.Default,
			_logger);

		await subscription.StartAsync(cancellationToken).ConfigureAwait(false);
		return subscription;
	}

	#region IDocumentPersistenceProvider Implementation

	/// <inheritdoc/>
	public Task<TResult> ExecuteDocumentAsync<TConnection, TResult>(
		IDocumentDataRequest<TConnection, TResult> documentRequest,
		CancellationToken cancellationToken)
	{
		throw new NotSupportedException(
			"Use cloud-native specific methods for Firestore operations.");
	}

	/// <inheritdoc/>
	public Task<TResult> ExecuteDocumentInTransactionAsync<TConnection, TResult>(
		IDocumentDataRequest<TConnection, TResult> documentRequest,
		ITransactionScope transactionScope,
		CancellationToken cancellationToken)
	{
		throw new NotSupportedException(
			"Use ExecuteBatchAsync for transactional operations in Firestore.");
	}

	/// <inheritdoc/>
	public Task<IEnumerable<object>> ExecuteDocumentBatchAsync<TConnection>(
		IEnumerable<IDocumentDataRequest<TConnection, object>> documentRequests,
		CancellationToken cancellationToken)
	{
		throw new NotSupportedException(
			"Use ExecuteBatchAsync for batch operations in Firestore.");
	}

	/// <inheritdoc/>
	public Task<TResult> ExecuteBulkDocumentAsync<TConnection, TResult>(
		IDocumentDataRequest<TConnection, TResult> bulkDocumentRequest,
		CancellationToken cancellationToken)
	{
		throw new NotSupportedException(
			"Use batch operations for bulk operations in Firestore.");
	}

	/// <inheritdoc/>
	public Task<TResult> ExecuteAggregationAsync<TConnection, TResult>(
		IDocumentDataRequest<TConnection, TResult> aggregationRequest,
		CancellationToken cancellationToken)
	{
		throw new NotSupportedException(
			"Firestore aggregation queries are limited. Use client-side aggregation.");
	}

	/// <inheritdoc/>
	public Task<string> ExecuteIndexOperationAsync<TConnection>(
		IDocumentDataRequest<TConnection, string> indexRequest,
		CancellationToken cancellationToken)
	{
		throw new NotSupportedException(
			"Index management in Firestore is done through the Firebase console.");
	}

	/// <inheritdoc/>
#pragma warning disable IDE0060 // Parameter required by interface contract
	public Task<IDictionary<string, object>> GetDocumentStoreStatisticsAsync(
		CancellationToken cancellationToken)
#pragma warning restore IDE0060
	{
		EnsureInitialized();

		var stats = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["Provider"] = "Firestore",
			["Name"] = Name,
			["IsAvailable"] = IsAvailable,
			["ProjectId"] = _options.ProjectId ?? "N/A"
		};

		return Task.FromResult<IDictionary<string, object>>(stats);
	}

	/// <inheritdoc/>
	public async Task<IDictionary<string, object>> GetCollectionInfoAsync(
		string collectionName,
		CancellationToken cancellationToken)
	{
		EnsureInitialized();

		var info = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["CollectionName"] = collectionName,
			["Path"] = collectionName
		};

		try
		{
			var collectionRef = _db.Collection(collectionName);
			var snapshot = await collectionRef.Limit(1).GetSnapshotAsync(cancellationToken)
				.ConfigureAwait(false);

			info["HasDocuments"] = snapshot.Count > 0;
		}
		catch (Exception ex)
		{
			info["Error"] = ex.Message;
		}

		return info;
	}

	/// <inheritdoc/>
	public bool ValidateDocumentRequest<TConnection, TResult>(
		IDocumentDataRequest<TConnection, TResult> documentRequest) =>
		documentRequest != null;

	/// <inheritdoc/>
	public IEnumerable<string> GetSupportedOperationTypes() =>
		["Create", "Read", "Update", "Delete", "Query", "Batch", "Realtime"];

	#endregion IDocumentPersistenceProvider Implementation

	#region IPersistenceProvider Implementation

	/// <inheritdoc/>
	public Task<TResult> ExecuteAsync<TConnection, TResult>(
		IDataRequest<TConnection, TResult> request,
		CancellationToken cancellationToken)
		where TConnection : IDisposable
	{
		throw new NotSupportedException(
			"Use cloud-native specific methods for Firestore operations.");
	}

	/// <inheritdoc/>
	public Task<TResult> ExecuteInTransactionAsync<TConnection, TResult>(
		IDataRequest<TConnection, TResult> request,
		ITransactionScope transactionScope,
		CancellationToken cancellationToken)
		where TConnection : IDisposable
	{
		throw new NotSupportedException(
			"Use ExecuteBatchAsync for transactional operations in Firestore.");
	}

	/// <inheritdoc/>
	public ITransactionScope CreateTransactionScope(
		System.Data.IsolationLevel isolationLevel = System.Data.IsolationLevel.ReadCommitted,
		TimeSpan? timeout = null)
	{
		throw new NotSupportedException(
			"Firestore uses RunTransactionAsync for transactions. Use ExecuteBatchAsync instead.");
	}

	/// <inheritdoc/>
	public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken)
	{
		try
		{
			await InitializeAsync(cancellationToken).ConfigureAwait(false);

			// Try to list collections to verify connectivity
			var collections = _db.ListRootCollectionsAsync();
			await foreach (var _ in collections.ConfigureAwait(false))
			{
				break;
			}

			return true;
		}
		catch
		{
			return false;
		}
	}

	/// <inheritdoc/>
	public async Task<IDictionary<string, object>> GetMetricsAsync(CancellationToken cancellationToken) =>
		await GetDocumentStoreStatisticsAsync(cancellationToken).ConfigureAwait(false);

	/// <inheritdoc/>
	public async Task InitializeAsync(
		IPersistenceOptions options,
		CancellationToken cancellationToken)
	{
		// Options parameter is intentionally unused - configuration comes from constructor
		_ = options;
		await InitializeAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public Task<TConnection> GetConnectionAsync<TConnection>(
		CancellationToken cancellationToken)
		where TConnection : IDisposable
	{
		throw new NotSupportedException(
			"Firestore does not use traditional connections.");
	}

	/// <inheritdoc/>
	public void ReturnConnection<TConnection>(TConnection connection)
		where TConnection : IDisposable
	{
		// Connection parameter intentionally unused - Firestore doesn't use traditional connections
		_ = connection;
	}

	/// <inheritdoc/>
	public async Task<bool> IsConnectionValidAsync<TConnection>(
		TConnection connection,
		CancellationToken cancellationToken)
		where TConnection : IDisposable
	{
		// Connection parameter intentionally unused - validity check uses TestConnectionAsync
		_ = connection;
		return await TestConnectionAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public bool ValidateRequest<TConnection, TResult>(
		IDataRequest<TConnection, TResult> request) =>
		request != null;

	/// <inheritdoc/>
	public Task<IDictionary<string, object>?> GetConnectionPoolStatsAsync(CancellationToken cancellationToken)
	{
		var stats = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["ConnectionMode"] = "gRPC",
			["IsInitialized"] = _initialized,
			["IsDisposed"] = _disposed
		};

		return Task.FromResult<IDictionary<string, object>?>(stats);
	}

	/// <inheritdoc/>
	public async Task InitializeAsync(
		IDictionary<string, object>? initializationParameters,
		CancellationToken cancellationToken)
	{
		// initializationParameters intentionally unused - configuration comes from constructor
		_ = initializationParameters;
		await InitializeAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		if (serviceType == typeof(IPersistenceProviderHealth))
		{
			return this;
		}

		if (serviceType == typeof(IPersistenceProviderTransaction))
		{
			return this;
		}

		return null;
	}

	#endregion IPersistenceProvider Implementation

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		LogDisposing(Name);

		// Acquire lock before disposing to ensure no concurrent init is in progress
		_initLock.Wait();
		_initLock.Dispose();
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		LogDisposing(Name);

		// Acquire lock before disposing to ensure no concurrent init is in progress
		await _initLock.WaitAsync().ConfigureAwait(false);
		_initLock.Dispose();
	}

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	private static Dictionary<string, object> SerializeDocument<TDocument>(TDocument document)
	{
		var json = JsonSerializer.Serialize(document);
		var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
		return dict ?? new Dictionary<string, object>();
	}

	[return: MaybeNull]
	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	private static TDocument DeserializeDocument<TDocument>(DocumentSnapshot snapshot)
		where TDocument : class
	{
		var dict = snapshot.ToDictionary();
		var json = JsonSerializer.Serialize(dict);
		return JsonSerializer.Deserialize<TDocument>(json);
	}

	private static string GetDocumentId<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TDocument>(
		TDocument document)
	{
		var idProperty = typeof(TDocument).GetProperty("Id");
		if (idProperty != null)
		{
			var value = idProperty.GetValue(document);
			if (value != null)
			{
				return value.ToString() ?? Guid.NewGuid().ToString();
			}
		}

		var docIdProperty = typeof(TDocument).GetProperty("DocumentId");
		if (docIdProperty != null)
		{
			var value = docIdProperty.GetValue(document);
			if (value != null)
			{
				return value.ToString() ?? Guid.NewGuid().ToString();
			}
		}

		return Guid.NewGuid().ToString();
	}

	private async Task<FirestoreDb> CreateDatabaseAsync()
	{
		// Check for emulator
		if (!string.IsNullOrWhiteSpace(_options.EmulatorHost))
		{
			_ = FirestoreEmulatorHelper.TryConfigureEmulatorHost(_options.EmulatorHost);
			return await FirestoreDb.CreateAsync(_options.ProjectId ?? "test-project").ConfigureAwait(false);
		}

		FirestoreDbBuilder builder;

		if (!string.IsNullOrWhiteSpace(_options.CredentialsJson))
		{
			builder = new FirestoreDbBuilder { ProjectId = _options.ProjectId, JsonCredentials = _options.CredentialsJson };
		}
		else if (!string.IsNullOrWhiteSpace(_options.CredentialsPath))
		{
			builder = new FirestoreDbBuilder { ProjectId = _options.ProjectId, CredentialsPath = _options.CredentialsPath };
		}
		else
		{
			builder = new FirestoreDbBuilder { ProjectId = _options.ProjectId };
		}

		return await builder.BuildAsync().ConfigureAwait(false);
	}

	private string GetCollectionPath(IPartitionKey partitionKey)
	{
		if (!string.IsNullOrWhiteSpace(_options.DefaultCollection))
		{
			return _options.DefaultCollection;
		}

		return partitionKey.Value;
	}

	private void EnsureInitialized()
	{
		if (!_initialized || _db == null)
		{
			throw new InvalidOperationException(
				$"Provider '{Name}' has not been initialized. Call InitializeAsync first.");
		}
	}
}

/// <summary>
/// Internal interface for replace batch operations.
/// </summary>
internal interface IFirestoreBatchReplaceOperation
{
	/// <summary>
	/// Gets the document to replace.
	/// </summary>
	object Document { get; }
}
