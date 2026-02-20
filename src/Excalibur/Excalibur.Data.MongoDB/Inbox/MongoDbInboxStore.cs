// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.MongoDB.Diagnostics;
using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MongoDB.Driver;

namespace Excalibur.Data.MongoDB.Inbox;

/// <summary>
/// MongoDB-based implementation of <see cref="IInboxStore"/>.
/// </summary>
/// <remarks>
/// Uses InsertOneAsync with unique index on (messageId, handlerType) for atomic first-writer-wins semantics.
/// Catches MongoWriteException with duplicate key error (11000) for conflict detection.
/// </remarks>
public sealed partial class MongoDbInboxStore : IInboxStore, IAsyncDisposable
{
	private const int DuplicateKeyErrorCode = 11000;

	private readonly MongoDbInboxOptions _options;
	private readonly ILogger<MongoDbInboxStore> _logger;
	private IMongoClient? _client;
	private IMongoDatabase? _database;
	private IMongoCollection<MongoDbInboxDocument>? _collection;
	private bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbInboxStore"/> class.
	/// </summary>
	/// <param name="options">The MongoDB inbox options.</param>
	/// <param name="logger">The logger instance.</param>
	public MongoDbInboxStore(
		IOptions<MongoDbInboxOptions> options,
		ILogger<MongoDbInboxStore> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_options.Validate();
		_logger = logger;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbInboxStore"/> class with an existing client.
	/// </summary>
	/// <param name="client">An existing MongoDB client.</param>
	/// <param name="options">The MongoDB inbox options.</param>
	/// <param name="logger">The logger instance.</param>
	public MongoDbInboxStore(
		IMongoClient client,
		IOptions<MongoDbInboxOptions> options,
		ILogger<MongoDbInboxStore> logger)
	{
		ArgumentNullException.ThrowIfNull(client);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_client = client;
		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_database = client.GetDatabase(_options.DatabaseName);
		_collection = _database.GetCollection<MongoDbInboxDocument>(_options.CollectionName);
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

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var entry = new InboxEntry(messageId, handlerType, messageType, payload, metadata);
		var document = MongoDbInboxDocument.FromInboxEntry(entry);

		try
		{
			await _collection.InsertOneAsync(document, cancellationToken: cancellationToken).ConfigureAwait(false);
			LogCreatedEntry(_logger, messageId, handlerType, null);
			return entry;
		}
		catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
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

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var id = MongoDbInboxDocument.CreateId(messageId, handlerType);
		var filter = Builders<MongoDbInboxDocument>.Filter.Eq(d => d.Id, id);

		var existing = await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false)
					   ?? throw new InvalidOperationException(
						   $"Inbox entry not found for message '{messageId}' and handler '{handlerType}'.");

		if (existing.Status == (int)InboxStatus.Processed)
		{
			throw new InvalidOperationException(
				$"Inbox entry already processed for message '{messageId}' and handler '{handlerType}'.");
		}

		var update = Builders<MongoDbInboxDocument>.Update
			.Set(d => d.Status, (int)InboxStatus.Processed)
			.Set(d => d.ProcessedAt, DateTimeOffset.UtcNow)
			.Set(d => d.LastError, null);

		_ = await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken).ConfigureAwait(false);

