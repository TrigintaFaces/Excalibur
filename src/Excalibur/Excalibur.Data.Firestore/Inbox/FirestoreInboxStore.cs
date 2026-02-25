// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Firestore.Diagnostics;
using Excalibur.Dispatch.Abstractions;

using Google.Cloud.Firestore;

using Grpc.Core;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.Firestore.Inbox;

/// <summary>
/// Firestore-based implementation of <see cref="IInboxStore"/>.
/// </summary>
/// <remarks>
/// Uses CreateAsync for atomic first-writer-wins semantics.
/// Document path: {CollectionName}/{messageId}_{handlerType}
/// Catches RpcException with StatusCode.AlreadyExists for conflict detection.
/// </remarks>
public sealed partial class FirestoreInboxStore : IInboxStore, IAsyncDisposable
{
	private readonly FirestoreInboxOptions _options;
	private readonly ILogger<FirestoreInboxStore> _logger;
	private FirestoreDb? _db;
	private CollectionReference? _collection;
	private bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreInboxStore"/> class.
	/// </summary>
	/// <param name="options">The Firestore inbox options.</param>
	/// <param name="logger">The logger instance.</param>
	public FirestoreInboxStore(
		IOptions<FirestoreInboxOptions> options,
		ILogger<FirestoreInboxStore> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_options.Validate();
		_logger = logger;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreInboxStore"/> class with an existing FirestoreDb.
	/// </summary>
	/// <param name="db">An existing Firestore database instance.</param>
	/// <param name="options">The Firestore inbox options.</param>
	/// <param name="logger">The logger instance.</param>
	public FirestoreInboxStore(
		FirestoreDb db,
		IOptions<FirestoreInboxOptions> options,
		ILogger<FirestoreInboxStore> logger)
	{
		ArgumentNullException.ThrowIfNull(db);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_db = db;
		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_collection = db.Collection(_options.CollectionName);
		_initialized = true;
	}

	/// <inheritdoc/>
	public async ValueTask<InboxEntry> CreateEntryAsync(
		string messageId,
		string handlerType,
		string messageType,
		byte[] payload,
		IDictionary<string, object> metadata,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ArgumentException.ThrowIfNullOrWhiteSpace(messageType);
		ArgumentNullException.ThrowIfNull(payload);
		ArgumentNullException.ThrowIfNull(metadata);

		await EnsureInitializedAsync().ConfigureAwait(false);

		var entry = new InboxEntry(messageId, handlerType, messageType, payload, metadata);
		var docId = GetDocumentId(messageId, handlerType);
		var docRef = _collection.Document(docId);

		var data = CreateDocumentData(entry);

		try
		{
			// CreateAsync fails if document already exists
			_ = await docRef.CreateAsync(data, cancellationToken).ConfigureAwait(false);
			LogCreatedEntry(_logger, messageId, handlerType, null);
			return entry;
		}
		catch (RpcException ex) when (ex.StatusCode == StatusCode.AlreadyExists)
		{
			throw new InvalidOperationException(
				$"Inbox entry already exists for message '{messageId}' and handler '{handlerType}'.", ex);
		}
	}

	/// <inheritdoc/>
	public async ValueTask MarkProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		await EnsureInitializedAsync().ConfigureAwait(false);

		var docId = GetDocumentId(messageId, handlerType);
		var docRef = _collection.Document(docId);

		var snapshot = await docRef.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

		if (!snapshot.Exists)
		{
			throw new InvalidOperationException(
				$"Inbox entry not found for message '{messageId}' and handler '{handlerType}'.");
		}

		var status = snapshot.GetValue<int>("status");
		if (status == (int)InboxStatus.Processed)
		{
			throw new InvalidOperationException(
				$"Inbox entry already processed for message '{messageId}' and handler '{handlerType}'.");
		}

		_ = await docRef.UpdateAsync(
			new Dictionary<string, object>
			{
				["status"] = (int)InboxStatus.Processed,
				["processedAt"] = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
				["lastError"] = FieldValue.Delete
			}, cancellationToken: cancellationToken).ConfigureAwait(false);

		LogProcessedEntry(_logger, messageId, handlerType, null);
	}

