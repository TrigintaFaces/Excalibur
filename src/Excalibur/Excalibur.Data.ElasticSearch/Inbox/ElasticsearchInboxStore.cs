// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;

using Excalibur.Data.ElasticSearch.Diagnostics;
using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.ElasticSearch.Inbox;

/// <summary>
/// Elasticsearch-based implementation of <see cref="IInboxStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// Uses ES document ID = {messageId}_{handlerType} for atomic idempotent writes via OpType.Create.
/// Payloads are stored as Base64-encoded strings.
/// </para>
/// </remarks>
public sealed partial class ElasticsearchInboxStore : IInboxStore
{
	private readonly ElasticsearchClient _client;
	private readonly ElasticsearchInboxOptions _options;
	private readonly ILogger<ElasticsearchInboxStore> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="ElasticsearchInboxStore"/> class.
	/// </summary>
	/// <param name="client">The Elasticsearch client.</param>
	/// <param name="options">The inbox options.</param>
	/// <param name="logger">The logger instance.</param>
	public ElasticsearchInboxStore(
		ElasticsearchClient client,
		IOptions<ElasticsearchInboxOptions> options,
		ILogger<ElasticsearchInboxStore> logger)
	{
		_client = client ?? throw new ArgumentNullException(nameof(client));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

		var entry = new InboxEntry(messageId, handlerType, messageType, payload, metadata);
		var doc = ToDocument(entry);
		var docId = GetDocumentId(messageId, handlerType);

		var response = await _client.IndexAsync(
			doc,
			idx => idx
				.Index(_options.IndexName)
				.Id(docId)
				.OpType(OpType.Create)
				.Refresh(GetRefresh()),
			cancellationToken).ConfigureAwait(false);

		if (!response.IsValidResponse)
		{
			// Version conflict means document already exists
			if (response.ElasticsearchServerError?.Status == 409)
			{
				throw new InvalidOperationException(
					$"Inbox entry already exists for message '{messageId}' and handler '{handlerType}'.");
			}

			throw new InvalidOperationException(
				$"Failed to create inbox entry: {response.ElasticsearchServerError?.Error?.Reason ?? "Unknown error"}");
		}

		LogCreatedEntry(messageId, handlerType);
		return entry;
	}

	/// <inheritdoc/>
	public async ValueTask MarkProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		var docId = GetDocumentId(messageId, handlerType);
		var existing = await GetDocumentAsync(docId, cancellationToken).ConfigureAwait(false)
			?? throw new InvalidOperationException(
				$"Inbox entry not found for message '{messageId}' and handler '{handlerType}'.");

		if (existing.Status == (int)InboxStatus.Processed)
		{
			throw new InvalidOperationException(
				$"Inbox entry already processed for message '{messageId}' and handler '{handlerType}'.");
		}

		existing.Status = (int)InboxStatus.Processed;
		existing.ProcessedAt = DateTimeOffset.UtcNow;

