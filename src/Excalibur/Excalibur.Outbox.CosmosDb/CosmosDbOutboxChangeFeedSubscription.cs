// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;

using Excalibur.Data.CloudNative;
using Excalibur.Data.CosmosDb;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

using CloudPartitionKey = Excalibur.Data.CloudNative.PartitionKey;

namespace Excalibur.Outbox.CosmosDb;

/// <summary>
/// Cosmos DB change feed subscription for outbox messages.
/// </summary>
public sealed partial class CosmosDbOutboxChangeFeedSubscription : IChangeFeedSubscription<CloudOutboxMessage>
{
	private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

	private readonly Container _container;
	private readonly IChangeFeedOptions _options;
	private readonly ILogger _logger;
	private readonly IChangeFeedCheckpointStore? _checkpointStore;
	private readonly string _checkpointKey;
	private readonly CancellationTokenSource _cts = new();

	private bool _isActive;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbOutboxChangeFeedSubscription"/> class.
	/// </summary>
	/// <param name="container">The Cosmos DB container.</param>
	/// <param name="options">The change feed options.</param>
	/// <param name="logger">The logger.</param>
	/// <param name="checkpointStore">
	/// Optional durable checkpoint store. When supplied, the continuation token is loaded on start and
	/// persisted after each batch so the subscription resumes across restarts instead of replaying from
	/// the configured start position. When <see langword="null"/> (default), behavior is unchanged
	/// (continuation tracked in memory only). See bd-egwtku.
	/// </param>
	public CosmosDbOutboxChangeFeedSubscription(
		Container container,
		IChangeFeedOptions options,
		ILogger logger,
		IChangeFeedCheckpointStore? checkpointStore = null)
	{
		_container = container ?? throw new ArgumentNullException(nameof(container));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_checkpointStore = checkpointStore;

		// Stable, restart-invariant checkpoint key (NOT SubscriptionId, which carries a per-process Guid).
		_checkpointKey = $"outbox-cf-{container.Id}";
		SubscriptionId = $"outbox-cf-{Guid.NewGuid():N}";
	}

	/// <inheritdoc/>
	public string SubscriptionId { get; }

	/// <inheritdoc/>
	public bool IsActive => _isActive && !_disposed;

	/// <inheritdoc/>
	public string? CurrentContinuationToken { get; private set; }

