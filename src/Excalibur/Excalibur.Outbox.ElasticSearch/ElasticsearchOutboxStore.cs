// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;

using Excalibur.Data.ElasticSearch.Diagnostics;
using Excalibur.Dispatch;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Outbox.ElasticSearch;

/// <summary>
/// Elasticsearch-based implementation of <see cref="IOutboxStore"/> and <see cref="IOutboxStoreAdmin"/>.
/// </summary>
/// <remarks>
/// <para>
/// Uses ES document ID = message ID for direct access.
/// Messages are sorted by priority and creation time for ordered retrieval.
/// </para>
/// </remarks>
public sealed partial class ElasticsearchOutboxStore : IOutboxStore, IOutboxStoreAdmin, IDeadLetterableOutboxStore, IAsyncDisposable
{
	private readonly ElasticsearchClient _client;
	private readonly ElasticsearchOutboxOptions _options;
	private readonly ILogger<ElasticsearchOutboxStore> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="ElasticsearchOutboxStore"/> class.
	/// </summary>
	/// <param name="client">The Elasticsearch client.</param>
	/// <param name="options">The outbox options.</param>
	/// <param name="logger">The logger instance.</param>
	public ElasticsearchOutboxStore(
		ElasticsearchClient client,
		IOptions<ElasticsearchOutboxOptions> options,
		ILogger<ElasticsearchOutboxStore> logger)
	{
		_client = client ?? throw new ArgumentNullException(nameof(client));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc/>
	public async ValueTask StageMessageAsync(OutboundMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		var doc = ToDocument(message);

		var response = await _client.IndexAsync(
			doc,
			idx => idx
				.Index(_options.IndexName)
				.Id(message.Id)
				.OpType(OpType.Create)
				.Refresh(GetRefresh()),
			cancellationToken).ConfigureAwait(false);

		if (!response.IsValidResponse)
		{
			if (response.ElasticsearchServerError?.Status == 409)
			{
				throw new InvalidOperationException($"Outbox message already exists with ID '{message.Id}'.");
			}

			throw new InvalidOperationException(
				$"Failed to stage outbox message: {response.ElasticsearchServerError?.Error?.Reason ?? "Unknown error"}");
		}

		LogMessageStaged(message.Id, message.MessageType, message.Destination);
	}

	/// <inheritdoc/>
	[UnconditionalSuppressMessage(
		"AOT", "IL3050:Using RequiresDynamicCode member in AOT",
		Justification = "Outbox payloads use runtime serialization for message types.")]
	[UnconditionalSuppressMessage(
		"Trimming", "IL2026:Members annotated with RequiresUnreferencedCode may break with trimming",
		Justification = "Outbox payloads use runtime serialization for message types.")]
	public async ValueTask EnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);

		var messageType = message.GetType().FullName ?? message.GetType().Name;
		var payload = JsonSerializer.SerializeToUtf8Bytes(message, message.GetType());

		var outbound = OutboundMessage.FromContext(messageType, payload, messageType, context);

