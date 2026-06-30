// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;

using Excalibur.Data.ElasticSearch.Diagnostics;
using Excalibur.Dispatch;
using Excalibur.Inbox.Observability;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Inbox.ElasticSearch;

/// <summary>
/// Elasticsearch-based implementation of <see cref="IInboxStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// Uses ES document ID = {messageId}_{handlerType} for atomic idempotent writes via OpType.Create.
/// Payloads are stored as Base64-encoded strings.
/// </para>
/// </remarks>
public sealed partial class ElasticsearchInboxStore : IInboxStore, IProcessingTrackingInboxStore, IClaimableInboxStore, IInboxStoreAdmin
{
	/// <summary>Bounded retries for the optimistic-concurrency conditional delete in <see cref="ReleaseAsync"/>.</summary>
	private const int ReleaseMaxRetries = 5;

	/// <summary>
	/// Test-only seam: when non-null, invoked once inside <see cref="ReleaseAsync"/> in the window between
	/// the status read and the conditional delete, so a test can deterministically interleave a concurrent
	/// finalize and exercise the conditional-delete guard. Always <see langword="null"/> in production
	/// (single null-check ⇒ zero overhead).
	/// </summary>
	internal Func<CancellationToken, Task>? ReleaseRaceHookForTests { get; set; }

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

		using var activity = InboxActivitySource.StartCreateEntryActivity(messageId, handlerType);

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

		using var activity = InboxActivitySource.StartMarkProcessedActivity(messageId, handlerType);

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
	public async ValueTask MarkProcessingAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		var docId = GetDocumentId(messageId, handlerType);

		// Durably persist the in-flight Processing status (and the LastAttemptAt stamp the stuck-processing
		// timeout reads) BEFORE handler execution, so a concurrent delivery observes Processing via
		// GetEntryAsync and is skipped by the at-most-once guard.
		var existing = await GetDocumentAsync(docId, cancellationToken).ConfigureAwait(false)
			?? throw new InvalidOperationException(
				$"Inbox entry not found for message '{messageId}' and handler '{handlerType}'.");

		existing.Status = (int)InboxStatus.Processing;
		existing.LastAttemptAt = DateTimeOffset.UtcNow;

		await UpdateDocumentAsync(docId, existing, cancellationToken).ConfigureAwait(false);
		LogProcessingEntry(messageId, handlerType);
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
	public async ValueTask<bool> TryClaimAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		// Atomic first-writer-wins claim into the NON-TERMINAL Processing state via OpType.Create: the create
		// fails with a 409 conflict on an existing doc (already claimed/processed) => not claimed. Finalized via
		// MarkProcessedAsync, removed via ReleaseAsync.
		var doc = new ElasticsearchInboxDocument
		{
			MessageId = messageId,
			HandlerType = handlerType,
			MessageType = "Unknown",
			Status = (int)InboxStatus.Processing,
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

		// 409 = already exists = already claimed/processed = duplicate.
		if (response.ElasticsearchServerError?.Status == 409)
		{
			LogTryMarkProcessedDuplicate(messageId, handlerType);
			return false;
		}

		throw new InvalidOperationException(
			$"Failed to claim inbox entry: {response.ElasticsearchServerError?.Error?.Reason ?? "Unknown error"}");
	}

	/// <inheritdoc/>
	public async ValueTask ReleaseAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		var docId = GetDocumentId(messageId, handlerType);

