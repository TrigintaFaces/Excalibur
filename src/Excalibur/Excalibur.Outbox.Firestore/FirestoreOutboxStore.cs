// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;

using Excalibur.Data.Abstractions.CloudNative;
using Excalibur.Data.Abstractions.Observability;
using Excalibur.Data.Firestore;
using Excalibur.Dispatch.Abstractions.Diagnostics;

using Google.Api.Gax;
using Google.Cloud.Firestore;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Outbox.Firestore;

/// <summary>
/// Google Cloud Firestore implementation of the cloud-native outbox store.
/// </summary>
public sealed partial class FirestoreOutboxStore : ICloudNativeOutboxStore, IAsyncDisposable
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = false
	};

	private readonly FirestoreOutboxOptions _options;
	private readonly ILogger<FirestoreOutboxStore> _logger;
	private readonly SemaphoreSlim _initLock = new(1, 1);

	private FirestoreDb? _db;
	private CollectionReference? _collection;
	private bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreOutboxStore"/> class.
	/// </summary>
	/// <param name="options">The Firestore outbox options.</param>
	/// <param name="logger">The logger instance.</param>
	public FirestoreOutboxStore(
		IOptions<FirestoreOutboxOptions> options,
		ILogger<FirestoreOutboxStore> logger)
	{
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_options.Validate();
	}

	/// <inheritdoc/>
	public CloudProviderType ProviderType => CloudProviderType.Firestore;

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

			LogInitializing(_options.CollectionName);

			_db = await CreateDatabaseAsync(cancellationToken).ConfigureAwait(false);
			_collection = _db.Collection(_options.CollectionName);
			_initialized = true;
		}
		finally
		{
			_ = _initLock.Release();
		}
	}

	/// <inheritdoc/>
	public async Task<CloudOperationResult<CloudOutboxMessage>> AddAsync(
		CloudOutboxMessage message,
		IPartitionKey partitionKey,
		CancellationToken cancellationToken)
	{
		EnsureInitialized();

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		var docData = ToFirestoreDocument(message, partitionKey);
		var docRef = _collection.Document(message.MessageId);

		try
		{
			_ = await docRef.SetAsync(docData, cancellationToken: cancellationToken).ConfigureAwait(false);

			LogOperationCompleted("Add");

			return new CloudOperationResult<CloudOutboxMessage>(
				success: true,
				statusCode: 200,
				requestCharge: 1,
				document: message);
		}
		catch (Exception ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.Firestore,
				"add",
				message.MessageId,
				message.CorrelationId,
				message.CausationId);
			LogOperationFailed("Add", ex.Message, ex);
			return new CloudOperationResult<CloudOutboxMessage>(
				success: false,
				statusCode: 500,
				requestCharge: 0,
				errorMessage: ex.Message);
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.Firestore,
				"add",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public async Task<CloudBatchResult> AddBatchAsync(
		IEnumerable<CloudOutboxMessage> messages,
		IPartitionKey partitionKey,
		CancellationToken cancellationToken)
	{
		EnsureInitialized();

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		var messageList = messages.ToList();
		var operationResults = new List<CloudOperationResult>();

		try
		{
			// Firestore batch limit is 500
			var batches = messageList
				.Select((msg, idx) => new { msg, idx })
				.GroupBy(x => x.idx / _options.MaxBatchSize)
				.Select(g => g.Select(x => x.msg).ToList());

			foreach (var batch in batches)
			{
				var writeBatch = _db.StartBatch();

				foreach (var message in batch)
				{
					var docData = ToFirestoreDocument(message, partitionKey);
					var docRef = _collection.Document(message.MessageId);
					_ = writeBatch.Set(docRef, docData);
				}

				_ = await writeBatch.CommitAsync(cancellationToken).ConfigureAwait(false);

				operationResults.AddRange(batch.Select(_ => new CloudOperationResult(
					success: true,
					statusCode: 200,
					requestCharge: 1)));
			}

			LogOperationCompleted("AddBatch");

			return new CloudBatchResult(
				success: true,
				requestCharge: messageList.Count,
				operationResults: operationResults);
		}
		catch (Exception ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.Firestore,
				"add_batch");
			LogOperationFailed("AddBatch", ex.Message, ex);
			return new CloudBatchResult(
				success: false,
				requestCharge: 0,
				operationResults: [],
				errorMessage: ex.Message);
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.Firestore,
				"add_batch",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public async Task<CloudQueryResult<CloudOutboxMessage>> GetPendingAsync(
		IPartitionKey partitionKey,
		int batchSize,
		CancellationToken cancellationToken)
	{
		EnsureInitialized();

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		try
		{
			var query = _collection
				.WhereEqualTo("partitionKey", partitionKey.Value)
				.WhereEqualTo("isPublished", false)
				.Limit(batchSize);

			var snapshot = await query.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
			var messages = snapshot.Documents.Select(FromFirestoreDocument).ToList();

			LogOperationCompleted("GetPending");

			string? continuationToken = null;
			if (snapshot.Documents.Count == batchSize && snapshot.Documents.Count > 0)
			{
				var lastDoc = snapshot.Documents[^1];
				continuationToken = lastDoc.Id;
			}

			return new CloudQueryResult<CloudOutboxMessage>(messages, snapshot.Documents.Count, continuationToken);
		}
		catch (Exception ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.Firestore,
				"get_pending");
			LogOperationFailed("GetPending", ex.Message, ex);
			return new CloudQueryResult<CloudOutboxMessage>([], 0);
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.Firestore,
				"get_pending",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public async Task<CloudOperationResult> MarkAsPublishedAsync(
		string messageId,
		IPartitionKey partitionKey,
		CancellationToken cancellationToken)
	{
		EnsureInitialized();

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		var publishedAt = DateTimeOffset.UtcNow;
		var ttlTimestamp = _options.DefaultTimeToLiveSeconds > 0
			? Timestamp.FromDateTimeOffset(publishedAt.AddSeconds(_options.DefaultTimeToLiveSeconds))
			: (Timestamp?)null;

		try
		{
			var docRef = _collection.Document(messageId);
			var updates = new Dictionary<string, object> { ["isPublished"] = true, ["publishedAt"] = publishedAt.ToString("o") };

			if (ttlTimestamp.HasValue)
			{
				updates["expireAt"] = ttlTimestamp.Value;
			}

			_ = await docRef.UpdateAsync(updates, cancellationToken: cancellationToken).ConfigureAwait(false);

			LogOperationCompleted("MarkAsPublished");

			return new CloudOperationResult(
				success: true,
				statusCode: 200,
				requestCharge: 1);
		}
		catch (Exception ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.Firestore,
				"mark_published",
				messageId);
			LogOperationFailed("MarkAsPublished", ex.Message, ex);
			return new CloudOperationResult(
				success: false,
				statusCode: 500,
				requestCharge: 0,
				errorMessage: ex.Message);
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.Firestore,
				"mark_published",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public async Task<CloudBatchResult> MarkBatchAsPublishedAsync(
		IEnumerable<string> messageIds,
		IPartitionKey partitionKey,
		CancellationToken cancellationToken)
	{
		EnsureInitialized();

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		var messageIdList = messageIds.ToList();
		var publishedAt = DateTimeOffset.UtcNow;
		var ttlTimestamp = _options.DefaultTimeToLiveSeconds > 0
			? Timestamp.FromDateTimeOffset(publishedAt.AddSeconds(_options.DefaultTimeToLiveSeconds))
			: (Timestamp?)null;

		try
		{
			var batches = messageIdList
				.Select((id, idx) => new { id, idx })
				.GroupBy(x => x.idx / _options.MaxBatchSize)
				.Select(g => g.Select(x => x.id).ToList());

			var operationResults = new List<CloudOperationResult>();

			foreach (var batch in batches)
			{
				var writeBatch = _db.StartBatch();

				foreach (var messageId in batch)
				{
					var docRef = _collection.Document(messageId);
					var updates = new Dictionary<string, object> { ["isPublished"] = true, ["publishedAt"] = publishedAt.ToString("o") };

					if (ttlTimestamp.HasValue)
					{
						updates["expireAt"] = ttlTimestamp.Value;
					}

					_ = writeBatch.Update(docRef, updates);
				}

				_ = await writeBatch.CommitAsync(cancellationToken).ConfigureAwait(false);

				operationResults.AddRange(batch.Select(_ => new CloudOperationResult(
					success: true,
					statusCode: 200,
					requestCharge: 1)));
			}

			LogOperationCompleted("MarkBatchAsPublished");

			return new CloudBatchResult(
				success: true,
				requestCharge: messageIdList.Count,
				operationResults: operationResults);
		}
		catch (Exception ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.Firestore,
				"mark_batch_published");
			LogOperationFailed("MarkBatchAsPublished", ex.Message, ex);
			return new CloudBatchResult(
				success: false,
				requestCharge: 0,
				operationResults: [],
				errorMessage: ex.Message);
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.Firestore,
				"mark_batch_published",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public async Task<CloudCleanupResult> CleanupOldMessagesAsync(
		IPartitionKey partitionKey,
		TimeSpan retentionPeriod,
		CancellationToken cancellationToken)
	{
		EnsureInitialized();

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		var cutoffDate = DateTimeOffset.UtcNow.Subtract(retentionPeriod);
		var deletedCount = 0;

		try
		{
			var query = _collection
				.WhereEqualTo("partitionKey", partitionKey.Value)
				.WhereEqualTo("isPublished", true)
				.WhereLessThan("publishedAt", cutoffDate.ToString("o"));

			var snapshot = await query.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

			var batches = snapshot.Documents
				.Select((doc, idx) => new { doc, idx })
				.GroupBy(x => x.idx / _options.MaxBatchSize)
				.Select(g => g.Select(x => x.doc).ToList());

			foreach (var batch in batches)
			{
				var writeBatch = _db.StartBatch();

				foreach (var doc in batch)
				{
					_ = writeBatch.Delete(doc.Reference);
					deletedCount++;
				}

				_ = await writeBatch.CommitAsync(cancellationToken).ConfigureAwait(false);
			}

			LogOperationCompleted("CleanupOldMessages");
		}
		catch (Exception ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.Firestore,
				"cleanup_old");
			LogOperationFailed("CleanupOldMessages", ex.Message, ex);
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.Firestore,
				"cleanup_old",
				result,
				stopwatch.Elapsed);
		}

		return new CloudCleanupResult(deletedCount, deletedCount);
	}

	/// <inheritdoc/>
	public async Task<IChangeFeedSubscription<CloudOutboxMessage>> SubscribeToNewMessagesAsync(
		IChangeFeedOptions? options,
		CancellationToken cancellationToken)
	{
		EnsureInitialized();

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;

		try
		{
			var subscription = new FirestoreOutboxListenerSubscription(
				_db,
				_options,
				_logger);

			await subscription.StartAsync(cancellationToken).ConfigureAwait(false);
			return subscription;
		}
		catch (Exception)
		{
			result = WriteStoreTelemetry.Results.Failure;
			throw;
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.Firestore,
				"subscribe_new",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public async Task<CloudOperationResult> IncrementRetryCountAsync(
		string messageId,
		IPartitionKey partitionKey,
		string? errorMessage,
		CancellationToken cancellationToken)
	{
		EnsureInitialized();

		var stopwatch = ValueStopwatch.StartNew();
		var result = WriteStoreTelemetry.Results.Success;
		try
		{
			var docRef = _collection.Document(messageId);
			var updates = new Dictionary<string, object> { ["retryCount"] = FieldValue.Increment(1) };

			if (!string.IsNullOrEmpty(errorMessage))
			{
				updates["lastError"] = errorMessage;
			}

			_ = await docRef.UpdateAsync(updates, cancellationToken: cancellationToken).ConfigureAwait(false);

			LogOperationCompleted("IncrementRetryCount");

			return new CloudOperationResult(
				success: true,
				statusCode: 200,
				requestCharge: 1);
		}
		catch (Exception ex)
		{
			result = WriteStoreTelemetry.Results.Failure;
			using var scope = WriteStoreTelemetry.BeginLogScope(
				_logger,
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.Firestore,
				"increment_retry",
				messageId);
			LogOperationFailed("IncrementRetryCount", ex.Message, ex);
			return new CloudOperationResult(
				success: false,
				statusCode: 500,
				requestCharge: 0,
				errorMessage: ex.Message);
		}
		finally
		{
			WriteStoreTelemetry.RecordOperation(
				WriteStoreTelemetry.Stores.OutboxStore,
				WriteStoreTelemetry.Providers.Firestore,
				"increment_retry",
				result,
				stopwatch.Elapsed);
		}
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return ValueTask.CompletedTask;
		}

		_disposed = true;
		_initLock.Dispose();

		return ValueTask.CompletedTask;
	}

	private static Dictionary<string, object> ToFirestoreDocument(CloudOutboxMessage message, IPartitionKey partitionKey)
	{
		var doc = new Dictionary<string, object>
		{
			["messageId"] = message.MessageId,
			["partitionKey"] = partitionKey.Value,
			["messageType"] = message.MessageType,
			["payload"] = Convert.ToBase64String(message.Payload),
			["createdAt"] = message.CreatedAt.ToString("o"),
			["isPublished"] = message.IsPublished,
			["retryCount"] = message.RetryCount
		};

		if (message.Headers != null)
		{
			doc["headers"] = JsonSerializer.Serialize(message.Headers, JsonOptions);
		}

		if (!string.IsNullOrEmpty(message.AggregateId))
		{
			doc["aggregateId"] = message.AggregateId;
		}

		if (!string.IsNullOrEmpty(message.AggregateType))
		{
			doc["aggregateType"] = message.AggregateType;
		}

		if (!string.IsNullOrEmpty(message.CorrelationId))
		{
			doc["correlationId"] = message.CorrelationId;
		}

		if (!string.IsNullOrEmpty(message.CausationId))
		{
			doc["causationId"] = message.CausationId;
		}

		if (message.PublishedAt.HasValue)
		{
			doc["publishedAt"] = message.PublishedAt.Value.ToString("o");
		}

		if (!string.IsNullOrEmpty(message.LastError))
		{
			doc["lastError"] = message.LastError;
		}

		return doc;
	}

	private static CloudOutboxMessage FromFirestoreDocument(DocumentSnapshot doc)
	{
		return new CloudOutboxMessage
		{
			MessageId = doc.GetValue<string>("messageId"),
			MessageType = doc.GetValue<string>("messageType"),
			Payload = Convert.FromBase64String(doc.GetValue<string>("payload")),
			Headers = doc.ContainsField("headers") && doc.GetValue<string?>("headers") != null
				? JsonSerializer.Deserialize<Dictionary<string, string>>(doc.GetValue<string>("headers"), JsonOptions)
				: null,
			AggregateId = doc.ContainsField("aggregateId") ? doc.GetValue<string?>("aggregateId") : null,
			AggregateType = doc.ContainsField("aggregateType") ? doc.GetValue<string?>("aggregateType") : null,
			CorrelationId = doc.ContainsField("correlationId") ? doc.GetValue<string?>("correlationId") : null,
			CausationId = doc.ContainsField("causationId") ? doc.GetValue<string?>("causationId") : null,
			CreatedAt = DateTimeOffset.Parse(doc.GetValue<string>("createdAt"), CultureInfo.InvariantCulture),
			PublishedAt = doc.ContainsField("publishedAt") && doc.GetValue<string?>("publishedAt") != null
				? DateTimeOffset.Parse(doc.GetValue<string>("publishedAt"), CultureInfo.InvariantCulture)
				: null,
			RetryCount = doc.ContainsField("retryCount") ? doc.GetValue<int>("retryCount") : 0,
			LastError = doc.ContainsField("lastError") ? doc.GetValue<string?>("lastError") : null,
			PartitionKeyValue = doc.GetValue<string>("partitionKey")
		};
	}

	private async Task<FirestoreDb> CreateDatabaseAsync(CancellationToken cancellationToken)
	{
		var builder = new FirestoreDbBuilder { ProjectId = _options.ProjectId ?? "demo-project" };

		if (!string.IsNullOrWhiteSpace(_options.EmulatorHost))
		{
			builder.EmulatorDetection = EmulatorDetection.EmulatorOnly;
			_ = FirestoreEmulatorHelper.TryConfigureEmulatorHost(_options.EmulatorHost);
		}
		else if (!string.IsNullOrWhiteSpace(_options.CredentialsPath))
		{
			builder.CredentialsPath = _options.CredentialsPath;
		}
		else if (!string.IsNullOrWhiteSpace(_options.CredentialsJson))
		{
			builder.JsonCredentials = _options.CredentialsJson;
		}

		return await builder.BuildAsync(cancellationToken).ConfigureAwait(false);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void EnsureInitialized()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (!_initialized)
		{
			throw new InvalidOperationException(
				"Outbox store has not been initialized. Call InitializeAsync first.");
		}
	}
}