	/// <inheritdoc/>
	public async ValueTask<bool> TryMarkAsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		await EnsureInitializedAsync().ConfigureAwait(false);

		var docId = GetDocumentId(messageId, handlerType);
		var docRef = _collection.Document(docId);

		// Create a minimal document for first-writer-wins
		var now = DateTimeOffset.UtcNow;
		var data = new Dictionary<string, object>
		{
			["messageId"] = messageId,
			["handlerType"] = handlerType,
			["messageType"] = "Unknown",
			["status"] = (int)InboxStatus.Processed,
			["processedAt"] = Timestamp.FromDateTimeOffset(now),
			["receivedAt"] = Timestamp.FromDateTimeOffset(now)
		};

		try
		{
			// CreateAsync fails if document already exists
			_ = await docRef.CreateAsync(data, cancellationToken).ConfigureAwait(false);
			LogTryMarkProcessedSuccess(_logger, messageId, handlerType, null);
			return true;
		}
		catch (RpcException ex) when (ex.StatusCode == StatusCode.AlreadyExists)
		{
			LogTryMarkProcessedDuplicate(_logger, messageId, handlerType, null);
			return false;
		}
	}

	/// <inheritdoc/>
	public async ValueTask<bool> IsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		await EnsureInitializedAsync().ConfigureAwait(false);

		var docId = GetDocumentId(messageId, handlerType);
		var docRef = _collection.Document(docId);

		var snapshot = await docRef.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

		if (!snapshot.Exists)
		{
			return false;
		}

		var status = snapshot.GetValue<int>("status");
		return status == (int)InboxStatus.Processed;
	}

	/// <inheritdoc/>
	public async ValueTask<InboxEntry?> GetEntryAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		await EnsureInitializedAsync().ConfigureAwait(false);

		var docId = GetDocumentId(messageId, handlerType);
		var docRef = _collection.Document(docId);

		var snapshot = await docRef.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

		if (!snapshot.Exists)
		{
			return null;
		}

		return SnapshotToEntry(snapshot);
	}

	/// <inheritdoc/>
	public async ValueTask MarkFailedAsync(string messageId, string handlerType, string errorMessage, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ArgumentNullException.ThrowIfNull(errorMessage);

		await EnsureInitializedAsync().ConfigureAwait(false);

		var docId = GetDocumentId(messageId, handlerType);
		var docRef = _collection.Document(docId);

		var snapshot = await docRef.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

		if (!snapshot.Exists)
		{
			throw new InvalidOperationException(
				$"Inbox entry not found for message '{messageId}' and handler '{handlerType}'.");
		}

		_ = await docRef.UpdateAsync(
			new Dictionary<string, object>
			{
				["status"] = (int)InboxStatus.Failed,
				["lastError"] = errorMessage,
				["lastAttemptAt"] = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
				["retryCount"] = FieldValue.Increment(1)
			}, cancellationToken: cancellationToken).ConfigureAwait(false);

		LogFailedEntry(_logger, messageId, handlerType, errorMessage, null);
	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<InboxEntry>> GetFailedEntriesAsync(
		int maxRetries,
		DateTimeOffset? olderThan,
		int batchSize,
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync().ConfigureAwait(false);

		var query = _collection
			.WhereEqualTo("status", (int)InboxStatus.Failed)
			.WhereLessThan("retryCount", maxRetries);

		if (olderThan.HasValue)
		{
			query = query.WhereLessThan("lastAttemptAt", Timestamp.FromDateTimeOffset(olderThan.Value));
		}

		query = query.Limit(batchSize);

		var snapshot = await query.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

		return snapshot.Documents.Select(SnapshotToEntry);
	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<InboxEntry>> GetAllEntriesAsync(CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync().ConfigureAwait(false);

		var snapshot = await _collection.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

		return snapshot.Documents.Select(SnapshotToEntry);
	}

	/// <inheritdoc/>
	public async ValueTask<InboxStatistics> GetStatisticsAsync(CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync().ConfigureAwait(false);

		// Firestore doesn't support COUNT aggregation natively without reading documents
		// For efficiency, we query each status separately with a limit of 0
		// This requires reading all documents for accurate counts

		var allDocs = await _collection.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

		var total = 0;
		var processed = 0;
		var failed = 0;
		var pending = 0;

		foreach (var doc in allDocs.Documents)
		{
			total++;
			var status = doc.GetValue<int>("status");

			switch ((InboxStatus)status)
			{
				case InboxStatus.Processed:
					processed++;
					break;

				case InboxStatus.Failed:
					failed++;
					break;

				case InboxStatus.Received:
				case InboxStatus.Processing:
					pending++;
					break;

				default:
					break;
			}
		}

		return new InboxStatistics { TotalEntries = total, ProcessedEntries = processed, FailedEntries = failed, PendingEntries = pending };
	}

	/// <inheritdoc/>
	public async ValueTask<int> CleanupAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync().ConfigureAwait(false);

		var cutoff = DateTimeOffset.UtcNow - retentionPeriod;

		var query = _collection
			.WhereEqualTo("status", (int)InboxStatus.Processed)
			.WhereLessThan("processedAt", Timestamp.FromDateTimeOffset(cutoff));

		var snapshot = await query.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

		var deleted = 0;
		var batch = _db.StartBatch();
		const int maxBatchSize = 500; // Firestore batch limit

		foreach (var doc in snapshot.Documents)
		{
			_ = batch.Delete(doc.Reference);
			deleted++;

			if (deleted % maxBatchSize == 0)
			{
				_ = await batch.CommitAsync(cancellationToken).ConfigureAwait(false);
				batch = _db.StartBatch();
			}
		}

		if (deleted % maxBatchSize != 0)
		{
			_ = await batch.CommitAsync(cancellationToken).ConfigureAwait(false);
		}

		LogCleanedUpEntries(_logger, deleted, null);
		return deleted;
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return ValueTask.CompletedTask;
		}

		_disposed = true;
		// FirestoreDb doesn't implement IDisposable - connections are managed internally
		return ValueTask.CompletedTask;
	}

	private static string GetDocumentId(string messageId, string handlerType) =>
		$"{messageId}_{handlerType}";

	private static Dictionary<string, object> CreateDocumentData(InboxEntry entry)
	{
		var data = new Dictionary<string, object>
		{
			["messageId"] = entry.MessageId,
			["handlerType"] = entry.HandlerType,
			["messageType"] = entry.MessageType,
			["payload"] = Blob.CopyFrom(entry.Payload),
			["metadata"] = entry.Metadata.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
			["receivedAt"] = Timestamp.FromDateTimeOffset(entry.ReceivedAt),
			["status"] = (int)entry.Status,
			["retryCount"] = entry.RetryCount
		};

		if (entry.ProcessedAt.HasValue)
		{
			data["processedAt"] = Timestamp.FromDateTimeOffset(entry.ProcessedAt.Value);
		}

		if (entry.LastAttemptAt.HasValue)
		{
			data["lastAttemptAt"] = Timestamp.FromDateTimeOffset(entry.LastAttemptAt.Value);
		}

		if (!string.IsNullOrEmpty(entry.LastError))
		{
			data["lastError"] = entry.LastError;
		}

		if (!string.IsNullOrEmpty(entry.CorrelationId))
		{
			data["correlationId"] = entry.CorrelationId;
		}

		if (!string.IsNullOrEmpty(entry.TenantId))
		{
			data["tenantId"] = entry.TenantId;
		}

		if (!string.IsNullOrEmpty(entry.Source))
		{
			data["source"] = entry.Source;
		}

		return data;
	}

	private static InboxEntry SnapshotToEntry(DocumentSnapshot snapshot)
	{
		Blob? payloadBlob = snapshot.TryGetValue<Blob>("payload", out var blob) ? blob : null;
		var metadataDict = snapshot.TryGetValue<Dictionary<string, object>>("metadata", out var md) ? md : new Dictionary<string, object>();

		return new InboxEntry
		{
			MessageId = snapshot.GetValue<string>("messageId"),
			HandlerType = snapshot.GetValue<string>("handlerType"),
			MessageType = snapshot.GetValue<string>("messageType"),
			Payload = payloadBlob?.ByteString.ToByteArray() ?? [],
			Metadata = metadataDict,
			ReceivedAt = snapshot.GetValue<Timestamp>("receivedAt").ToDateTimeOffset(),
			ProcessedAt = snapshot.TryGetValue<Timestamp>("processedAt", out var processedAt) ? processedAt.ToDateTimeOffset() : null,
			Status = (InboxStatus)snapshot.GetValue<int>("status"),
			LastError = snapshot.TryGetValue<string>("lastError", out var error) ? error : null,
			RetryCount = snapshot.TryGetValue<int>("retryCount", out var retryCount) ? retryCount : 0,
			LastAttemptAt =
				snapshot.TryGetValue<Timestamp>("lastAttemptAt", out var lastAttempt) ? lastAttempt.ToDateTimeOffset() : null,
			CorrelationId = snapshot.TryGetValue<string>("correlationId", out var correlationId) ? correlationId : null,
			TenantId = snapshot.TryGetValue<string>("tenantId", out var tenantId) ? tenantId : null,
			Source = snapshot.TryGetValue<string>("source", out var source) ? source : null
		};
	}

	[LoggerMessage(DataFirestoreEventId.InboxEntryCreated, LogLevel.Debug,
		"Created inbox entry for message '{MessageId}' and handler '{HandlerType}'")]
	private static partial void LogCreatedEntry(ILogger logger, string messageId, string handlerType, Exception? exception);

	[LoggerMessage(DataFirestoreEventId.InboxEntryProcessed, LogLevel.Debug,
		"Marked inbox entry as processed for message '{MessageId}' and handler '{HandlerType}'")]
	private static partial void LogProcessedEntry(ILogger logger, string messageId, string handlerType, Exception? exception);

	[LoggerMessage(DataFirestoreEventId.InboxTryMarkProcessedSuccess, LogLevel.Debug,
		"TryMarkAsProcessed succeeded for message '{MessageId}' and handler '{HandlerType}'")]
	private static partial void LogTryMarkProcessedSuccess(ILogger logger, string messageId, string handlerType, Exception? exception);

	[LoggerMessage(DataFirestoreEventId.InboxTryMarkProcessedDuplicate, LogLevel.Debug,
		"TryMarkAsProcessed detected duplicate for message '{MessageId}' and handler '{HandlerType}'")]
	private static partial void LogTryMarkProcessedDuplicate(ILogger logger, string messageId, string handlerType, Exception? exception);

	[LoggerMessage(DataFirestoreEventId.InboxEntryFailed, LogLevel.Warning,
		"Marked inbox entry as failed for message '{MessageId}' and handler '{HandlerType}': {ErrorMessage}")]
	private static partial void LogFailedEntry(ILogger logger, string messageId, string handlerType, string errorMessage,
		Exception? exception);

	[LoggerMessage(DataFirestoreEventId.InboxCleanedUp, LogLevel.Information, "Cleaned up {Count} inbox entries")]
	private static partial void LogCleanedUpEntries(ILogger logger, int count, Exception? exception);

	private async Task EnsureInitializedAsync()
	{
		if (_initialized)
		{
			return;
		}

		var builder = new FirestoreDbBuilder { ProjectId = _options.ProjectId };

		if (!string.IsNullOrEmpty(_options.EmulatorHost))
		{
			builder.EmulatorDetection = Google.Api.Gax.EmulatorDetection.EmulatorOnly;
			_ = FirestoreEmulatorHelper.TryConfigureEmulatorHost(_options.EmulatorHost);
		}

		if (!string.IsNullOrEmpty(_options.CredentialsPath))
		{
			builder.CredentialsPath = _options.CredentialsPath;
		}
		else if (!string.IsNullOrEmpty(_options.CredentialsJson))
		{
			builder.JsonCredentials = _options.CredentialsJson;
		}

		_db = await builder.BuildAsync().ConfigureAwait(false);
		_collection = _db.Collection(_options.CollectionName);
		_initialized = true;
	}
}