		// Atomic delete-unless-Processed. Capture the document's optimistic-concurrency tokens
		// (_seq_no/_primary_term) on read and issue a CONDITIONAL delete (IfSeqNo/IfPrimaryTerm). A
		// concurrent MarkProcessed bumps the version, so our delete fails with a version conflict instead of
		// removing a now-finalized entry — we then re-read and no-op if it has become Processed. This closes
		// the read-then-delete race the plain delete left open.
		for (var attempt = 0; attempt < ReleaseMaxRetries; attempt++)
		{
			var get = await _client.GetAsync<ElasticsearchInboxDocument>(
				_options.IndexName, docId, cancellationToken).ConfigureAwait(false);

			if (!get.IsValidResponse || !get.Found || get.Source is null
				|| get.Source.Status == (int)InboxStatus.Processed)
			{
				// Absent or finalized — never delete.
				return;
			}

			// Test-only seam (null in production): lets a test interleave a concurrent finalize in the
			// read-then-delete window so the conditional-delete guard can be exercised deterministically.
			if (ReleaseRaceHookForTests is { } raceHook)
			{
				await raceHook(cancellationToken).ConfigureAwait(false);
			}

			var deleteResponse = await _client.DeleteAsync(
				new DeleteRequest(_options.IndexName, docId)
				{
					IfSeqNo = get.SeqNo,
					IfPrimaryTerm = get.PrimaryTerm,
				},
				cancellationToken).ConfigureAwait(false);

			if (deleteResponse.IsValidResponse)
			{
				return;
			}

			// A 409 means another writer changed the doc between our read and conditional delete — re-read
			// and re-evaluate (it may now be Processed → no-op). Any other failure is not retriable here.
			if (deleteResponse.ElasticsearchServerError?.Status != 409)
			{
				return;
			}
		}
	}

	/// <inheritdoc/>
	public async ValueTask<bool> IsProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);

		using var activity = InboxActivitySource.StartExistsActivity(messageId, handlerType);

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

		using var activity = InboxActivitySource.StartMarkFailedActivity(messageId, handlerType);

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
	public async ValueTask MarkFailedAsync(string messageId, string handlerType, string errorMessage, int retryCount, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(handlerType);
		ArgumentNullException.ThrowIfNull(errorMessage);

		using var activity = InboxActivitySource.StartMarkFailedActivity(messageId, handlerType);

		var docId = GetDocumentId(messageId, handlerType);
		var existing = await GetDocumentAsync(docId, cancellationToken).ConfigureAwait(false)
			?? throw new InvalidOperationException(
				$"Inbox entry not found for message '{messageId}' and handler '{handlerType}'.");

		existing.Status = (int)InboxStatus.Failed;
		existing.LastError = errorMessage;

		// Set the retry count EXACTLY (no increment) so a transient short-circuit leaves the entry
		// re-admittable without consuming a delivery attempt (FR-4).
		existing.RetryCount = retryCount;
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
			new TermQuery { Field = "status", Value = (int)InboxStatus.Failed },
			new NumberRangeQuery("retryCount") { Lt = maxRetries },
		};

		if (olderThan.HasValue)
		{
			mustClauses.Add(
				new DateRangeQuery("lastAttemptAt") { Lt = (DateMath)olderThan.Value.DateTime });
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
		// Compute statistics with server-side counts rather than materializing up to 10k documents
		// into memory and aggregating client-side.
		var total = await CountAsync(new MatchAllQuery(), cancellationToken).ConfigureAwait(false);
		var processed = await CountAsync(
			new TermQuery { Field = "status", Value = (int)InboxStatus.Processed }, cancellationToken).ConfigureAwait(false);
		var failed = await CountAsync(
			new TermQuery { Field = "status", Value = (int)InboxStatus.Failed }, cancellationToken).ConfigureAwait(false);
		var received = await CountAsync(
			new TermQuery { Field = "status", Value = (int)InboxStatus.Received }, cancellationToken).ConfigureAwait(false);
		var processing = await CountAsync(
			new TermQuery { Field = "status", Value = (int)InboxStatus.Processing }, cancellationToken).ConfigureAwait(false);

		return new InboxStatistics
		{
			TotalEntries = total,
			ProcessedEntries = processed,
			FailedEntries = failed,
			PendingEntries = received + processing,
		};
	}

	/// <summary>
	/// Returns the server-side document count matching <paramref name="query"/> without materializing documents.
	/// </summary>
	private async ValueTask<int> CountAsync(Query query, CancellationToken cancellationToken)
	{
		var request = new CountRequest(_options.IndexName) { Query = query };
		var response = await _client.CountAsync(request, cancellationToken).ConfigureAwait(false);
		return response.IsValidResponse ? (int)response.Count : 0;
	}

	/// <inheritdoc/>
	public async ValueTask<int> CleanupAsync(DateTimeOffset olderThan, CancellationToken cancellationToken)
	{
		using var activity = InboxActivitySource.StartCleanupActivity();

		// Strictly older-than cutoff: only entries received before `olderThan` are deleted.
		// An entry received exactly at `olderThan` is retained (EC-5). Previously this issued a
		// MatchAll query that deleted every inbox document regardless of age (FR-4 data-loss bug).
		var cutoff = DateMath.Anchored(olderThan.UtcDateTime);

		var response = await _client.DeleteByQueryAsync<ElasticsearchInboxDocument>(
			d => d
				.Indices(_options.IndexName)
				.Query(q => q
					.Range(r => r
						.DateRange(dr => dr
							.Field(f => f.ReceivedAt)
							.Lt(cutoff)))),
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

	[LoggerMessage(DataElasticsearchEventId.DocumentProcessing, LogLevel.Debug,
		"Marked inbox entry as processing for message '{MessageId}' and handler '{HandlerType}'")]
	private partial void LogProcessingEntry(string messageId, string handlerType);

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