		await StageMessageAsync(outbound, cancellationToken).ConfigureAwait(false);
		LogMessageEnqueued(outbound.Id, messageType);
	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize, CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);

		var now = DateTimeOffset.UtcNow;

		// Get staged messages + scheduled messages that are now due
		var searchRequest = new SearchRequest(_options.IndexName)
		{
			Size = batchSize,
			Sort =
			[
				new SortOptions { Field = new FieldSort("priority") { Order = SortOrder.Asc } },
				new SortOptions { Field = new FieldSort("createdAt") { Order = SortOrder.Asc } },
			],
			Query = new BoolQuery
			{
				Should =
				[
					// Immediate (unscheduled) staged messages: status == Staged AND no scheduledAt.
					new BoolQuery
					{
						Must = [new TermQuery { Field = "status", Value = (int)OutboxStatus.Staged }],
						MustNot = [new ExistsQuery { Field = new Field("scheduledAt") }],
					},
					// Scheduled staged messages that are now due: status == Staged AND scheduledAt <= now.
					new BoolQuery
					{
						Must =
						[
							new TermQuery { Field = "status", Value = (int)OutboxStatus.Staged },
							new DateRangeQuery("scheduledAt") { Lte = (DateMath)now.DateTime },
						],
					},
				],
				MinimumShouldMatch = 1,
			},
		};

		var response = await _client.SearchAsync<ElasticsearchOutboxDocument>(
			searchRequest, cancellationToken).ConfigureAwait(false);

		if (!response.IsValidResponse)
		{
			return [];
		}

		return response.Documents.Select(FromDocument);
	}

	/// <inheritdoc/>
	public async ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

		var existing = await GetOutboxDocumentAsync(messageId, cancellationToken).ConfigureAwait(false)
			?? throw new InvalidOperationException($"Outbox message not found with ID '{messageId}'.");

		if (existing.Status == (int)OutboxStatus.Sent)
		{
			throw new InvalidOperationException($"Outbox message already sent with ID '{messageId}'.");
		}

		existing.Status = (int)OutboxStatus.Sent;
		existing.SentAt = DateTimeOffset.UtcNow;

		await UpdateOutboxDocumentAsync(messageId, existing, cancellationToken).ConfigureAwait(false);
		LogMessageSent(messageId);
	}

	/// <inheritdoc/>
	public async ValueTask MarkFailedAsync(string messageId, string errorMessage, int retryCount, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentNullException.ThrowIfNull(errorMessage);

		var existing = await GetOutboxDocumentAsync(messageId, cancellationToken).ConfigureAwait(false);
		if (existing == null)
		{
			return; // Silent return per conformance tests
		}

		existing.Status = (int)OutboxStatus.Failed;
		existing.LastError = errorMessage;
		existing.RetryCount = retryCount;
		existing.LastAttemptAt = DateTimeOffset.UtcNow;

		await UpdateOutboxDocumentAsync(messageId, existing, cancellationToken).ConfigureAwait(false);
		LogMessageFailed(messageId, errorMessage, retryCount);
	}

	/// <inheritdoc/>
	public async ValueTask MarkDeadLetteredAsync(string messageId, string reason, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentNullException.ThrowIfNull(reason);

		var existing = await GetOutboxDocumentAsync(messageId, cancellationToken).ConfigureAwait(false);
		if (existing == null)
		{
			return; // Silent return — message may have been cleaned up
		}

		existing.Status = (int)OutboxStatus.DeadLettered;
		existing.LastError = reason;
		existing.LastAttemptAt = DateTimeOffset.UtcNow;

		await UpdateOutboxDocumentAsync(messageId, existing, cancellationToken).ConfigureAwait(false);
		_logger.LogWarning("Marked outbox message {MessageId} as dead-lettered: {Reason}", messageId, reason);
	}

	/// <inheritdoc/>
	public async ValueTask<IEnumerable<OutboundMessage>> GetFailedMessagesAsync(
		int maxRetries,
		DateTimeOffset? olderThan,
		int batchSize,
		CancellationToken cancellationToken)
	{
		var mustClauses = new List<Query>
		{
			new TermQuery { Field = "status", Value = (int)OutboxStatus.Failed },
		};

		if (maxRetries > 0)
		{
			mustClauses.Add(new NumberRangeQuery("retryCount") { Lt = maxRetries });
		}

		if (olderThan.HasValue)
		{
			mustClauses.Add(new DateRangeQuery("lastAttemptAt") { Lt = (DateMath)olderThan.Value.DateTime });
		}

		var response = await _client.SearchAsync<ElasticsearchOutboxDocument>(s => s
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
	public async ValueTask<IEnumerable<OutboundMessage>> GetScheduledMessagesAsync(
		DateTimeOffset scheduledBefore,
		int batchSize,
		CancellationToken cancellationToken)
	{
		var searchRequest = new SearchRequest(_options.IndexName)
		{
			Size = batchSize,
			Query = new BoolQuery
			{
				Must =
				[
					new ExistsQuery { Field = new Field("scheduledAt") },
					new DateRangeQuery("scheduledAt") { Lte = (DateMath)scheduledBefore.DateTime },
					new TermQuery { Field = "status", Value = (int)OutboxStatus.Staged },
				],
			},
		};

		var response = await _client.SearchAsync<ElasticsearchOutboxDocument>(
			searchRequest, cancellationToken).ConfigureAwait(false);

		if (!response.IsValidResponse)
		{
			return [];
		}

		return response.Documents.Select(FromDocument);
	}

	/// <inheritdoc/>
	public async ValueTask<int> CleanupSentMessagesAsync(DateTimeOffset olderThan, int batchSize, CancellationToken cancellationToken)
	{
		// Bounded cleanup: delete ONLY Sent documents whose sentAt is strictly older than the cutoff,
		// on the CONFIGURED index. Previously this issued a MatchAll DeleteByQuery against a hardcoded
		// "excalibur-outbox" index literal, deleting the entire live outbox (Staged + recent Sent
		// included) regardless of olderThan — a data-loss bug (FR MS-A1). The status + sentAt-range
		// predicate makes "delete an unsent or recent document" inexpressible by this path.
		var deleteRequest = new DeleteByQueryRequest(_options.IndexName)
		{
			Query = new BoolQuery
			{
				Must =
				[
					new TermQuery { Field = "status", Value = (int)OutboxStatus.Sent },
					new DateRangeQuery("sentAt") { Lt = (DateMath)olderThan.UtcDateTime },
				],
			},
		};

		var response = await _client.DeleteByQueryAsync(
			deleteRequest, cancellationToken).ConfigureAwait(false);

		var deleted = (int)(response.Deleted ?? 0);
		LogMessagesCleanedUp(deleted, olderThan);
		return deleted;
	}

	/// <inheritdoc/>
	public async ValueTask<OutboxStatistics> GetStatisticsAsync(CancellationToken cancellationToken)
	{
		var now = DateTimeOffset.UtcNow;

		// Compute statistics with server-side counts and a single oldest-document lookup per status,
		// rather than materializing up to 10k documents into memory and aggregating client-side.
		var staged = await CountAsync(
			new TermQuery { Field = "status", Value = (int)OutboxStatus.Staged }, cancellationToken).ConfigureAwait(false);
		var sent = await CountAsync(
			new TermQuery { Field = "status", Value = (int)OutboxStatus.Sent }, cancellationToken).ConfigureAwait(false);
		var failed = await CountAsync(
			new TermQuery { Field = "status", Value = (int)OutboxStatus.Failed }, cancellationToken).ConfigureAwait(false);
		var scheduled = await CountAsync(
			new BoolQuery
			{
				Must =
				[
					new TermQuery { Field = "status", Value = (int)OutboxStatus.Staged },
					new ExistsQuery { Field = new Field("scheduledAt") },
				],
			},
			cancellationToken).ConfigureAwait(false);

		var oldestStaged = await GetOldestCreatedAtAsync(OutboxStatus.Staged, cancellationToken).ConfigureAwait(false);
		var oldestFailed = await GetOldestCreatedAtAsync(OutboxStatus.Failed, cancellationToken).ConfigureAwait(false);

		return new OutboxStatistics
		{
			StagedMessageCount = staged,
			SendingMessageCount = 0,
			SentMessageCount = sent,
			FailedMessageCount = failed,
			ScheduledMessageCount = scheduled,
			OldestUnsentMessageAge = oldestStaged.HasValue ? now - oldestStaged.Value : null,
			OldestFailedMessageAge = oldestFailed.HasValue ? now - oldestFailed.Value : null,
			CapturedAt = now,
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

	/// <summary>
	/// Returns the creation timestamp of the oldest document in the given status, fetching only that
	/// single document (size 1, sorted ascending) instead of paging the whole status partition.
	/// </summary>
	private async ValueTask<DateTimeOffset?> GetOldestCreatedAtAsync(OutboxStatus status, CancellationToken cancellationToken)
	{
		var request = new SearchRequest<ElasticsearchOutboxDocument>(_options.IndexName)
		{
			Size = 1,
			Query = new TermQuery { Field = "status", Value = (int)status },
			Sort = [new SortOptions { Field = new FieldSort("createdAt") { Order = SortOrder.Asc } }],
		};

		var response = await _client.SearchAsync<ElasticsearchOutboxDocument>(request, cancellationToken).ConfigureAwait(false);
		if (!response.IsValidResponse)
		{
			return null;
		}

		var oldest = response.Documents.FirstOrDefault();
		return oldest is null ? null : oldest.CreatedAt;
	}

	private Refresh GetRefresh() =>
		_options.RefreshPolicy == "true" ? Refresh.True
		: _options.RefreshPolicy == "false" ? Refresh.False
		: Refresh.WaitFor;

	private static ElasticsearchOutboxDocument ToDocument(OutboundMessage message) =>
		new()
		{
			Id = message.Id,
			MessageType = message.MessageType,
			PayloadBase64 = Convert.ToBase64String(message.Payload),
			Destination = message.Destination,
			CreatedAt = message.CreatedAt,
			Status = (int)message.Status,
			Priority = message.Priority,
			RetryCount = message.RetryCount,
			CorrelationId = message.CorrelationId,
			CausationId = message.CausationId,
			TenantId = message.TenantId,
			LastError = message.LastError,
			ScheduledAt = message.ScheduledAt,
			SentAt = message.SentAt,
			LastAttemptAt = message.LastAttemptAt,
			Headers = message.Headers.Count > 0
				? new Dictionary<string, object>(message.Headers, StringComparer.Ordinal)
				: null,
		};

	private static OutboundMessage FromDocument(ElasticsearchOutboxDocument doc) =>
		new()
		{
			Id = doc.Id,
			MessageType = doc.MessageType,
			Payload = doc.PayloadBase64 != null ? Convert.FromBase64String(doc.PayloadBase64) : [],
			Destination = doc.Destination,
			CreatedAt = doc.CreatedAt,
			Status = (OutboxStatus)doc.Status,
			Priority = doc.Priority,
			RetryCount = doc.RetryCount,
			CorrelationId = doc.CorrelationId,
			CausationId = doc.CausationId,
			TenantId = doc.TenantId,
			LastError = doc.LastError,
			ScheduledAt = doc.ScheduledAt,
			SentAt = doc.SentAt,
			LastAttemptAt = doc.LastAttemptAt,
			Headers = doc.Headers is { Count: > 0 }
				? new Dictionary<string, object>(doc.Headers, StringComparer.Ordinal)
				: new Dictionary<string, object>(StringComparer.Ordinal),
		};

	private async Task<ElasticsearchOutboxDocument?> GetOutboxDocumentAsync(string messageId, CancellationToken cancellationToken)
	{
		var response = await _client.GetAsync<ElasticsearchOutboxDocument>(
			_options.IndexName,
			messageId,
			cancellationToken).ConfigureAwait(false);

		return response.IsValidResponse && response.Found ? response.Source : null;
	}

	private async Task UpdateOutboxDocumentAsync(string messageId, ElasticsearchOutboxDocument doc, CancellationToken cancellationToken)
	{
		var response = await _client.IndexAsync(
			doc,
			idx => idx
				.Index(_options.IndexName)
				.Id(messageId)
				.Refresh(GetRefresh()),
			cancellationToken).ConfigureAwait(false);

		if (!response.IsValidResponse)
		{
			throw new InvalidOperationException(
				$"Failed to update outbox document: {response.ElasticsearchServerError?.Error?.Reason ?? "Unknown error"}");
		}
	}

	[LoggerMessage(DataElasticsearchEventId.DocumentIndexed, LogLevel.Debug,
		"Staged outbox message {MessageId} of type {MessageType} to destination {Destination}")]
	private partial void LogMessageStaged(string messageId, string messageType, string destination);

	[LoggerMessage(DataElasticsearchEventId.DocumentRetrieved, LogLevel.Debug,
		"Enqueued outbox message {MessageId} of type {MessageType}")]
	private partial void LogMessageEnqueued(string messageId, string messageType);

	[LoggerMessage(DataElasticsearchEventId.DocumentUpdated, LogLevel.Debug,
		"Marked outbox message {MessageId} as sent")]
	private partial void LogMessageSent(string messageId);

	[LoggerMessage(DataElasticsearchEventId.VersionConflict, LogLevel.Warning,
		"Marked outbox message {MessageId} as failed: {ErrorMessage} (retry {RetryCount})")]
	private partial void LogMessageFailed(string messageId, string errorMessage, int retryCount);

	[LoggerMessage(DataElasticsearchEventId.BulkOperationCompleted, LogLevel.Information,
		"Cleaned up {Count} sent outbox messages older than {OlderThan}")]
	private partial void LogMessagesCleanedUp(int count, DateTimeOffset olderThan);

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		// ElasticsearchClient does not require disposal.
		// This implementation satisfies the IAsyncDisposable contract for consistency
		// with other outbox store implementations and allows future resource cleanup.
		return ValueTask.CompletedTask;
	}
}