	/// <inheritdoc/>
	public Task StartAsync(CancellationToken cancellationToken)
	{
		if (_disposed)
		{
			throw new ObjectDisposedException(nameof(CosmosDbOutboxChangeFeedSubscription));
		}

		LogStarting(SubscriptionId);
		_isActive = true;
		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public async Task StopAsync(CancellationToken cancellationToken)
	{
		if (_disposed)
		{
			return;
		}

		LogStopping(SubscriptionId);
		_isActive = false;
		await _cts.CancelAsync().ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async IAsyncEnumerable<IChangeFeedEvent<CloudOutboxMessage>> ReadChangesAsync(
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		if (_disposed)
		{
			throw new ObjectDisposedException(nameof(CosmosDbOutboxChangeFeedSubscription));
		}

		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
		var linkedToken = linkedCts.Token;

		// Resume from the durable checkpoint (if configured) before the first iterator.
		if (CurrentContinuationToken is null && _checkpointStore is not null)
		{
			CurrentContinuationToken =
				await _checkpointStore.LoadAsync(_checkpointKey, linkedToken).ConfigureAwait(false);
		}

		var startTime = GetStartTime();
		var iterator = CreateChangeFeedIterator(startTime);

		while (_isActive && !linkedToken.IsCancellationRequested)
		{
			if (!iterator.HasMoreResults)
			{
				try
				{
					await Task.Delay(_options.PollingInterval, linkedToken).ConfigureAwait(false);
				}
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
				{
					yield break;
				}

				iterator = CreateChangeFeedIterator(null);
				continue;
			}

			FeedResponse<OutboxDocument>? response = null;
			try
			{
				response = await iterator.ReadNextAsync(linkedToken).ConfigureAwait(false);
			}
			catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotModified)
			{
				try
				{
					await Task.Delay(_options.PollingInterval, linkedToken).ConfigureAwait(false);
				}
				catch (OperationCanceledException) when (linkedToken.IsCancellationRequested)
				{
					yield break;
				}

				continue;
			}
			catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
			{
				yield break;
			}

			if (response == null || response.Count == 0)
			{
				continue;
			}

			LogReceivedBatch(SubscriptionId, response.Count);

			// Capture THIS page's "resume-after-this-page" continuation token before yielding; persist it
			// only AFTER the consumer has processed (pulled) the page (post-yield, below) — never before —
			// so a crash mid-page resumes from BEFORE the page (at-least-once), never advancing past
			// unprocessed changes. bd-ydln24 / SA seam 17195.
			var pageContinuationToken = response.ContinuationToken;
			CurrentContinuationToken = pageContinuationToken;

			long sequenceNumber = 0;
			foreach (var doc in response)
			{
				// Only yield unpublished messages for the outbox pattern
				if (doc.IsPublished)
				{
					continue;
				}

				var message = FromDocument(doc);
				yield return new OutboxChangeFeedEvent(
					ChangeFeedEventType.Created,
					message,
					doc.Id,
					new CloudPartitionKey(doc.PartitionKey),
					DateTimeOffset.UtcNow,
					response.ContinuationToken ?? string.Empty,
					sequenceNumber++);
			}

			// Persist AFTER the whole page has been yielded to (and processed by) the consumer, so progress
			// survives a restart without ever advancing past an unprocessed change. No-op when no store is
			// configured (prior in-memory-only behavior).
			if (_checkpointStore is not null && !string.IsNullOrEmpty(pageContinuationToken))
			{
				await _checkpointStore.SaveAsync(_checkpointKey, pageContinuationToken, linkedToken)
					.ConfigureAwait(false);
			}
		}
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_isActive = false;
		await _cts.CancelAsync().ConfigureAwait(false);
		_cts.Dispose();
	}

	private static CloudOutboxMessage FromDocument(OutboxDocument doc) =>
		new()
		{
			MessageId = doc.Id,
			MessageType = doc.MessageType,
			Payload = Convert.FromBase64String(doc.Payload),
#pragma warning disable IL2026
			Headers = !string.IsNullOrEmpty(doc.Headers)
				? JsonSerializer.Deserialize<Dictionary<string, string>>(doc.Headers, JsonOptions)
				: null,
#pragma warning restore IL2026
			AggregateId = doc.AggregateId,
			AggregateType = doc.AggregateType,
			CorrelationId = doc.CorrelationId,
			CausationId = doc.CausationId,
			CreatedAt = DateTimeOffset.Parse(doc.CreatedAt, CultureInfo.InvariantCulture),
			PublishedAt = !string.IsNullOrEmpty(doc.PublishedAt) ? DateTimeOffset.Parse(doc.PublishedAt, CultureInfo.InvariantCulture) : null,
			RetryCount = doc.RetryCount,
			LastError = doc.LastError,
			PartitionKeyValue = doc.PartitionKey,
			ETag = doc.ETag
		};

	private DateTime? GetStartTime()
	{
		return _options.StartPosition switch
		{
			ChangeFeedStartPosition.Beginning => null,
			ChangeFeedStartPosition.Now => DateTimeOffset.UtcNow.UtcDateTime,
			ChangeFeedStartPosition.FromTimestamp when _options.StartTimestamp.HasValue =>
				_options.StartTimestamp.Value.UtcDateTime,
			_ => DateTimeOffset.UtcNow.UtcDateTime
		};
	}

	private FeedIterator<OutboxDocument> CreateChangeFeedIterator(DateTime? startTime)
	{
		ChangeFeedStartFrom startFrom;

		if (!string.IsNullOrEmpty(CurrentContinuationToken))
		{
			startFrom = ChangeFeedStartFrom.ContinuationToken(CurrentContinuationToken);
		}
		else if (!string.IsNullOrEmpty(_options.ContinuationToken))
		{
			startFrom = ChangeFeedStartFrom.ContinuationToken(_options.ContinuationToken);
		}
		else if (startTime.HasValue)
		{
			startFrom = ChangeFeedStartFrom.Time(startTime.Value);
		}
		else
		{
			startFrom = ChangeFeedStartFrom.Beginning();
		}

		var requestOptions = new ChangeFeedRequestOptions { PageSizeHint = _options.MaxBatchSize };

		return _container.GetChangeFeedIterator<OutboxDocument>(startFrom, ChangeFeedMode.Incremental, requestOptions);
	}

	/// <summary>
	/// Internal document representation for Cosmos DB storage.
	/// </summary>
	private sealed class OutboxDocument
	{
		public required string Id { get; set; }
		public required string PartitionKey { get; set; }
		public required string MessageType { get; set; }
		public required string Payload { get; set; }
		public string? Headers { get; set; }
		public string? AggregateId { get; set; }
		public string? AggregateType { get; set; }
		public string? CorrelationId { get; set; }
		public string? CausationId { get; set; }
		public required string CreatedAt { get; set; }
		public string? PublishedAt { get; set; }
		public bool IsPublished { get; set; }
		public int RetryCount { get; set; }
		public string? LastError { get; set; }
		public string? ETag { get; set; }
	}
}

/// <summary>
/// Change feed event for outbox messages.
/// </summary>
internal sealed class OutboxChangeFeedEvent : IChangeFeedEvent<CloudOutboxMessage>
{
	public OutboxChangeFeedEvent(
		ChangeFeedEventType eventType,
		CloudOutboxMessage? document,
		string documentId,
		IPartitionKey partitionKey,
		DateTimeOffset timestamp,
		string continuationToken,
		long sequenceNumber)
	{
		EventType = eventType;
		Document = document;
		DocumentId = documentId;
		PartitionKey = partitionKey;
		Timestamp = timestamp;
		ContinuationToken = continuationToken;
		SequenceNumber = sequenceNumber;
	}

	/// <inheritdoc/>
	public ChangeFeedEventType EventType { get; }

	/// <inheritdoc/>
	public CloudOutboxMessage? Document { get; }

	/// <inheritdoc/>
	public string DocumentId { get; }

	/// <inheritdoc/>
	public IPartitionKey PartitionKey { get; }

	/// <inheritdoc/>
	public DateTimeOffset Timestamp { get; }

	/// <inheritdoc/>
	public string ContinuationToken { get; }

	/// <inheritdoc/>
	public long SequenceNumber { get; }
}
