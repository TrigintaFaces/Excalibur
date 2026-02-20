// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;

using Excalibur.Data.Redis.Diagnostics;
using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

namespace Excalibur.Data.Redis.Inbox;

/// <summary>
/// Redis-based implementation of <see cref="IInboxStore"/>.
/// </summary>
/// <remarks>
/// Uses Redis SETNX (SET ... NX) for atomic first-writer-wins semantics.
/// Keys are formatted as: {KeyPrefix}:{messageId}:{handlerType}
/// Uses Redis TTL for automatic cleanup of processed entries.
/// </remarks>
public sealed partial class RedisInboxStore : IInboxStore, IAsyncDisposable
{
	private static readonly CompositeFormat EntryAlreadyExistsFormat =
		CompositeFormat.Parse(Resources.RedisInboxStore_EntryAlreadyExistsFormat);

	private static readonly CompositeFormat EntryNotFoundFormat =
		CompositeFormat.Parse(Resources.RedisInboxStore_EntryNotFoundFormat);

	private static readonly CompositeFormat EntryAlreadyProcessedFormat =
		CompositeFormat.Parse(Resources.RedisInboxStore_EntryAlreadyProcessedFormat);

	private readonly RedisInboxOptions _options;
	private readonly ILogger<RedisInboxStore> _logger;
	private ConnectionMultiplexer? _connection;
	private IDatabase? _database;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="RedisInboxStore"/> class.
	/// </summary>
	/// <param name="options">The Redis inbox options.</param>
	/// <param name="logger">The logger instance.</param>
	public RedisInboxStore(
		IOptions<RedisInboxOptions> options,
		ILogger<RedisInboxStore> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_options.Validate();
		_logger = logger;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="RedisInboxStore"/> class with an existing connection.
	/// </summary>
	/// <param name="connection">An existing Redis connection multiplexer.</param>
	/// <param name="options">The Redis inbox options.</param>
	/// <param name="logger">The logger instance.</param>
	public RedisInboxStore(
		ConnectionMultiplexer connection,
		IOptions<RedisInboxOptions> options,
		ILogger<RedisInboxStore> logger)
	{
		ArgumentNullException.ThrowIfNull(connection);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_connection = connection;
		_options = options.Value;
		_options.Validate();
		_logger = logger;
		_database = connection.GetDatabase(_options.DatabaseId);
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

		await EnsureConnectedAsync().ConfigureAwait(false);

		var entry = new InboxEntry(messageId, handlerType, messageType, payload, metadata);
		var key = GetKey(messageId, handlerType);
		var value = SerializeEntry(entry);

		// Use SETNX for atomic first-writer-wins
		var wasSet = await _database.StringSetAsync(
			key,
			value,
			when: When.NotExists).ConfigureAwait(false);

		if (!wasSet)
		{
			throw new InvalidOperationException(
				string.Format(
					CultureInfo.InvariantCulture,
					EntryAlreadyExistsFormat,
					messageId,
					handlerType));
		}

		// Set TTL if configured
		if (_options.DefaultTtlSeconds > 0)
		{
			_ = await _database.KeyExpireAsync(key, TimeSpan.FromSeconds(_options.DefaultTtlSeconds)).ConfigureAwait(false);
		}

		LogCreatedEntry(_logger, messageId, handlerType, null);
		return entry;
	}

	/// <inheritdoc/>
	public async ValueTask MarkProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		await EnsureConnectedAsync().ConfigureAwait(false);

		var key = GetKey(messageId, handlerType);
		var value = await _database.StringGetAsync(key).ConfigureAwait(false);

		if (value.IsNullOrEmpty)
		{
			throw new InvalidOperationException(
				string.Format(
					CultureInfo.InvariantCulture,
					EntryNotFoundFormat,
					messageId,
					handlerType));
		}

		var entry = DeserializeEntry(value);

		if (entry.Status == InboxStatus.Processed)
		{
			throw new InvalidOperationException(
				string.Format(
					CultureInfo.InvariantCulture,
					EntryAlreadyProcessedFormat,
					messageId,
					handlerType));
		}

		entry.MarkProcessed();

		_ = await _database.StringSetAsync(key, SerializeEntry(entry)).ConfigureAwait(false);

		// Update TTL if configured
		if (_options.DefaultTtlSeconds > 0)
		{
			_ = await _database.KeyExpireAsync(key, TimeSpan.FromSeconds(_options.DefaultTtlSeconds)).ConfigureAwait(false);
		}

		LogProcessedEntry(_logger, messageId, handlerType, null);
	}

