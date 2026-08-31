// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;

using Excalibur.Data.CloudNative;
using Excalibur.Data.Persistence;
using Excalibur.Data.Resilience;

using Google.Cloud.Firestore;

using Grpc.Core;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Firestore;

/// <summary>
/// Google Cloud Firestore implementation of the cloud-native persistence provider.
/// </summary>
[SuppressMessage(
	"Maintainability",
	"CA1506:Avoid excessive class coupling",
	Justification = "Cloud persistence providers inherently couple with many SDK and abstraction types.")]
public sealed partial class FirestorePersistenceProvider : ICloudNativePersistenceProvider,
	ICloudNativeProviderInfo, ICloudNativePersistenceQueryOperations, ICloudNativePersistenceBatchOperations, ICloudNativePersistenceChangeFeed,
	IPersistenceProviderHealth, IPersistenceProviderConnection, IAsyncDisposable
{
	private readonly FirestoreOptions _options;
	private readonly ILogger<FirestorePersistenceProvider> _logger;
	private readonly SemaphoreSlim _initLock = new(1, 1);
	private FirestoreDb? _db;
	private volatile bool _initialized;
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

		Name = string.IsNullOrWhiteSpace(_options.Name) ? "firestore" : _options.Name;
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

		Name = string.IsNullOrWhiteSpace(_options.Name) ? "firestore" : _options.Name;
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
	public CloudPersistenceProviderType CloudProvider => CloudPersistenceProviderType.Firestore;

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
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var collectionPath = GetCollectionPath(partitionKey);
		var docRef = _db!.Collection(collectionPath).Document(id);

		try
		{
			var snapshot = await docRef.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

			LogOperationCompleted("GetById");

			if (!snapshot.Exists)
			{
				return null;
			}

#pragma warning disable IL2026, IL3050 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
			return DeserializeDocument<TDocument>(snapshot);
#pragma warning restore IL2026, IL3050
		}
		catch (Exception ex)
		{
			LogOperationFailed("GetById", ex.Message, ex);
			throw;
		}
	}

	/// <inheritdoc/>
	[UnconditionalSuppressMessage("Trimming", "IL2095:DynamicallyAccessedMembers on method type parameter do not match overridden type parameter", Justification = "Type constraint is propagated from the interface; callers provide concrete types.")]
	public async Task<CloudOperationResult<TDocument>> CreateAsync<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TDocument>(
		TDocument document,
		IPartitionKey partitionKey,
		CancellationToken cancellationToken)
		where TDocument : class
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var documentId = GetDocumentId(document);
		var collectionPath = GetCollectionPath(partitionKey);
		var docRef = _db!.Collection(collectionPath).Document(documentId);

		try
		{
#pragma warning disable IL2026, IL3050 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
			var data = SerializeDocument(document);
#pragma warning restore IL2026, IL3050
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
	[UnconditionalSuppressMessage("Trimming", "IL2095:DynamicallyAccessedMembers on method type parameter do not match overridden type parameter", Justification = "Type constraint is propagated from the interface; callers provide concrete types.")]
	public async Task<CloudOperationResult<TDocument>> UpdateAsync<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TDocument>(
		TDocument document,
		IPartitionKey partitionKey,
		string? etag,
		CancellationToken cancellationToken)
		where TDocument : class
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var documentId = GetDocumentId(document);
		var collectionPath = GetCollectionPath(partitionKey);
		var docRef = _db!.Collection(collectionPath).Document(documentId);

		try
		{
#pragma warning disable IL2026, IL3050 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
			var data = SerializeDocument(document);
#pragma warning restore IL2026, IL3050

			if (!string.IsNullOrEmpty(etag))
			{
				// Use transaction for optimistic concurrency
				var notFound = false;
				var versionMismatch = false;

				await _db!.RunTransactionAsync(async transaction =>
				{
					// Reset per attempt: Firestore re-runs this callback when the transaction conflicts, and a
					// verdict left over from a failed attempt would outlive the attempt that produced it,
					// reporting a not-found or an etag mismatch the retry disproved -- so a write that
					// succeeded is reported as a failure and the caller retries work that already landed.
					notFound = false;
					versionMismatch = false;

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
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var collectionPath = GetCollectionPath(partitionKey);
		var docRef = _db!.Collection(collectionPath).Document(id);

		try
		{
			if (!string.IsNullOrEmpty(etag))
			{
				var notFound = false;
				var versionMismatch = false;

				await _db!.RunTransactionAsync(async transaction =>
				{
					// Reset per attempt: Firestore re-runs this callback when the transaction conflicts, and a
					// verdict left over from a failed attempt would outlive the attempt that produced it,
					// reporting a not-found or an etag mismatch the retry disproved -- so a write that
					// succeeded is reported as a failure and the caller retries work that already landed.
					notFound = false;
					versionMismatch = false;

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
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var collectionPath = GetCollectionPath(partitionKey);
		var collectionRef = _db!.Collection(collectionPath);
		var documents = new List<TDocument>();

		try
		{
			Query query = collectionRef;

			var snapshot = await query.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

			foreach (var doc in snapshot.Documents)
			{
#pragma warning disable IL2026, IL3050 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
				var document = DeserializeDocument<TDocument>(doc);
#pragma warning restore IL2026, IL3050
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
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var operationsList = operations.ToList();
		if (operationsList.Count == 0)
		{
			return new CloudBatchResult(
				success: true,
				requestCharge: 0,
				operationResults: []);
		}

		var batch = _db!.StartBatch();
		var collectionPath = GetCollectionPath(partitionKey);

		foreach (var operation in operationsList)
		{
			var docRef = _db!.Collection(collectionPath).Document(operation.DocumentId);

			switch (operation.OperationType)
			{
				case CloudBatchOperationType.Create:
#pragma warning disable IL2026, IL3050 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
					_ = batch.Create(docRef, SerializeDocument(RequireDocument(operation)));
#pragma warning restore IL2026, IL3050
					break;

				case CloudBatchOperationType.Replace:
				case CloudBatchOperationType.Upsert:
#pragma warning disable IL2026, IL3050 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
					_ = batch.Set(docRef, SerializeDocument(RequireDocument(operation)));
#pragma warning restore IL2026, IL3050
					break;

				case CloudBatchOperationType.Delete:
					_ = batch.Delete(docRef);
					break;

				default:
					throw new NotSupportedException(
						$"A Firestore batch cannot perform a '{operation.OperationType}' operation. Supported operations are Create, Replace, Upsert and Delete.");
			}
		}

		try
		{
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
	[UnconditionalSuppressMessage("Trimming", "IL2095:DynamicallyAccessedMembers on method type parameter do not match overridden type parameter", Justification = "Type constraint is propagated from the interface; callers provide concrete types.")]
	public async Task<IChangeFeedSubscription<TDocument>> CreateChangeFeedSubscriptionAsync<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TDocument>(
		string containerName,
		IChangeFeedOptions? options,
		CancellationToken cancellationToken)
		where TDocument : class
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var subscription = new FirestoreListenerSubscription<TDocument>(
			_db!,
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
	public async Task<IDictionary<string, object>> GetDocumentStoreStatisticsAsync(
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var stats = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["Provider"] = "Firestore",
			["Name"] = Name,
			["IsAvailable"] = IsAvailable,
			["ProjectId"] = _options.ProjectId ?? "N/A"
		};

		return stats;
	}

	/// <inheritdoc/>
	public async Task<IDictionary<string, object>> GetCollectionInfoAsync(
		string collectionName,
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var info = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["CollectionName"] = collectionName,
			["Path"] = collectionName
		};

		try
		{
			var collectionRef = _db!.Collection(collectionName);
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
	public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken)
	{
		try
		{
			await InitializeAsync(cancellationToken).ConfigureAwait(false);

			// A single document read, not ListRootCollectionsAsync. Listing collections is an admin-surface
			// operation the Firestore emulator does not implement, so probing with it reports a reachable
			// emulator as unreachable -- and because this method swallows every exception into false, that
			// looked identical to a genuine connectivity failure. A get on a document that need not exist
			// round-trips through the same channel and is supported everywhere Firestore runs; a miss is a
			// successful answer, which is exactly what proves the connection.
			// The identifiers deliberately avoid the __x__ shape: Firestore RESERVES identifiers matching
			// __.*__ and rejects them, which this codebase already documents for its grant and activity-group
			// document ids. A probe named that way fails validation before any traffic leaves the process --
			// and this method would then report a perfectly reachable database as unreachable.
			_ = await _db!.Collection("excalibur-connectivity-probe").Document("probe")
				.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

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

		if (serviceType == typeof(IPersistenceProviderConnection))
		{
			return this;
		}

		// IPersistenceProviderTransaction is deliberately not offered, and is not implemented. That
		// capability's scope is ambient: created before the provider knows what will enrol in it, then
		// written through, and committed once. Firestore's atomicity comes from RunTransactionAsync,
		// which owns the control flow and may re-run the caller's callback on contention -- a shape an
		// ambient scope cannot express. Callers needing atomicity use ExecuteBatchAsync, which states
		// those constraints in its own signature rather than discovering them at commit.

		if (serviceType == typeof(ICloudNativePersistenceQueryOperations))
		{
			return this;
		}

		if (serviceType == typeof(ICloudNativePersistenceBatchOperations))
		{
			return this;
		}

		if (serviceType == typeof(ICloudNativePersistenceChangeFeed))
		{
			return this;
		}

		if (serviceType == typeof(ICloudNativeProviderInfo))
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

		// Do not block on _initLock.Wait() in sync Dispose -- use DisposeAsync for graceful cleanup.
		// Direct disposal is safe because _disposed flag prevents concurrent init.
		_initLock?.Dispose();
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
		// Use timeout to prevent indefinite hang during disposal
		using var disposeCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		try
		{
			await _initLock.WaitAsync(disposeCts.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
		{
			// Proceed with disposal even if lock acquisition times out
		}

		_initLock?.Dispose();
	}

	/// <summary>
	/// Returns the document a writing batch operation carries, or throws when the operation declares a write but
	/// supplies no payload -- a caller mistake that would otherwise be committed as an empty batch entry.
	/// </summary>
	private static object RequireDocument(ICloudBatchOperation operation) =>
		operation is ICloudBatchDocumentOperation documentOperation
			? documentOperation.Document
			: throw new ArgumentException(
				$"Batch operation '{operation.OperationType}' for document '{operation.DocumentId}' carries no document. "
				+ $"Use {nameof(CloudBatchCreateOperation)}, {nameof(CloudBatchReplaceOperation)} or {nameof(CloudBatchUpsertOperation)}, "
				+ $"or any {nameof(ICloudBatchDocumentOperation)} implementation.",
				nameof(operation));

	/// <summary>
	/// Projects a document onto the map shape Firestore writes.
	/// </summary>
	/// <remarks>
	/// Every value is converted to a CLR type the Firestore SDK has a converter for. Handing the SDK the
	/// <see cref="JsonElement"/> values a plain dictionary deserialization produces makes it throw
	/// "Unable to create converter for type System.Text.Json.JsonElement" on the first write of any document
	/// with a field, so the conversion is not cosmetic -- it is what makes a write possible at all.
	/// </remarks>
	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.SerializeToDocument<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.SerializeToDocument<TValue>(TValue, JsonSerializerOptions)")]
	private static Dictionary<string, object?> SerializeDocument<TDocument>(TDocument document)
	{
		using var json = JsonSerializer.SerializeToDocument(document);

		return json.RootElement.ValueKind == JsonValueKind.Object
			? ToFirestoreMap(json.RootElement)
			: [];
	}

	private static Dictionary<string, object?> ToFirestoreMap(JsonElement element)
	{
		var map = new Dictionary<string, object?>(StringComparer.Ordinal);

		foreach (var property in element.EnumerateObject())
		{
			map[property.Name] = ToFirestoreValue(property.Value);
		}

		return map;
	}

	private static object? ToFirestoreValue(JsonElement element) => element.ValueKind switch
	{
		JsonValueKind.Object => ToFirestoreMap(element),
		JsonValueKind.Array => element.EnumerateArray().Select(ToFirestoreValue).ToList(),
		JsonValueKind.String => element.GetString(),
		JsonValueKind.Number => element.TryGetInt64(out var integer) ? integer : element.GetDouble(),
		JsonValueKind.True => true,
		JsonValueKind.False => false,
		_ => null,
	};

	[return: MaybeNull]
	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
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
			// Point this client at the emulator directly. The process-wide FIRESTORE_EMULATOR_HOST
			// variable is first-write-wins, so routing through it lets a second store silently talk to
			// another store's emulator. Endpoint and EmulatorDetection.EmulatorOnly are mutually
			// exclusive -- setting both throws -- so an explicit endpoint with insecure credentials is
			// the combination that reaches an emulator per instance.
			return await new FirestoreDbBuilder
			{
				ProjectId = _options.ProjectId ?? "test-project",
				Endpoint = _options.EmulatorHost,
				ChannelCredentials = ChannelCredentials.Insecure,
			}.BuildAsync().ConfigureAwait(false);
		}

		FirestoreDbBuilder builder;

#pragma warning disable CS0618 // CredentialsPath/JsonCredentials are obsolete but replacements require significant refactoring
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
#pragma warning restore CS0618

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

	/// <summary>
	/// Initializes the provider if it is not already initialized, then returns.
	/// </summary>
	/// <remarks>
	/// Every operation drives this, so the provider is usable straight out of the container without the
	/// consumer calling <see cref="InitializeAsync(CancellationToken)"/> first. A connection failure surfaces the underlying
	/// Firestore error naming what is unreachable, and leaves the provider uninitialized so the next
	/// operation retries.
	/// </remarks>
	private async ValueTask EnsureInitializedAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (_initialized && _db != null)
		{
			return;
		}

		await InitializeAsync(cancellationToken).ConfigureAwait(false);
	}
}
