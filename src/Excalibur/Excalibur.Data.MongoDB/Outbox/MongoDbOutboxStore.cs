// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.Json;

using Excalibur.Data.MongoDB.Diagnostics;
using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MongoDB.Driver;

namespace Excalibur.Data.MongoDB.Outbox;

/// <summary>
/// MongoDB-based implementation of <see cref="IOutboxStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// Uses FindOneAndUpdate with status filter for atomic status transitions.
/// This prevents race conditions in MarkSentAsync by ensuring the status
/// check and update happen atomically in a single database operation.
/// </para>
/// <para>
/// Messages are indexed by status, priority, and scheduling for efficient queries.
/// </para>
/// </remarks>
public sealed partial class MongoDbOutboxStore : IOutboxStore, IOutboxStoreAdmin, IAsyncDisposable
{
	private readonly MongoDbOutboxOptions _options;
	private readonly ILogger<MongoDbOutboxStore> _logger;
	private IMongoClient? _client;
	private IMongoDatabase? _database;
	private IMongoCollection<MongoDbOutboxDocument>? _collection;
	private bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbOutboxStore"/> class.
	/// </summary>
	/// <param name="options">The MongoDB outbox options.</param>
	/// <param name="logger">The logger instance.</param>
	public MongoDbOutboxStore(
		IOptions<MongoDbOutboxOptions> options,
		ILogger<MongoDbOutboxStore> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_options.Validate();
		_logger = logger;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbOutboxStore"/> class with an existing client.
	/// </summary>
	/// <param name="client">An existing MongoDB client.</param>
	/// <param name="options">The MongoDB outbox options.</param>
	/// <param name="logger">The logger instance.</param>
	public MongoDbOutboxStore(
		IMongoClient client,
		IOptions<MongoDbOutboxOptions> options,
		ILogger<MongoDbOutboxStore> logger)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_client = client;
		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_database = client.GetDatabase(_options.DatabaseName);
		_collection = _database.GetCollection<MongoDbOutboxDocument>(_options.CollectionName);
	}

	/// <inheritdoc/>
	public async ValueTask StageMessageAsync(OutboundMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var document = MongoDbOutboxDocument.FromOutboundMessage(message);

		try
		{
			await _collection.InsertOneAsync(document, cancellationToken: cancellationToken).ConfigureAwait(false);
			LogMessageStaged(message.Id, message.MessageType, message.Destination);
		}
		catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
		{
			throw new InvalidOperationException(
				$"Message with ID '{message.Id}' already exists in the outbox.", ex);
		}
	}

	/// <inheritdoc/>
	public async ValueTask EnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var messageType = message.GetType().FullName ?? message.GetType().Name;
		var payload = JsonSerializer.SerializeToUtf8Bytes(message, message.GetType());

		var outbound = new OutboundMessage(messageType, payload, messageType)
		{
			CorrelationId = context.CorrelationId,
			CausationId = context.CausationId
		};

		await StageMessageAsync(outbound, cancellationToken).ConfigureAwait(false);

		LogMessageEnqueued(outbound.Id, messageType);
	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize, CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var now = DateTimeOffset.UtcNow;
		var filter = Builders<MongoDbOutboxDocument>.Filter;

		// Get staged messages that are either not scheduled or scheduled for now
		var stagedFilter = filter.And(
			filter.Eq(d => d.Status, (int)OutboxStatus.Staged),
			filter.Or(
				filter.Eq(d => d.ScheduledAt, null),
				filter.Lte(d => d.ScheduledAt, now)));

		// Sort by priority (lower = higher priority), then by creation time
		var sort = Builders<MongoDbOutboxDocument>.Sort
			.Ascending(d => d.Priority)
			.Ascending(d => d.CreatedAt);

		var documents = await _collection
			.Find(stagedFilter)
			.Sort(sort)
			.Limit(batchSize)
			.ToListAsync(cancellationToken)
			.ConfigureAwait(false);

