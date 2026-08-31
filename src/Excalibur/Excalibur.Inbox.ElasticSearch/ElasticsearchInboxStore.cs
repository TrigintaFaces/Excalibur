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
/// Uses an ES document ID composed from the tenant, message id, and handler type for atomic
/// idempotent writes via OpType.Create. The composition is injective, so distinct entries can never
/// share a document and be mistaken for duplicates of each other.
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
	private readonly ITenantContext _tenantContext;
	/// <summary>
	/// Gets the tenant term this store runs under, resolved in one place so every statement it builds binds
	/// the same value. The context is a required dependency, so the term is decided identically on every
	/// path: the store cannot resolve one partition on write and a different one on read.
	/// </summary>
	private KeyedTenantPartition CurrentTenantPartition =>
		KeyedTenantPartition.FromContext(_tenantContext);


	/// <summary>
	/// Initializes a new instance of the <see cref="ElasticsearchInboxStore"/> class.
	/// </summary>
	/// <param name="client">The Elasticsearch client.</param>
	/// <param name="options">The inbox options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	public ElasticsearchInboxStore(
		ElasticsearchClient client,
		IOptions<ElasticsearchInboxOptions> options,
		ILogger<ElasticsearchInboxStore> logger,
		ITenantContext tenantContext)
	{
		_client = client ?? throw new ArgumentNullException(nameof(client));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		ArgumentNullException.ThrowIfNull(tenantContext);
		_tenantContext = tenantContext;
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

		// Processed is absorbing: refuse rather than demote a finalized entry back to Processing, which
		// would re-admit the message and run the handler again.
		if (existing.Status == (int)InboxStatus.Processed)
		{
			return;
		}

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
			LogTryClaimSuccess(messageId, handlerType);
			return true;
		}

		// 409 = already exists = already claimed/processed = duplicate.
		if (response.ElasticsearchServerError?.Status == 409)
		{
			LogTryClaimDuplicate(messageId, handlerType);
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

		// Processed is absorbing: refuse rather than demote a finalized entry to Failed, which would
		// make it re-admittable and run the handler again.
		if (existing.Status == (int)InboxStatus.Processed)
		{
			return;
		}

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

		// Processed is absorbing: refuse rather than demote a finalized entry to Failed, which would
		// make it re-admittable and run the handler again.
		if (existing.Status == (int)InboxStatus.Processed)
		{
			return;
		}

		existing.Status = (int)InboxStatus.Failed;
		existing.LastError = errorMessage;

		// Set the retry count EXACTLY (no increment) so a transient short-circuit leaves the entry
		// re-admittable without consuming a delivery attempt.
		existing.RetryCount = retryCount;
		existing.LastAttemptAt = DateTimeOffset.UtcNow;

		await UpdateDocumentAsync(docId, existing, cancellationToken).ConfigureAwait(false);
		LogFailedEntry(messageId, handlerType, errorMessage);
	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<InboxEntry>> GetAllTenantsFailedEntriesAsync(
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
	public async ValueTask<IEnumerable<InboxEntry>> GetAllTenantsEntriesAsync(CancellationToken cancellationToken)
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
	public async ValueTask<InboxStatistics> GetAllTenantsStatisticsAsync(CancellationToken cancellationToken)
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
	public async ValueTask<int> CleanupAllTenantsProcessedEntriesAsync(DateTimeOffset olderThan, CancellationToken cancellationToken)
	{
		using var activity = InboxActivitySource.StartCleanupActivity();

		// Strictly older-than cutoff: only entries PROCESSED before `olderThan` are deleted. An entry
		// processed exactly at `olderThan` is retained. Previously this issued a MatchAll query
		// that deleted every inbox document regardless of age, which was a data-loss bug.
		//
		// Two further conditions, both load-bearing, both previously absent:
		//
		//   status = Processed  — retention removes entries whose work is DONE. Without it, cleanup also
		//                         deleted Failed entries (dropping the record of work that still needs
		//                         attention) and Pending ones (dropping the deduplication record, so the
		//                         message would be processed a second time on redelivery — the one thing
		//                         an inbox exists to prevent).
		//   range on ProcessedAt — the age that matters is when the entry was completed, not when it
		//                         arrived. Keying on ReceivedAt deletes a long-running or recently-retried
		//                         entry purely for having arrived early.
		//
		// This is the predicate the SQL providers already use:
		//   DELETE ... WHERE status = @ProcessedStatus AND processed_at < @CutoffDate
		var cutoff = DateMath.Anchored(olderThan.UtcDateTime);

		var response = await _client.DeleteByQueryAsync<ElasticsearchInboxDocument>(
			d => d
				.Indices(_options.IndexName)
				.Query(q => q
					.Bool(b => b
						.Filter(
							f => f.Term(t => t.Field(doc => doc.Status).Value((int)InboxStatus.Processed)),
							f => f.Range(r => r
								.DateRange(dr => dr
									.Field(doc => doc.ProcessedAt)
									.Lt(cutoff)))))),
			cancellationToken).ConfigureAwait(false);

		var deleted = (int)(response.Deleted ?? 0);
		LogCleanedUpEntries(deleted);
		return deleted;
	}

	/// <summary>
	/// Composes the deduplication document id for an entry, discriminated by the ambient tenant.
	/// </summary>
	/// <remarks>
	/// The tenant term is part of the id, not merely a field on the document. Carrying TenantId as an
	/// attribute while keying on (messageId, handlerType) leaves the dedup decision tenant-blind: two
	/// tenants processing messages that share a message id resolve to the same document, so the second
	/// is treated as a duplicate and silently dropped. That is a cross-tenant isolation breach and a
	/// message-loss bug, and it fails on the success path where nothing prompts an investigation.
	/// <para>
	/// Every call site routes through here, so the write id and the lookup id cannot drift apart.
	/// </para>
	/// </remarks>
	private string GetDocumentId(string messageId, string handlerType) =>
		ComposeDocumentId(CurrentTenantPartition.TenantId, messageId, handlerType);

	/// <summary>
	/// The store's document-id contract, expressed as a pure function of its three terms so it can be
	/// exercised directly. It delegates to the shared injective composition: putting the tenant term in
	/// the id is only sound if the composition is unambiguous, and joining on a separator the terms may
	/// themselves contain is not — distinct entries render one id and the later message is dropped as a
	/// duplicate that never existed.
	/// </summary>
	/// <param name="tenantId">The tenant term the entry belongs to.</param>
	/// <param name="messageId">The message identifier being deduplicated.</param>
	/// <param name="handlerType">The handler the message is being deduplicated for.</param>
	/// <returns>The document id for the entry.</returns>
	internal static string ComposeDocumentId(string tenantId, string messageId, string handlerType)
	{
		var documentId = InboxDocumentKey.Compose(tenantId, messageId, handlerType);
		InboxDocumentKey.ThrowIfExceedsIdLimit(documentId, MaxDocumentIdUtf8Bytes, "Elasticsearch");
		return documentId;
	}

	/// <summary>
	/// Elasticsearch refuses a document <c>_id</c> longer than 512 bytes. The limit is checked when the id
	/// is composed rather than left to the server, so the failure names the cause instead of arriving as a
	/// generic rejection on the write path — and so it surfaces identically on the read path, where a
	/// server-side limit would not have been consulted at all.
	/// </summary>
	private const int MaxDocumentIdUtf8Bytes = 512;

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

	[LoggerMessage(DataElasticsearchEventId.InboxTryClaimSuccess, LogLevel.Debug,
		"TryClaim succeeded for message '{MessageId}' and handler '{HandlerType}'")]
	private partial void LogTryClaimSuccess(string messageId, string handlerType);

	[LoggerMessage(DataElasticsearchEventId.InboxTryClaimDuplicate, LogLevel.Debug,
		"TryClaim detected duplicate for message '{MessageId}' and handler '{HandlerType}'")]
	private partial void LogTryClaimDuplicate(string messageId, string handlerType);

	[LoggerMessage(DataElasticsearchEventId.VersionConflict, LogLevel.Warning,
		"Marked inbox entry as failed for message '{MessageId}' and handler '{HandlerType}': {ErrorMessage}")]
	private partial void LogFailedEntry(string messageId, string handlerType, string errorMessage);

	[LoggerMessage(DataElasticsearchEventId.BulkOperationCompleted, LogLevel.Information,
		"Cleaned up {Count} inbox entries")]
	private partial void LogCleanedUpEntries(int count);
}
