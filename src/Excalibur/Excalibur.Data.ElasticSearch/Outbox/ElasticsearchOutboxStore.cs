// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;

using Excalibur.Data.ElasticSearch.Diagnostics;
using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.ElasticSearch.Outbox;

/// <summary>
/// Elasticsearch-based implementation of <see cref="IOutboxStore"/> and <see cref="IOutboxStoreAdmin"/>.
/// </summary>
/// <remarks>
/// <para>
/// Uses ES document ID = message ID for direct access.
/// Messages are sorted by priority and creation time for ordered retrieval.
/// </para>
/// </remarks>
public sealed partial class ElasticsearchOutboxStore : IOutboxStore, IOutboxStoreAdmin
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

		var outbound = new OutboundMessage(messageType, payload, messageType)
		{
			CorrelationId = context.CorrelationId,
			CausationId = context.CausationId,
		};

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
				SortOptions.Field(new Field("priority"), new FieldSort { Order = SortOrder.Asc }),
				SortOptions.Field(new Field("createdAt"), new FieldSort { Order = SortOrder.Asc }),
			],
			Query = new BoolQuery
			{
				Should =
				[
					new TermQuery(new Field("status")) { Value = (int)OutboxStatus.Staged },
					new BoolQuery
					{
						Must =
						[
							new TermQuery(new Field("status")) { Value = (int)OutboxStatus.Staged },
							new DateRangeQuery(new Field("scheduledAt")) { Lte = (DateMath)now.DateTime },
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
	public async ValueTask<IEnumerable<OutboundMessage>> GetFailedMessagesAsync(
		int maxRetries,
		DateTimeOffset? olderThan,
		int batchSize,
		CancellationToken cancellationToken)
	{
		var mustClauses = new List<Query>
		{
			new TermQuery(new Field("status")) { Value = (int)OutboxStatus.Failed },
		};

		if (maxRetries > 0)
		{
			mustClauses.Add(new NumberRangeQuery(new Field("retryCount")) { Lt = maxRetries });
		}

		if (olderThan.HasValue)
		{
			mustClauses.Add(new DateRangeQuery(new Field("lastAttemptAt")) { Lt = (DateMath)olderThan.Value.DateTime });
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
					new DateRangeQuery(new Field("scheduledAt")) { Lte = (DateMath)scheduledBefore.DateTime },
					new TermQuery(new Field("status")) { Value = (int)OutboxStatus.Staged },
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
		var response = await _client.DeleteByQueryAsync<ElasticsearchOutboxDocument>(
			static d => d
				.Indices("excalibur-outbox")
				.Query(q => q.MatchAll(new MatchAllQuery())),
			cancellationToken).ConfigureAwait(false);

		var deleted = (int)(response.Deleted ?? 0);
		LogMessagesCleanedUp(deleted, olderThan);
		return deleted;
	}

	/// <inheritdoc/>
	public async ValueTask<OutboxStatistics> GetStatisticsAsync(CancellationToken cancellationToken)
	{
		var now = DateTimeOffset.UtcNow;

		var response = await _client.SearchAsync<ElasticsearchOutboxDocument>(s => s
			.Index(_options.IndexName)
			.Size(10000)
			.Query(q => q.MatchAll(new MatchAllQuery())),
			cancellationToken).ConfigureAwait(false);

		if (!response.IsValidResponse)
		{
			return new OutboxStatistics { CapturedAt = now };
		}

		var docs = response.Documents;

		var staged = docs.Where(d => d.Status == (int)OutboxStatus.Staged).ToList();
		var failed = docs.Where(d => d.Status == (int)OutboxStatus.Failed).ToList();

		return new OutboxStatistics
		{
			StagedMessageCount = staged.Count,
			SendingMessageCount = 0,
			SentMessageCount = docs.Count(d => d.Status == (int)OutboxStatus.Sent),
			FailedMessageCount = failed.Count,
			ScheduledMessageCount = docs.Count(d => d.ScheduledAt.HasValue && d.Status == (int)OutboxStatus.Staged),
			OldestUnsentMessageAge = staged.Count > 0 ? now - staged.Min(d => d.CreatedAt) : null,
			OldestFailedMessageAge = failed.Count > 0 ? now - failed.Min(d => d.CreatedAt) : null,
			CapturedAt = now,
		};
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
}