		return documents.Select(d => d.ToOutboundMessage());
	}

	/// <inheritdoc/>
	public async ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var filter = Builders<MongoDbOutboxDocument>.Filter;
		var now = DateTimeOffset.UtcNow;

		// Use FindOneAndUpdate with status filter for atomic transition
		// This ensures no race condition: only one caller can successfully transition the status
		// We use Ne(Sent) so that only non-sent messages can be updated
		var atomicFilter = filter.And(
			filter.Eq(d => d.Id, messageId),
			filter.Ne(d => d.Status, (int)OutboxStatus.Sent));

		var update = Builders<MongoDbOutboxDocument>.Update
			.Set(d => d.Status, (int)OutboxStatus.Sent)
			.Set(d => d.SentAt, now)
			.Set(d => d.LastError, null);

		var result = await _collection.FindOneAndUpdateAsync(
			atomicFilter,
			update,
			new FindOneAndUpdateOptions<MongoDbOutboxDocument> { ReturnDocument = ReturnDocument.Before },
			cancellationToken).ConfigureAwait(false);

		// If result is null, either message doesn't exist OR it was already sent
		if (result == null)
		{
			// Check if message exists to provide correct error message
			var exists = await _collection.CountDocumentsAsync(
				filter.Eq(d => d.Id, messageId),
				cancellationToken: cancellationToken).ConfigureAwait(false);

			if (exists == 0)
			{
				throw new InvalidOperationException($"Message with ID '{messageId}' not found.");
			}

			throw new InvalidOperationException($"Message with ID '{messageId}' is already marked as sent.");
		}

		LogMessageSent(messageId);
	}

	/// <inheritdoc/>
	public async ValueTask MarkFailedAsync(string messageId, string errorMessage, int retryCount, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentNullException.ThrowIfNull(errorMessage);
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var filter = Builders<MongoDbOutboxDocument>.Filter.Eq(d => d.Id, messageId);
		var now = DateTimeOffset.UtcNow;

		// Check if exists - silent return per conformance tests
		var exists = await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken).ConfigureAwait(false);
		if (exists == 0)
		{
			return;
		}

		var update = Builders<MongoDbOutboxDocument>.Update
			.Set(d => d.Status, (int)OutboxStatus.Failed)
			.Set(d => d.LastError, errorMessage)
			.Set(d => d.RetryCount, retryCount)
			.Set(d => d.LastAttemptAt, now);

		_ = await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken).ConfigureAwait(false);

		LogMessageFailed(messageId, errorMessage, retryCount);
	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<OutboundMessage>> GetFailedMessagesAsync(
		int maxRetries,
		DateTimeOffset? olderThan,
		int batchSize,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var filterBuilder = Builders<MongoDbOutboxDocument>.Filter;
		var filter = filterBuilder.Eq(d => d.Status, (int)OutboxStatus.Failed);

		if (maxRetries > 0)
		{
			filter = filterBuilder.And(filter, filterBuilder.Lt(d => d.RetryCount, maxRetries));
		}

		if (olderThan.HasValue)
		{
			filter = filterBuilder.And(filter, filterBuilder.Lt(d => d.LastAttemptAt, olderThan.Value));
		}

		// Sort by retry count (ascending) then by last attempt time
		var sort = Builders<MongoDbOutboxDocument>.Sort
			.Ascending(d => d.RetryCount)
			.Ascending(d => d.LastAttemptAt);

		var documents = await _collection
			.Find(filter)
			.Sort(sort)
			.Limit(batchSize)
			.ToListAsync(cancellationToken)
			.ConfigureAwait(false);

		return documents.Select(d => d.ToOutboundMessage());
	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<OutboundMessage>> GetScheduledMessagesAsync(
		DateTimeOffset scheduledBefore,
		int batchSize,
		CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var filterBuilder = Builders<MongoDbOutboxDocument>.Filter;
		var filter = filterBuilder.And(
			filterBuilder.Eq(d => d.Status, (int)OutboxStatus.Staged),
			filterBuilder.Ne(d => d.ScheduledAt, null),
			filterBuilder.Lte(d => d.ScheduledAt, scheduledBefore));

		var sort = Builders<MongoDbOutboxDocument>.Sort.Ascending(d => d.ScheduledAt);

		var documents = await _collection
			.Find(filter)
			.Sort(sort)
			.Limit(batchSize)
			.ToListAsync(cancellationToken)
			.ConfigureAwait(false);

		return documents.Select(d => d.ToOutboundMessage());
	}

	/// <inheritdoc/>
	public async ValueTask<int> CleanupSentMessagesAsync(DateTimeOffset olderThan, int batchSize, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var filterBuilder = Builders<MongoDbOutboxDocument>.Filter;
		var filter = filterBuilder.And(
			filterBuilder.Eq(d => d.Status, (int)OutboxStatus.Sent),
			filterBuilder.Lt(d => d.SentAt, olderThan));

		// Find messages to delete
		var toDelete = await _collection
			.Find(filter)
			.Limit(batchSize)
			.ToListAsync(cancellationToken)
			.ConfigureAwait(false);

		if (toDelete.Count == 0)
		{
			return 0;
		}

		// Delete by IDs
		var ids = toDelete.Select(d => d.Id).ToList();
		var deleteFilter = filterBuilder.In(d => d.Id, ids);

		var result = await _collection.DeleteManyAsync(deleteFilter, cancellationToken).ConfigureAwait(false);

		LogMessagesCleanedUp((int)result.DeletedCount, olderThan);
		return (int)result.DeletedCount;
	}

	/// <inheritdoc/>
	public async ValueTask<OutboxStatistics> GetStatisticsAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var filter = Builders<MongoDbOutboxDocument>.Filter;
		var now = DateTimeOffset.UtcNow;

		// Count by status
		var stagedCount = (int)await _collection.CountDocumentsAsync(
			filter.Eq(d => d.Status, (int)OutboxStatus.Staged),
			cancellationToken: cancellationToken).ConfigureAwait(false);

		var sendingCount = (int)await _collection.CountDocumentsAsync(
			filter.Eq(d => d.Status, (int)OutboxStatus.Sending),
			cancellationToken: cancellationToken).ConfigureAwait(false);

		var sentCount = (int)await _collection.CountDocumentsAsync(
			filter.Eq(d => d.Status, (int)OutboxStatus.Sent),
			cancellationToken: cancellationToken).ConfigureAwait(false);

		var failedCount = (int)await _collection.CountDocumentsAsync(
			filter.Eq(d => d.Status, (int)OutboxStatus.Failed),
			cancellationToken: cancellationToken).ConfigureAwait(false);

		var scheduledCount = (int)await _collection.CountDocumentsAsync(
			filter.And(
				filter.Eq(d => d.Status, (int)OutboxStatus.Staged),
				filter.Ne(d => d.ScheduledAt, null)),
			cancellationToken: cancellationToken).ConfigureAwait(false);

		// Get oldest unsent
		TimeSpan? oldestUnsentAge = null;
		var oldestUnsent = await _collection
			.Find(filter.And(
				filter.Eq(d => d.Status, (int)OutboxStatus.Staged),
				filter.Or(
					filter.Eq(d => d.ScheduledAt, null),
					filter.Lte(d => d.ScheduledAt, now))))
			.SortBy(d => d.CreatedAt)
			.Limit(1)
			.FirstOrDefaultAsync(cancellationToken)
			.ConfigureAwait(false);

		if (oldestUnsent != null)
		{
			oldestUnsentAge = now - oldestUnsent.CreatedAt;
		}

		// Get oldest failed
		TimeSpan? oldestFailedAge = null;
		var oldestFailed = await _collection
			.Find(filter.Eq(d => d.Status, (int)OutboxStatus.Failed))
			.SortBy(d => d.CreatedAt)
			.Limit(1)
			.FirstOrDefaultAsync(cancellationToken)
			.ConfigureAwait(false);

		if (oldestFailed != null)
		{
			oldestFailedAge = now - oldestFailed.CreatedAt;
		}

		return new OutboxStatistics
		{
			StagedMessageCount = stagedCount,
			SendingMessageCount = sendingCount,
			SentMessageCount = sentCount,
			FailedMessageCount = failedCount,
			ScheduledMessageCount = scheduledCount,
			OldestUnsentMessageAge = oldestUnsentAge,
			OldestFailedMessageAge = oldestFailedAge,
			CapturedAt = now
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
		// MongoDB client doesn't implement IDisposable - it manages connections internally
		return ValueTask.CompletedTask;
	}

	private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
	{
		if (_initialized)
		{
			return;
		}

		if (_client == null)
		{
			var settings = MongoClientSettings.FromConnectionString(_options.ConnectionString);
			settings.ServerSelectionTimeout = TimeSpan.FromSeconds(_options.ServerSelectionTimeoutSeconds);
			settings.ConnectTimeout = TimeSpan.FromSeconds(_options.ConnectTimeoutSeconds);
			settings.MaxConnectionPoolSize = _options.MaxPoolSize;

			if (_options.UseSsl)
			{
				settings.UseTls = true;
			}

			_client = new MongoClient(settings);
			_database = _client.GetDatabase(_options.DatabaseName);
			_collection = _database.GetCollection<MongoDbOutboxDocument>(_options.CollectionName);
		}

		// Create indexes
		var indexBuilder = Builders<MongoDbOutboxDocument>.IndexKeys;

		// Compound index for unsent message queries: status + scheduledAt + priority + createdAt
		var unsentIndex = new CreateIndexModel<MongoDbOutboxDocument>(
			indexBuilder.Combine(
				indexBuilder.Ascending(d => d.Status),
				indexBuilder.Ascending(d => d.ScheduledAt),
				indexBuilder.Ascending(d => d.Priority),
				indexBuilder.Ascending(d => d.CreatedAt)));

		// Index on status for status-specific queries
		var statusIndex = new CreateIndexModel<MongoDbOutboxDocument>(
			indexBuilder.Ascending(d => d.Status));

		// Index for failed message queries
		var failedIndex = new CreateIndexModel<MongoDbOutboxDocument>(
			indexBuilder.Combine(
				indexBuilder.Ascending(d => d.Status),
				indexBuilder.Ascending(d => d.RetryCount),
				indexBuilder.Ascending(d => d.LastAttemptAt)));

		// TTL index on SentAt for automatic cleanup
		if (_options.SentMessageTtlSeconds > 0)
		{
			var ttlIndex = new CreateIndexModel<MongoDbOutboxDocument>(
				indexBuilder.Ascending(d => d.SentAt),
				new CreateIndexOptions { ExpireAfter = TimeSpan.FromSeconds(_options.SentMessageTtlSeconds) });

			_ = await _collection.Indexes.CreateOneAsync(ttlIndex, cancellationToken: cancellationToken).ConfigureAwait(false);
		}

		_ = await _collection.Indexes.CreateManyAsync([unsentIndex, statusIndex, failedIndex], cancellationToken).ConfigureAwait(false);

		_initialized = true;
	}

	[LoggerMessage(DataMongoDbEventId.MessageStaged, LogLevel.Debug, "Staged message {MessageId} of type {MessageType} to destination {Destination}")]
	private partial void LogMessageStaged(string messageId, string messageType, string destination);

	[LoggerMessage(DataMongoDbEventId.MessageEnqueued, LogLevel.Debug, "Enqueued message {MessageId} of type {MessageType}")]
	private partial void LogMessageEnqueued(string messageId, string messageType);

	[LoggerMessage(DataMongoDbEventId.MessageSent, LogLevel.Debug, "Marked message {MessageId} as sent")]
	private partial void LogMessageSent(string messageId);

	[LoggerMessage(DataMongoDbEventId.MessageFailed, LogLevel.Warning, "Marked message {MessageId} as failed: {ErrorMessage} (retry {RetryCount})")]
	private partial void LogMessageFailed(string messageId, string errorMessage, int retryCount);

	[LoggerMessage(DataMongoDbEventId.MessagesCleanedUp, LogLevel.Information, "Cleaned up {Count} sent messages older than {OlderThan}")]
	private partial void LogMessagesCleanedUp(int count, DateTimeOffset olderThan);
}