		LogProcessedEntry(_logger, messageId, handlerType, null);
	}

	/// <inheritdoc/>
	public async ValueTask<bool> TryMarkAsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		// Create a minimal document for first-writer-wins
		var document = new MongoDbInboxDocument
		{
			Id = MongoDbInboxDocument.CreateId(messageId, handlerType),
			MessageId = messageId,
			HandlerType = handlerType,
			MessageType = "Unknown",
			Status = (int)InboxStatus.Processed,
			ProcessedAt = DateTimeOffset.UtcNow,
			ReceivedAt = DateTimeOffset.UtcNow
		};

		try
		{
			await _collection.InsertOneAsync(document, cancellationToken: cancellationToken).ConfigureAwait(false);
			LogTryMarkProcessedSuccess(_logger, messageId, handlerType, null);
			return true;
		}
		catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
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

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var id = MongoDbInboxDocument.CreateId(messageId, handlerType);
		var filter = Builders<MongoDbInboxDocument>.Filter.And(
			Builders<MongoDbInboxDocument>.Filter.Eq(d => d.Id, id),
			Builders<MongoDbInboxDocument>.Filter.Eq(d => d.Status, (int)InboxStatus.Processed));

		var count = await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken).ConfigureAwait(false);
		return count > 0;
	}

	/// <inheritdoc/>
	public async ValueTask<InboxEntry?> GetEntryAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var id = MongoDbInboxDocument.CreateId(messageId, handlerType);
		var filter = Builders<MongoDbInboxDocument>.Filter.Eq(d => d.Id, id);

		var document = await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
		return document?.ToInboxEntry();
	}

	/// <inheritdoc/>
	public async ValueTask MarkFailedAsync(string messageId, string handlerType, string errorMessage, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ArgumentNullException.ThrowIfNull(errorMessage);

		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var id = MongoDbInboxDocument.CreateId(messageId, handlerType);
		var filter = Builders<MongoDbInboxDocument>.Filter.Eq(d => d.Id, id);

		_ = await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false)
			?? throw new InvalidOperationException(
				$"Inbox entry not found for message '{messageId}' and handler '{handlerType}'.");

		var update = Builders<MongoDbInboxDocument>.Update
			.Set(d => d.Status, (int)InboxStatus.Failed)
			.Set(d => d.LastError, errorMessage)
			.Set(d => d.LastAttemptAt, DateTimeOffset.UtcNow)
			.Inc(d => d.RetryCount, 1);

		_ = await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken).ConfigureAwait(false);

		LogFailedEntry(_logger, messageId, handlerType, errorMessage, null);
	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<InboxEntry>> GetFailedEntriesAsync(
		int maxRetries,
		DateTimeOffset? olderThan,
		int batchSize,
		CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var filterBuilder = Builders<MongoDbInboxDocument>.Filter;
		var filter = filterBuilder.And(
			filterBuilder.Eq(d => d.Status, (int)InboxStatus.Failed),
			filterBuilder.Lt(d => d.RetryCount, maxRetries));

		if (olderThan.HasValue)
		{
			filter = filterBuilder.And(filter, filterBuilder.Lt(d => d.LastAttemptAt, olderThan.Value));
		}

		var documents = await _collection
			.Find(filter)
			.Limit(batchSize)
			.ToListAsync(cancellationToken)
			.ConfigureAwait(false);

		return documents.Select(d => d.ToInboxEntry());
	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<InboxEntry>> GetAllEntriesAsync(CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var documents = await _collection
			.Find(Builders<MongoDbInboxDocument>.Filter.Empty)
			.ToListAsync(cancellationToken)
			.ConfigureAwait(false);

		return documents.Select(d => d.ToInboxEntry());
	}

	/// <inheritdoc/>
	public async ValueTask<InboxStatistics> GetStatisticsAsync(CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var filter = Builders<MongoDbInboxDocument>.Filter;

		var total = await _collection.CountDocumentsAsync(filter.Empty, cancellationToken: cancellationToken).ConfigureAwait(false);
		var processed = await _collection
			.CountDocumentsAsync(filter.Eq(d => d.Status, (int)InboxStatus.Processed), cancellationToken: cancellationToken)
			.ConfigureAwait(false);
		var failed = await _collection
			.CountDocumentsAsync(filter.Eq(d => d.Status, (int)InboxStatus.Failed), cancellationToken: cancellationToken)
			.ConfigureAwait(false);
		var pending = await _collection.CountDocumentsAsync(
			filter.Or(
				filter.Eq(d => d.Status, (int)InboxStatus.Received),
				filter.Eq(d => d.Status, (int)InboxStatus.Processing)),
			cancellationToken: cancellationToken).ConfigureAwait(false);

		return new InboxStatistics
		{
			TotalEntries = (int)total,
			ProcessedEntries = (int)processed,
			FailedEntries = (int)failed,
			PendingEntries = (int)pending
		};
	}

	/// <inheritdoc/>
	public async ValueTask<int> CleanupAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken)
	{
		await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

		var cutoff = DateTimeOffset.UtcNow - retentionPeriod;
		var filter = Builders<MongoDbInboxDocument>.Filter.And(
			Builders<MongoDbInboxDocument>.Filter.Eq(d => d.Status, (int)InboxStatus.Processed),
			Builders<MongoDbInboxDocument>.Filter.Lt(d => d.ProcessedAt, cutoff));

		var result = await _collection.DeleteManyAsync(filter, cancellationToken).ConfigureAwait(false);

		LogCleanedUpEntries(_logger, (int)result.DeletedCount, null);
		return (int)result.DeletedCount;
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

	[LoggerMessage(DataMongoDbEventId.InboxStored, LogLevel.Debug,
		"Created inbox entry for message '{MessageId}' and handler '{HandlerType}'")]
	private static partial void LogCreatedEntry(ILogger logger, string messageId, string handlerType, Exception? exception);

	[LoggerMessage(DataMongoDbEventId.InboxMarkedComplete, LogLevel.Debug,
		"Marked inbox entry as processed for message '{MessageId}' and handler '{HandlerType}'")]
	private static partial void LogProcessedEntry(ILogger logger, string messageId, string handlerType, Exception? exception);

	[LoggerMessage(DataMongoDbEventId.InboxFirstProcessor, LogLevel.Debug,
		"TryMarkAsProcessed succeeded for message '{MessageId}' and handler '{HandlerType}'")]
	private static partial void LogTryMarkProcessedSuccess(ILogger logger, string messageId, string handlerType, Exception? exception);

	[LoggerMessage(DataMongoDbEventId.InboxAlreadyProcessed, LogLevel.Debug,
		"TryMarkAsProcessed detected duplicate for message '{MessageId}' and handler '{HandlerType}'")]
	private static partial void LogTryMarkProcessedDuplicate(ILogger logger, string messageId, string handlerType, Exception? exception);

	[LoggerMessage(DataMongoDbEventId.InboxMarkedFailed, LogLevel.Warning,
		"Marked inbox entry as failed for message '{MessageId}' and handler '{HandlerType}': {ErrorMessage}")]
	private static partial void LogFailedEntry(ILogger logger, string messageId, string handlerType, string errorMessage,
		Exception? exception);

	[LoggerMessage(DataMongoDbEventId.InboxCleanedUp, LogLevel.Information, "Cleaned up {Count} inbox entries")]
	private static partial void LogCleanedUpEntries(ILogger logger, int count, Exception? exception);

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
			_collection = _database.GetCollection<MongoDbInboxDocument>(_options.CollectionName);
		}

		// Create indexes
		var indexBuilder = Builders<MongoDbInboxDocument>.IndexKeys;

		// Index on handlerType for handler-specific queries
		var handlerIndex = new CreateIndexModel<MongoDbInboxDocument>(
			indexBuilder.Ascending(d => d.HandlerType));

		// Index on status for filtered queries
		var statusIndex = new CreateIndexModel<MongoDbInboxDocument>(
			indexBuilder.Ascending(d => d.Status));

		// TTL index on ProcessedAt for automatic cleanup
		if (_options.DefaultTtlSeconds > 0)
		{
			var ttlIndex = new CreateIndexModel<MongoDbInboxDocument>(
				indexBuilder.Ascending(d => d.ProcessedAt),
				new CreateIndexOptions { ExpireAfter = TimeSpan.FromSeconds(_options.DefaultTtlSeconds) });

			_ = await _collection.Indexes.CreateOneAsync(ttlIndex, cancellationToken: cancellationToken).ConfigureAwait(false);
		}

		_ = await _collection.Indexes.CreateManyAsync([handlerIndex, statusIndex], cancellationToken).ConfigureAwait(false);

		_initialized = true;
	}
}