	/// <inheritdoc/>
	public async ValueTask<bool> TryMarkAsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		await EnsureConnectedAsync().ConfigureAwait(false);

		var key = GetKey(messageId, handlerType);

		// Create a minimal entry for the processed record
		var entry = new InboxEntry
		{
			MessageId = messageId,
			HandlerType = handlerType,
			MessageType = "Unknown",
			Status = InboxStatus.Processed,
			ProcessedAt = DateTimeOffset.UtcNow
		};

		var value = SerializeEntry(entry);

		// Use SETNX for atomic first-writer-wins
		var wasSet = await _database.StringSetAsync(
			key,
			value,
			when: When.NotExists).ConfigureAwait(false);

		if (wasSet)
		{
			// Set TTL if configured
			if (_options.DefaultTtlSeconds > 0)
			{
				_ = await _database.KeyExpireAsync(key, TimeSpan.FromSeconds(_options.DefaultTtlSeconds)).ConfigureAwait(false);
			}

			LogTryMarkProcessedSuccess(_logger, messageId, handlerType, null);
			return true;
		}

		LogTryMarkProcessedDuplicate(_logger, messageId, handlerType, null);
		return false;
	}

	/// <inheritdoc/>
	public async ValueTask<bool> IsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		await EnsureConnectedAsync().ConfigureAwait(false);

		var key = GetKey(messageId, handlerType);
		var value = await _database.StringGetAsync(key).ConfigureAwait(false);

		if (value.IsNullOrEmpty)
		{
			return false;
		}

		var entry = DeserializeEntry(value);
		return entry.Status == InboxStatus.Processed;
	}

	/// <inheritdoc/>
	public async ValueTask<InboxEntry?> GetEntryAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		await EnsureConnectedAsync().ConfigureAwait(false);

		var key = GetKey(messageId, handlerType);
		var value = await _database.StringGetAsync(key).ConfigureAwait(false);

		if (value.IsNullOrEmpty)
		{
			return null;
		}

		return DeserializeEntry(value);
	}

	/// <inheritdoc/>
	public async ValueTask MarkFailedAsync(string messageId, string handlerType, string errorMessage, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ArgumentNullException.ThrowIfNull(errorMessage);

		await EnsureConnectedAsync().ConfigureAwait(false);

		var key = GetKey(messageId, handlerType);
		var value = await _database.StringGetAsync(key).ConfigureAwait(false);

		if (value.IsNullOrEmpty)
		{
			throw new InvalidOperationException(
				string.Format(
					CultureInfo.InvariantCulture,
					EntryNotFoundFormat,
					messageId,
					handlerType));
		}

		var entry = DeserializeEntry(value);
		entry.MarkFailed(errorMessage);

		_ = await _database.StringSetAsync(key, SerializeEntry(entry)).ConfigureAwait(false);

		LogFailedEntry(_logger, messageId, handlerType, errorMessage, null);
	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<InboxEntry>> GetFailedEntriesAsync(
		int maxRetries,
		DateTimeOffset? olderThan,
		int batchSize,
		CancellationToken cancellationToken)
	{
		await EnsureConnectedAsync().ConfigureAwait(false);

		var entries = new List<InboxEntry>();
		var pattern = $"{_options.KeyPrefix}:*";

		await foreach (var key in ScanKeysAsync(pattern).ConfigureAwait(false))
		{
			if (cancellationToken.IsCancellationRequested)
			{
				break;
			}

			var value = await _database.StringGetAsync(key).ConfigureAwait(false);
			if (value.IsNullOrEmpty)
			{
				continue;
			}

			var entry = DeserializeEntry(value);

			if (entry.Status == InboxStatus.Failed && entry.RetryCount < maxRetries)
			{
				if (!olderThan.HasValue || entry.LastAttemptAt < olderThan)
				{
					entries.Add(entry);
					if (entries.Count >= batchSize)
					{
						break;
					}
				}
			}
		}

		return entries;
	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<InboxEntry>> GetAllEntriesAsync(CancellationToken cancellationToken)
	{
		await EnsureConnectedAsync().ConfigureAwait(false);

		var entries = new List<InboxEntry>();
		var pattern = $"{_options.KeyPrefix}:*";

		await foreach (var key in ScanKeysAsync(pattern).ConfigureAwait(false))
		{
			if (cancellationToken.IsCancellationRequested)
			{
				break;
			}

			var value = await _database.StringGetAsync(key).ConfigureAwait(false);
			if (!value.IsNullOrEmpty)
			{
				entries.Add(DeserializeEntry(value));
			}
		}

		return entries;
	}

	/// <inheritdoc/>
	public async ValueTask<InboxStatistics> GetStatisticsAsync(CancellationToken cancellationToken)
	{
		await EnsureConnectedAsync().ConfigureAwait(false);

		var total = 0;
		var processed = 0;
		var failed = 0;
		var pending = 0;

		var pattern = $"{_options.KeyPrefix}:*";

		await foreach (var key in ScanKeysAsync(pattern).ConfigureAwait(false))
		{
			if (cancellationToken.IsCancellationRequested)
			{
				break;
			}

			var value = await _database.StringGetAsync(key).ConfigureAwait(false);
			if (value.IsNullOrEmpty)
			{
				continue;
			}

			var entry = DeserializeEntry(value);
			total++;

			switch (entry.Status)
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
					pending++;
					break;
			}
		}

		return new InboxStatistics { TotalEntries = total, ProcessedEntries = processed, FailedEntries = failed, PendingEntries = pending };
	}

	/// <inheritdoc/>
	public async ValueTask<int> CleanupAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken)
	{
		await EnsureConnectedAsync().ConfigureAwait(false);

		var cutoff = DateTimeOffset.UtcNow - retentionPeriod;
		var deleted = 0;
		var pattern = $"{_options.KeyPrefix}:*";

		await foreach (var key in ScanKeysAsync(pattern).ConfigureAwait(false))
		{
			if (cancellationToken.IsCancellationRequested)
			{
				break;
			}

			var value = await _database.StringGetAsync(key).ConfigureAwait(false);
			if (value.IsNullOrEmpty)
			{
				continue;
			}

			var entry = DeserializeEntry(value);

			if (entry.Status == InboxStatus.Processed && entry.ProcessedAt.HasValue && entry.ProcessedAt < cutoff)
			{
				if (await _database.KeyDeleteAsync(key).ConfigureAwait(false))
				{
					deleted++;
				}
			}
		}

		LogCleanedUpEntries(_logger, deleted, null);
		return deleted;
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		if (_connection != null)
		{
			await _connection.CloseAsync().ConfigureAwait(false);
			_connection.Dispose();
		}
	}

	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:Using RequiresDynamicCode member in AOT",
		Justification = "Inbox entries include dynamic metadata that requires runtime serialization.")]
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2026:Members annotated with RequiresUnreferencedCode may break with trimming",
		Justification = "Inbox entries include dynamic metadata that requires runtime serialization.")]
	private static string SerializeEntry(InboxEntry entry)
	{
		var document = new RedisInboxDocument
		{
			MessageId = entry.MessageId,
			HandlerType = entry.HandlerType,
			MessageType = entry.MessageType,
			Payload = entry.Payload,
			Metadata = new Dictionary<string, object>(entry.Metadata, StringComparer.Ordinal),
			ReceivedAt = entry.ReceivedAt,
			ProcessedAt = entry.ProcessedAt,
			Status = (int)entry.Status,
			LastError = entry.LastError,
			RetryCount = entry.RetryCount,
			LastAttemptAt = entry.LastAttemptAt,
			CorrelationId = entry.CorrelationId,
			TenantId = entry.TenantId,
			Source = entry.Source
		};

		return JsonSerializer.Serialize(document);
	}

	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:Using RequiresDynamicCode member in AOT",
		Justification = "Inbox entries include dynamic metadata that requires runtime deserialization.")]
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2026:Members annotated with RequiresUnreferencedCode may break with trimming",
		Justification = "Inbox entries include dynamic metadata that requires runtime deserialization.")]
	private static InboxEntry DeserializeEntry(string json)
	{
		var document = JsonSerializer.Deserialize<RedisInboxDocument>(json)
					   ?? throw new InvalidOperationException(Resources.RedisInboxStore_FailedToDeserializeEntry);

		var metadata = document.Metadata ?? new Dictionary<string, object>(StringComparer.Ordinal);

		return new InboxEntry
		{
			MessageId = document.MessageId,
			HandlerType = document.HandlerType,
			MessageType = document.MessageType,
			Payload = document.Payload ?? [],
			Metadata = metadata,
			ReceivedAt = document.ReceivedAt,
			ProcessedAt = document.ProcessedAt,
			Status = (InboxStatus)document.Status,
			LastError = document.LastError,
			RetryCount = document.RetryCount,
			LastAttemptAt = document.LastAttemptAt,
			CorrelationId = document.CorrelationId,
			TenantId = document.TenantId,
			Source = document.Source
		};
	}

	[LoggerMessage(DataRedisEventId.InboxEntryCreated, LogLevel.Debug,
		"Created inbox entry for message '{MessageId}' and handler '{HandlerType}'")]
	private static partial void LogCreatedEntry(ILogger logger, string messageId, string handlerType, Exception? exception);

	[LoggerMessage(DataRedisEventId.InboxEntryProcessed, LogLevel.Debug,
		"Marked inbox entry as processed for message '{MessageId}' and handler '{HandlerType}'")]
	private static partial void LogProcessedEntry(ILogger logger, string messageId, string handlerType, Exception? exception);

	[LoggerMessage(DataRedisEventId.InboxTryMarkProcessedSuccess, LogLevel.Debug,
		"TryMarkAsProcessed succeeded for message '{MessageId}' and handler '{HandlerType}'")]
	private static partial void LogTryMarkProcessedSuccess(ILogger logger, string messageId, string handlerType, Exception? exception);

	[LoggerMessage(DataRedisEventId.InboxTryMarkProcessedDuplicate, LogLevel.Debug,
		"TryMarkAsProcessed detected duplicate for message '{MessageId}' and handler '{HandlerType}'")]
	private static partial void LogTryMarkProcessedDuplicate(ILogger logger, string messageId, string handlerType, Exception? exception);

	[LoggerMessage(DataRedisEventId.InboxEntryFailed, LogLevel.Warning,
		"Marked inbox entry as failed for message '{MessageId}' and handler '{HandlerType}': {ErrorMessage}")]
	private static partial void LogFailedEntry(ILogger logger, string messageId, string handlerType, string errorMessage,
		Exception? exception);

	[LoggerMessage(DataRedisEventId.InboxCleanedUp, LogLevel.Information, "Cleaned up {Count} inbox entries")]
	private static partial void LogCleanedUpEntries(ILogger logger, int count, Exception? exception);

	private string GetKey(string messageId, string handlerType) =>
		$"{_options.KeyPrefix}:{messageId}:{handlerType}";

	private async Task EnsureConnectedAsync()
	{
		if (_database != null)
		{
			return;
		}

		var configOptions = ConfigurationOptions.Parse(_options.ConnectionString);
		configOptions.ConnectTimeout = _options.ConnectTimeoutMs;
		configOptions.SyncTimeout = _options.SyncTimeoutMs;
		configOptions.AbortOnConnectFail = _options.AbortOnConnectFail;
		configOptions.Ssl = _options.UseSsl;

		if (!string.IsNullOrEmpty(_options.Password))
		{
			configOptions.Password = _options.Password;
		}

		_connection = await ConnectionMultiplexer.ConnectAsync(configOptions).ConfigureAwait(false);
		_database = _connection.GetDatabase(_options.DatabaseId);
	}

	private async IAsyncEnumerable<RedisKey> ScanKeysAsync(string pattern)
	{
		var server = _connection.GetServers().FirstOrDefault();
		if (server == null)
		{
			yield break;
		}

		await foreach (var key in server.KeysAsync(_options.DatabaseId, pattern).ConfigureAwait(false))
		{
			yield return key;
		}
	}
}

/// <summary>
/// Internal document model for Redis serialization.
/// </summary>
internal sealed class RedisInboxDocument
{
	public string MessageId { get; set; } = string.Empty;
	public string HandlerType { get; set; } = string.Empty;
	public string MessageType { get; set; } = string.Empty;
	public byte[]? Payload { get; set; }
	public Dictionary<string, object>? Metadata { get; set; }
	public DateTimeOffset ReceivedAt { get; set; }
	public DateTimeOffset? ProcessedAt { get; set; }
	public int Status { get; set; }
	public string? LastError { get; set; }
	public int RetryCount { get; set; }
	public DateTimeOffset? LastAttemptAt { get; set; }
	public string? CorrelationId { get; set; }
	public string? TenantId { get; set; }
	public string? Source { get; set; }
}
