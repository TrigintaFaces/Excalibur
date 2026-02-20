// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;

using Excalibur.Data.Firestore.Diagnostics;
using Excalibur.Dispatch.Abstractions;

using Google.Cloud.Firestore;

using Grpc.Core;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;


namespace Excalibur.Data.Firestore.Outbox;

/// <summary>
/// Firestore-based implementation of <see cref="IOutboxStore"/>.
/// </summary>
/// <remarks>
/// Uses a simple collection design with documents keyed by message ID.
/// Status transitions are handled using Firestore transactions for atomicity.
/// </remarks>
public sealed partial class FirestoreOutboxStore : IOutboxStore, IOutboxStoreAdmin, IAsyncDisposable
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = false
	};

	private readonly FirestoreOutboxOptions _options;
	private readonly ILogger<FirestoreOutboxStore> _logger;
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
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_options.Validate();
		_logger = logger;

	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreOutboxStore"/> class with an existing FirestoreDb.
	/// </summary>
	/// <param name="db">An existing Firestore database instance.</param>
	/// <param name="options">The Firestore outbox options.</param>
	/// <param name="logger">The logger instance.</param>
	public FirestoreOutboxStore(
		FirestoreDb db,
		IOptions<FirestoreOutboxOptions> options,
		ILogger<FirestoreOutboxStore> logger)
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
	public async ValueTask StageMessageAsync(OutboundMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentException.ThrowIfNullOrWhiteSpace(message.Id);

		await EnsureInitializedAsync().ConfigureAwait(false);

		var docRef = _collection.Document(message.Id);
		var docData = ToFirestoreDocument(message);


		try
		{
			// CreateAsync fails if document already exists (duplicate detection)
			_ = await docRef.CreateAsync(docData, cancellationToken).ConfigureAwait(false);
			LogMessageStaged(message.Id, message.MessageType, message.Priority);
		}
		catch (RpcException ex) when (ex.StatusCode == StatusCode.AlreadyExists)
		{
			throw new InvalidOperationException(
				$"Message with ID '{message.Id}' already exists.", ex);

		}
	}

	/// <inheritdoc/>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	public async ValueTask EnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);

		var messageType = message.GetType().FullName ?? message.GetType().Name;
		var payload = JsonSerializer.SerializeToUtf8Bytes(message, message.GetType());

		var outboundMessage = new OutboundMessage(messageType, payload, messageType)
		{
			CorrelationId = context.CorrelationId,
			CausationId = context.CausationId
		};

		await StageMessageAsync(outboundMessage, cancellationToken).ConfigureAwait(false);

		LogMessageEnqueued(outboundMessage.Id, messageType);

	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize, CancellationToken cancellationToken)
	{
		if (batchSize <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be greater than 0.");
		}

		await EnsureInitializedAsync().ConfigureAwait(false);

		var now = DateTimeOffset.UtcNow;

		// Query staged messages that are ready for delivery (not scheduled or scheduled time passed)
		// Order by createdAt to ensure FIFO ordering (oldest first)
		var query = _collection
			.WhereEqualTo("status", (int)OutboxStatus.Staged)
			.OrderBy("createdAt")
			.Limit(batchSize);

		var snapshot = await query.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

		var messages = new List<OutboundMessage>();
		foreach (var doc in snapshot.Documents)
		{
			var message = FromFirestoreDocument(doc);

			// Filter out scheduled messages that aren't due yet
			if (message.ScheduledAt.HasValue && message.ScheduledAt.Value > now)
			{
				continue;
			}

			messages.Add(message);

			if (messages.Count >= batchSize)
			{
				break;
			}
		}

		return messages;

	}

	/// <inheritdoc/>
	public async ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

		await EnsureInitializedAsync().ConfigureAwait(false);


		var docRef = _collection.Document(messageId);

		// Use a transaction for optimistic concurrency control.
		// Only one concurrent MarkSentAsync call can succeed - others will fail
		// because the transaction sees a different status than expected.
		try
		{
			await _db.RunTransactionAsync(async transaction =>
			{
				var snapshot = await transaction.GetSnapshotAsync(docRef, cancellationToken).ConfigureAwait(false);

				if (!snapshot.Exists)
				{
					throw new InvalidOperationException($"Message with ID '{messageId}' not found.");
				}

				var currentStatus = (OutboxStatus)snapshot.GetValue<int>("status");
				if (currentStatus == OutboxStatus.Sent)
				{
					throw new InvalidOperationException($"Message with ID '{messageId}' is already marked as sent.");

				}

				// Only allow transition from Staged or Failed to Sent
				if (currentStatus is not OutboxStatus.Staged and not OutboxStatus.Failed)
				{
					throw new InvalidOperationException(
						$"Cannot mark message '{messageId}' as sent. Current status: {currentStatus}");
				}

				var now = DateTimeOffset.UtcNow;
				transaction.Update(docRef, new Dictionary<string, object>
				{
					["status"] = (int)OutboxStatus.Sent,
					["sentAt"] = now.ToString("o", CultureInfo.InvariantCulture),
					["lastError"] = FieldValue.Delete
				});
			}, cancellationToken: cancellationToken).ConfigureAwait(false);

			LogMessageSent(messageId);
		}
		catch (RpcException ex) when (ex.StatusCode == StatusCode.Aborted)

		{
			// Transaction was aborted due to concurrent modification
			// Re-throw as InvalidOperationException to indicate concurrency conflict
			throw new InvalidOperationException(
				$"Concurrent modification detected for message '{messageId}'. Another process marked it as sent.", ex);

		}
	}

	/// <inheritdoc/>
	public async ValueTask MarkFailedAsync(string messageId, string errorMessage, int retryCount, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentNullException.ThrowIfNull(errorMessage);

		await EnsureInitializedAsync().ConfigureAwait(false);

		var docRef = _collection.Document(messageId);
		var snapshot = await docRef.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

		if (!snapshot.Exists)

		{
			// Message doesn't exist - silent return per conformance tests
			return;
		}

		var now = DateTimeOffset.UtcNow;
		_ = await docRef.UpdateAsync(new Dictionary<string, object>
		{
			["status"] = (int)OutboxStatus.Failed,
			["lastError"] = errorMessage,
			["retryCount"] = retryCount,
			["lastAttemptAt"] = now.ToString("o", CultureInfo.InvariantCulture)
		}, cancellationToken: cancellationToken).ConfigureAwait(false);

		LogMessageFailed(messageId, errorMessage, retryCount);

	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<OutboundMessage>> GetFailedMessagesAsync(
		int maxRetries,
		DateTimeOffset? olderThan,
		int batchSize,
		CancellationToken cancellationToken)
	{
		if (batchSize <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be greater than 0.");
		}

		await EnsureInitializedAsync().ConfigureAwait(false);

		// Query failed messages
		var query = _collection
			.WhereEqualTo("status", (int)OutboxStatus.Failed);

		if (maxRetries > 0)
		{
			query = query.WhereLessThan("retryCount", maxRetries);
		}

		if (olderThan.HasValue)
		{
			query = query.WhereLessThan("lastAttemptAt", olderThan.Value.ToString("o", CultureInfo.InvariantCulture));
		}

		query = query.Limit(batchSize);

		var snapshot = await query.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);


		var messages = snapshot.Documents.Select(FromFirestoreDocument).ToList();

		// Sort by retryCount ASC, lastAttemptAt ASC
		return messages
			.OrderBy(m => m.RetryCount)
			.ThenBy(m => m.LastAttemptAt);

	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<OutboundMessage>> GetScheduledMessagesAsync(
		DateTimeOffset scheduledBefore,
		int batchSize,
		CancellationToken cancellationToken)
	{
		if (batchSize <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be greater than 0.");
		}

		await EnsureInitializedAsync().ConfigureAwait(false);

		// Query staged messages with scheduledAt <= scheduledBefore
		var query = _collection
			.WhereEqualTo("status", (int)OutboxStatus.Staged)
			.WhereLessThanOrEqualTo("scheduledAt", scheduledBefore.ToString("o", CultureInfo.InvariantCulture))
			.Limit(batchSize);

		var snapshot = await query.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

		return snapshot.Documents.Select(FromFirestoreDocument);

	}

	/// <inheritdoc/>
	public async ValueTask<int> CleanupSentMessagesAsync(DateTimeOffset olderThan, int batchSize, CancellationToken cancellationToken)
	{
		if (batchSize <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be greater than 0.");
		}

		await EnsureInitializedAsync().ConfigureAwait(false);

		var olderThanStr = olderThan.ToString("o", CultureInfo.InvariantCulture);

		// Query sent messages older than cutoff
		var query = _collection
			.WhereEqualTo("status", (int)OutboxStatus.Sent)
			.WhereLessThan("sentAt", olderThanStr)
			.Limit(batchSize);

		var snapshot = await query.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

		if (snapshot.Documents.Count == 0)
		{
			return 0;

		}

		// Delete in batches
		var deleted = 0;
		const int maxBatchSize = 500; // Firestore batch limit
		var batch = _db.StartBatch();

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

		LogCleanedUp(deleted, olderThan);
		return deleted;

	}

	/// <inheritdoc/>
	public async ValueTask<OutboxStatistics> GetStatisticsAsync(CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync().ConfigureAwait(false);

		var now = DateTimeOffset.UtcNow;

		// Firestore doesn't support COUNT aggregation natively without reading documents
		// Need to read all documents for accurate counts
		var allDocs = await _collection.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

		var staged = 0;
		var sending = 0;
		var sent = 0;
		var failed = 0;
		var scheduled = 0;
		DateTimeOffset? oldestStagedCreatedAt = null;
		DateTimeOffset? oldestFailedCreatedAt = null;

		foreach (var doc in allDocs.Documents)
		{
			var status = (OutboxStatus)doc.GetValue<int>("status");
			var createdAtStr = doc.GetValue<string>("createdAt");
			var createdAt = DateTimeOffset.Parse(createdAtStr, CultureInfo.InvariantCulture);

			switch (status)
			{
				case OutboxStatus.Staged:
					// Check if scheduled
					if (doc.TryGetValue<string>("scheduledAt", out _))
					{
						scheduled++;
					}
					else
					{
						staged++;
					}

					if (!oldestStagedCreatedAt.HasValue || createdAt < oldestStagedCreatedAt)
					{
						oldestStagedCreatedAt = createdAt;
					}
					break;
				case OutboxStatus.Sending:
					sending++;
					break;
				case OutboxStatus.Sent:
					sent++;
					break;
				case OutboxStatus.Failed:
					failed++;
					if (!oldestFailedCreatedAt.HasValue || createdAt < oldestFailedCreatedAt)
					{
						oldestFailedCreatedAt = createdAt;
					}
					break;
				case OutboxStatus.PartiallyFailed:
					break;
				default:
					break;
			}
		}

		return new OutboxStatistics
		{
			StagedMessageCount = staged,
			SendingMessageCount = sending,
			SentMessageCount = sent,
			FailedMessageCount = failed,
			ScheduledMessageCount = scheduled,
			OldestUnsentMessageAge = oldestStagedCreatedAt.HasValue ? now - oldestStagedCreatedAt.Value : null,
			OldestFailedMessageAge = oldestFailedCreatedAt.HasValue ? now - oldestFailedCreatedAt.Value : null
		};

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

	private async Task EnsureInitializedAsync()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

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

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	private static Dictionary<string, object> ToFirestoreDocument(OutboundMessage message)
	{
		var doc = new Dictionary<string, object>
		{
			["messageId"] = message.Id,
			["messageType"] = message.MessageType,
			["payload"] = Blob.CopyFrom(message.Payload),
			["destination"] = message.Destination,
			["createdAt"] = message.CreatedAt.ToString("o", CultureInfo.InvariantCulture),
			["status"] = (int)message.Status,
			["priority"] = message.Priority,
			["retryCount"] = message.RetryCount
		};

		// Serialize headers as JSON string
		if (message.Headers.Count > 0)
		{
			doc["headers"] = JsonSerializer.Serialize(message.Headers, JsonOptions);
		}

		if (message.ScheduledAt.HasValue)
		{
			doc["scheduledAt"] = message.ScheduledAt.Value.ToString("o", CultureInfo.InvariantCulture);
		}

		if (message.SentAt.HasValue)
		{
			doc["sentAt"] = message.SentAt.Value.ToString("o", CultureInfo.InvariantCulture);
		}

		if (!string.IsNullOrEmpty(message.CorrelationId))
		{
			doc["correlationId"] = message.CorrelationId;
		}

		if (!string.IsNullOrEmpty(message.CausationId))
		{
			doc["causationId"] = message.CausationId;
		}

		if (!string.IsNullOrEmpty(message.TenantId))
		{
			doc["tenantId"] = message.TenantId;
		}

		if (!string.IsNullOrEmpty(message.LastError))
		{
			doc["lastError"] = message.LastError;
		}

		if (message.LastAttemptAt.HasValue)
		{
			doc["lastAttemptAt"] = message.LastAttemptAt.Value.ToString("o", CultureInfo.InvariantCulture);
		}

		if (!string.IsNullOrEmpty(message.TargetTransports))
		{
			doc["targetTransports"] = message.TargetTransports;
		}

		if (message.IsMultiTransport)
		{
			doc["isMultiTransport"] = message.IsMultiTransport;
		}

		return doc;
	}

	private static OutboundMessage FromFirestoreDocument(DocumentSnapshot doc)
	{
		Blob? payloadBlob = doc.TryGetValue<Blob>("payload", out var blob) ? blob : null;

		var message = new OutboundMessage
		{
			Id = doc.GetValue<string>("messageId"),
			MessageType = doc.GetValue<string>("messageType"),
			Payload = payloadBlob?.ByteString.ToByteArray() ?? [],
			Destination = doc.GetValue<string>("destination"),
			CreatedAt = DateTimeOffset.Parse(doc.GetValue<string>("createdAt"), CultureInfo.InvariantCulture),
			Status = (OutboxStatus)doc.GetValue<int>("status"),
			Priority = doc.TryGetValue<int>("priority", out var priority) ? priority : 0,
			RetryCount = doc.TryGetValue<int>("retryCount", out var retryCount) ? retryCount : 0
		};

		// Parse headers from JSON string
		if (doc.TryGetValue<string>("headers", out var headersJson) && !string.IsNullOrEmpty(headersJson))
		{
			var headers = JsonSerializer.Deserialize<Dictionary<string, object>>(headersJson, JsonOptions);
			if (headers != null)
			{
				foreach (var kvp in headers)
				{
					message.Headers[kvp.Key] = kvp.Value;
				}
			}
		}

		if (doc.TryGetValue<string>("scheduledAt", out var scheduledAt))
		{
			message.ScheduledAt = DateTimeOffset.Parse(scheduledAt, CultureInfo.InvariantCulture);
		}

		if (doc.TryGetValue<string>("sentAt", out var sentAt))
		{
			message.SentAt = DateTimeOffset.Parse(sentAt, CultureInfo.InvariantCulture);
		}

		if (doc.TryGetValue<string>("correlationId", out var correlationId))
		{
			message.CorrelationId = correlationId;
		}

		if (doc.TryGetValue<string>("causationId", out var causationId))
		{
			message.CausationId = causationId;
		}

		if (doc.TryGetValue<string>("tenantId", out var tenantId))
		{
			message.TenantId = tenantId;
		}

		if (doc.TryGetValue<string>("lastError", out var lastError))
		{
			message.LastError = lastError;
		}

		if (doc.TryGetValue<string>("lastAttemptAt", out var lastAttemptAt))
		{
			message.LastAttemptAt = DateTimeOffset.Parse(lastAttemptAt, CultureInfo.InvariantCulture);
		}

		if (doc.TryGetValue<string>("targetTransports", out var targetTransports))
		{
			message.TargetTransports = targetTransports;
		}

		if (doc.TryGetValue<bool>("isMultiTransport", out var isMultiTransport))
		{
			message.IsMultiTransport = isMultiTransport;
		}

		return message;

	}

	// Logging methods using LoggerMessage source generator
	[LoggerMessage(DataFirestoreEventId.OutboxMessageStaged, LogLevel.Debug, "Staged outbox message {MessageId} of type {MessageType} with priority {Priority}")]
	private partial void LogMessageStaged(string messageId, string messageType, int priority);

	[LoggerMessage(DataFirestoreEventId.OutboxMessageEnqueued, LogLevel.Debug, "Enqueued outbox message {MessageId} of type {MessageType}")]
	private partial void LogMessageEnqueued(string messageId, string messageType);

	[LoggerMessage(DataFirestoreEventId.OutboxMessageSent, LogLevel.Debug, "Marked outbox message {MessageId} as sent")]
	private partial void LogMessageSent(string messageId);

	[LoggerMessage(DataFirestoreEventId.OutboxMessageFailed, LogLevel.Warning, "Marked outbox message {MessageId} as failed: {ErrorMessage} (retry {RetryCount})")]
	private partial void LogMessageFailed(string messageId, string errorMessage, int retryCount);

	[LoggerMessage(DataFirestoreEventId.OutboxCleanedUp, LogLevel.Information, "Cleaned up {Count} sent outbox messages older than {CutoffDate}")]
	private partial void LogCleanedUp(int count, DateTimeOffset cutoffDate);
}