		await UpdateDocumentAsync(docId, existing, cancellationToken).ConfigureAwait(false);
		LogProcessedEntry(messageId, handlerType);
	}

	/// <inheritdoc/>
	public async ValueTask<bool> TryMarkAsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		var doc = new ElasticsearchInboxDocument
		{
			MessageId = messageId,
			HandlerType = handlerType,
			MessageType = "Unknown",
			Status = (int)InboxStatus.Processed,
			ProcessedAt = DateTimeOffset.UtcNow,
			ReceivedAt = DateTimeOffset.UtcNow,
		};

		var docId = GetDocumentId(messageId, handlerType);

		var response = await _client.IndexAsync(
			doc,
			idx => idx
				.Index(_options.IndexName)
				.Id(docId)
				.OpType(OpType.Create)
				.Refresh(GetRefresh()),
			cancellationToken).ConfigureAwait(false);

		if (response.IsValidResponse)
		{
			LogTryMarkProcessedSuccess(messageId, handlerType);
			return true;
		}

		// 409 = already exists = duplicate
		if (response.ElasticsearchServerError?.Status == 409)
		{
			LogTryMarkProcessedDuplicate(messageId, handlerType);
			return false;
		}

		throw new InvalidOperationException(
			$"Failed to mark inbox entry: {response.ElasticsearchServerError?.Error?.Reason ?? "Unknown error"}");
	}

	/// <inheritdoc/>
	public async ValueTask<bool> IsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		var docId = GetDocumentId(messageId, handlerType);
		var doc = await GetDocumentAsync(docId, cancellationToken).ConfigureAwait(false);

		return doc is { Status: (int)InboxStatus.Processed };
	}

	/// <inheritdoc/>
	public async ValueTask<InboxEntry?> GetEntryAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		var docId = GetDocumentId(messageId, handlerType);
		var doc = await GetDocumentAsync(docId, cancellationToken).ConfigureAwait(false);

		return doc == null ? null : FromDocument(doc);
	}

	/// <inheritdoc/>
	public async ValueTask MarkFailedAsync(string messageId, string handlerType, string errorMessage, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ArgumentNullException.ThrowIfNull(errorMessage);

		var docId = GetDocumentId(messageId, handlerType);
		var existing = await GetDocumentAsync(docId, cancellationToken).ConfigureAwait(false)
			?? throw new InvalidOperationException(
				$"Inbox entry not found for message '{messageId}' and handler '{handlerType}'.");

		existing.Status = (int)InboxStatus.Failed;
		existing.LastError = errorMessage;
		existing.RetryCount++;
		existing.LastAttemptAt = DateTimeOffset.UtcNow;

		await UpdateDocumentAsync(docId, existing, cancellationToken).ConfigureAwait(false);
		LogFailedEntry(messageId, handlerType, errorMessage);
	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<InboxEntry>> GetFailedEntriesAsync(
		int maxRetries,
		DateTimeOffset? olderThan,
		int batchSize,
		CancellationToken cancellationToken)
	{
		var mustClauses = new List<Query>
		{
			new TermQuery(new Field("status")) { Value = (int)InboxStatus.Failed },
			new NumberRangeQuery(new Field("retryCount")) { Lt = maxRetries },
		};

		if (olderThan.HasValue)
		{
			mustClauses.Add(
				new DateRangeQuery(new Field("lastAttemptAt")) { Lt = (DateMath)olderThan.Value.DateTime });
		}

		var response = await _client.SearchAsync<ElasticsearchInboxDocument>(s => s
			.Index(_options.IndexName)
			.Size(batchSize)
			.Query(q => q.Bool(b => b.Must(mustClauses.ToArray()))),
			cancellationToken).ConfigureAwait(false);

		if (!response.IsValidResponse)
		{
			return [];
		}

		return response.Documents.Select(FromDocument);
	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<InboxEntry>> GetAllEntriesAsync(CancellationToken cancellationToken)
	{
		var response = await _client.SearchAsync<ElasticsearchInboxDocument>(s => s
			.Index(_options.IndexName)
			.Size(10000)
			.Query(q => q.MatchAll(new MatchAllQuery())),
			cancellationToken).ConfigureAwait(false);

		if (!response.IsValidResponse)
		{
			return [];
		}

		return response.Documents.Select(FromDocument);
	}

	/// <inheritdoc/>
	public async ValueTask<InboxStatistics> GetStatisticsAsync(CancellationToken cancellationToken)
	{
		var response = await _client.SearchAsync<ElasticsearchInboxDocument>(s => s
			.Index(_options.IndexName)
			.Size(10000)
			.Query(q => q.MatchAll(new MatchAllQuery())),
			cancellationToken).ConfigureAwait(false);

		if (!response.IsValidResponse)
		{
			return new InboxStatistics();
		}

		var docs = response.Documents;
		return new InboxStatistics
		{
			TotalEntries = docs.Count,
			ProcessedEntries = docs.Count(d => d.Status == (int)InboxStatus.Processed),
			FailedEntries = docs.Count(d => d.Status == (int)InboxStatus.Failed),
			PendingEntries = docs.Count(d => d.Status is (int)InboxStatus.Received or (int)InboxStatus.Processing),
		};
	}

	/// <inheritdoc/>
	public async ValueTask<int> CleanupAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken)
	{
		var cutoff = DateTimeOffset.UtcNow - retentionPeriod;

		var response = await _client.DeleteByQueryAsync<ElasticsearchInboxDocument>(
			static d => d
				.Indices("excalibur-inbox")
				.Query(q => q.MatchAll(new MatchAllQuery())),
			cancellationToken).ConfigureAwait(false);

		var deleted = (int)(response.Deleted ?? 0);
		LogCleanedUpEntries(deleted);
		return deleted;
	}

	private static string GetDocumentId(string messageId, string handlerType) =>
		$"{messageId}_{handlerType}";

	private Refresh GetRefresh() =>
		_options.RefreshPolicy == "true" ? Refresh.True
		: _options.RefreshPolicy == "false" ? Refresh.False
		: Refresh.WaitFor;

	private static ElasticsearchInboxDocument ToDocument(InboxEntry entry) =>
		new()
		{
			MessageId = entry.MessageId,
			HandlerType = entry.HandlerType,
			MessageType = entry.MessageType,
			PayloadBase64 = Convert.ToBase64String(entry.Payload),
			Metadata = new Dictionary<string, object>(entry.Metadata, StringComparer.Ordinal),
			ReceivedAt = entry.ReceivedAt,
			ProcessedAt = entry.ProcessedAt,
			Status = (int)entry.Status,
			LastError = entry.LastError,
			RetryCount = entry.RetryCount,
			LastAttemptAt = entry.LastAttemptAt,
			CorrelationId = entry.CorrelationId,
			TenantId = entry.TenantId,
			Source = entry.Source,
		};

	private static InboxEntry FromDocument(ElasticsearchInboxDocument doc) =>
		new()
		{
			MessageId = doc.MessageId,
			HandlerType = doc.HandlerType,
			MessageType = doc.MessageType,
			Payload = doc.PayloadBase64 != null ? Convert.FromBase64String(doc.PayloadBase64) : [],
			Metadata = doc.Metadata ?? new Dictionary<string, object>(StringComparer.Ordinal),
			ReceivedAt = doc.ReceivedAt,
			ProcessedAt = doc.ProcessedAt,
			Status = (InboxStatus)doc.Status,
			LastError = doc.LastError,
			RetryCount = doc.RetryCount,
			LastAttemptAt = doc.LastAttemptAt,
			CorrelationId = doc.CorrelationId,
			TenantId = doc.TenantId,
			Source = doc.Source,
		};

	private async Task<ElasticsearchInboxDocument?> GetDocumentAsync(string docId, CancellationToken cancellationToken)
	{
		var response = await _client.GetAsync<ElasticsearchInboxDocument>(
			_options.IndexName,
			docId,
			cancellationToken).ConfigureAwait(false);

		return response.IsValidResponse && response.Found ? response.Source : null;
	}

	private async Task UpdateDocumentAsync(string docId, ElasticsearchInboxDocument doc, CancellationToken cancellationToken)
	{
		var response = await _client.IndexAsync(
			doc,
			idx => idx
				.Index(_options.IndexName)
				.Id(docId)
				.Refresh(GetRefresh()),
			cancellationToken).ConfigureAwait(false);

		if (!response.IsValidResponse)
		{
			throw new InvalidOperationException(
				$"Failed to update inbox document: {response.ElasticsearchServerError?.Error?.Reason ?? "Unknown error"}");
		}
	}

	[LoggerMessage(DataElasticsearchEventId.DocumentIndexed, LogLevel.Debug,
		"Created inbox entry for message '{MessageId}' and handler '{HandlerType}'")]
	private partial void LogCreatedEntry(string messageId, string handlerType);

	[LoggerMessage(DataElasticsearchEventId.DocumentUpdated, LogLevel.Debug,
		"Marked inbox entry as processed for message '{MessageId}' and handler '{HandlerType}'")]
	private partial void LogProcessedEntry(string messageId, string handlerType);

	[LoggerMessage(DataElasticsearchEventId.DocumentRetrieved, LogLevel.Debug,
		"TryMarkAsProcessed succeeded for message '{MessageId}' and handler '{HandlerType}'")]
	private partial void LogTryMarkProcessedSuccess(string messageId, string handlerType);

	[LoggerMessage(DataElasticsearchEventId.DocumentExistsChecked, LogLevel.Debug,
		"TryMarkAsProcessed detected duplicate for message '{MessageId}' and handler '{HandlerType}'")]
	private partial void LogTryMarkProcessedDuplicate(string messageId, string handlerType);

	[LoggerMessage(DataElasticsearchEventId.VersionConflict, LogLevel.Warning,
		"Marked inbox entry as failed for message '{MessageId}' and handler '{HandlerType}': {ErrorMessage}")]
	private partial void LogFailedEntry(string messageId, string handlerType, string errorMessage);

	[LoggerMessage(DataElasticsearchEventId.BulkOperationCompleted, LogLevel.Information,
		"Cleaned up {Count} inbox entries")]
	private partial void LogCleanedUpEntries(int count);
}
