// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Runtime.CompilerServices;
using System.Threading.Channels;

using Excalibur.Data.CloudNative;
using Excalibur.Data.CosmosDb;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace Excalibur.EventSourcing.CosmosDb;

/// <summary>
/// Change feed subscription for the Cosmos DB event store.
/// </summary>
public sealed class CosmosDbEventStoreChangeFeedSubscription : IChangeFeedSubscription<CloudStoredEvent>
{
	private readonly Container _container;
	private readonly CosmosDbEventStoreOptions _options;
	private readonly ILogger _logger;
	private readonly IChangeFeedCheckpointStore? _checkpointStore;
	private readonly string _checkpointKey;
	private readonly CancellationTokenSource _cts = new();
	private readonly Channel<IChangeFeedEvent<CloudStoredEvent>> _channel;

	private FeedIterator<EventDocument>? _feedIterator;
	private bool _isActive;
	private volatile bool _disposed;
	private string? _continuationToken;
	private long _sequenceNumber;

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbEventStoreChangeFeedSubscription"/> class.
	/// </summary>
	/// <param name="container">The Cosmos DB container.</param>
	/// <param name="options">The event store options.</param>
	/// <param name="logger">The logger.</param>
	/// <param name="checkpointStore">
	/// Optional durable checkpoint store. When supplied, the continuation token is loaded on start and
	/// persisted after each batch so the subscription resumes across restarts instead of replaying the
	/// whole feed from the beginning. When <see langword="null"/> (default), behavior is unchanged.
	/// </param>
	public CosmosDbEventStoreChangeFeedSubscription(
		Container container,
		CosmosDbEventStoreOptions options,
		ILogger logger,
		IChangeFeedCheckpointStore? checkpointStore = null)
	{
		_container = container ?? throw new ArgumentNullException(nameof(container));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_checkpointStore = checkpointStore;

		// Stable, restart-invariant checkpoint key (NOT SubscriptionId, which carries a per-process Guid).
		_checkpointKey = $"cosmos-eventstore-{container.Id}";
		SubscriptionId = $"cosmos-eventstore-{Guid.NewGuid():N}";

		_channel = Channel.CreateBounded<IChangeFeedEvent<CloudStoredEvent>>(
			new BoundedChannelOptions(_options.MaxBatchSize * 10)
			{
				FullMode = BoundedChannelFullMode.Wait,
				SingleReader = true,
				SingleWriter = true
			});
	}

	/// <inheritdoc/>
	public string SubscriptionId { get; }

	/// <inheritdoc/>
	public bool IsActive => _isActive && !_disposed;

	/// <inheritdoc/>
	public string? CurrentContinuationToken => _continuationToken;

	/// <inheritdoc/>
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		if (_disposed)
		{
			throw new ObjectDisposedException(nameof(CosmosDbEventStoreChangeFeedSubscription));
		}

		// Resume from the durable checkpoint (if configured) instead of always replaying from the
		// beginning of the feed on every restart (the "continuation lost on restart" bug — bd-egwtku).
		if (_checkpointStore is not null)
		{
			_continuationToken =
				await _checkpointStore.LoadAsync(_checkpointKey, cancellationToken).ConfigureAwait(false);
		}

		var startFrom = !string.IsNullOrEmpty(_continuationToken)
			? ChangeFeedStartFrom.ContinuationToken(_continuationToken)
			: ChangeFeedStartFrom.Beginning();

		var changeFeedOptions = new ChangeFeedRequestOptions { PageSizeHint = _options.MaxBatchSize };

		_feedIterator = _container.GetChangeFeedIterator<EventDocument>(
			startFrom,
			ChangeFeedMode.LatestVersion,
			changeFeedOptions);

		_isActive = true;

		_ = PollChangeFeedAsync(_cts.Token);
	}

	/// <inheritdoc/>
	public async Task StopAsync(CancellationToken cancellationToken)
	{
		if (_disposed)
		{
			return;
		}

		_isActive = false;
		await _cts.CancelAsync().ConfigureAwait(false);
		_channel.Writer.Complete();
	}

	/// <inheritdoc/>
	public async IAsyncEnumerable<IChangeFeedEvent<CloudStoredEvent>> ReadChangesAsync(
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		if (_disposed)
		{
			throw new ObjectDisposedException(nameof(CosmosDbEventStoreChangeFeedSubscription));
		}

		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
		var linkedToken = linkedCts.Token;

		while (!linkedToken.IsCancellationRequested && _isActive)
		{
			var items = new List<IChangeFeedEvent<CloudStoredEvent>>();
			var shouldBreak = false;

			try
			{
				var hasItem = await _channel.Reader.WaitToReadAsync(linkedToken).ConfigureAwait(false);
				if (!hasItem)
				{
					shouldBreak = true;
				}
				else
				{
					while (_channel.Reader.TryRead(out var change))
					{
						items.Add(change);
					}
				}
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				shouldBreak = true;
			}
			catch (ChannelClosedException)
			{
				shouldBreak = true;
			}

			string? lastProcessedToken = null;
			foreach (var item in items)
			{
				yield return item;

				// Track the continuation token of the event the consumer has now processed (pulled).
				if (!string.IsNullOrEmpty(item.ContinuationToken))
				{
					lastProcessedToken = item.ContinuationToken;
				}
			}

			// Persist the checkpoint AFTER the consumer has processed the drained batch, so durable
			// continuation reflects CONSUMER progress (at-least-once) — never the producer's channel
			// read-ahead. No-op when no store is configured (prior in-memory-only behavior). bd-ydln24.
			if (_checkpointStore is not null && !string.IsNullOrEmpty(lastProcessedToken))
			{
				await _checkpointStore.SaveAsync(_checkpointKey, lastProcessedToken, linkedToken)
					.ConfigureAwait(false);
			}

			if (shouldBreak)
			{
				yield break;
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
		_channel.Writer.Complete();
		_feedIterator?.Dispose();
	}

	private static CloudStoredEvent ToCloudStoredEvent(EventDocument doc) =>
		new()
		{
			EventId = doc.EventId,
			AggregateId = doc.AggregateId,
			AggregateType = doc.AggregateType,
			EventType = doc.EventType,
			Version = doc.Version,
			Timestamp = doc.Timestamp,
			EventData = doc.EventData,
			Metadata = doc.Metadata,
			PartitionKeyValue = doc.StreamId,
			DocumentId = doc.Id,
			ETag = doc.ETag
		};

	private async Task PollChangeFeedAsync(CancellationToken cancellationToken)
	{
		while (!cancellationToken.IsCancellationRequested && _isActive && _feedIterator != null)
		{
			try
			{
				while (_feedIterator.HasMoreResults)
				{
					var response = await _feedIterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);

					foreach (var doc in response)
					{
						var cloudEvent = ToCloudStoredEvent(doc);
						var seqNum = Interlocked.Increment(ref _sequenceNumber);

						var feedEvent = new EventStoreChangeFeedEvent(
							ChangeFeedEventType.Created,
							cloudEvent,
							cloudEvent.DocumentId ?? doc.Id,
							new Data.CloudNative.PartitionKey(doc.StreamId),
							cloudEvent.Timestamp,
							response.ContinuationToken,
							seqNum);

						await _channel.Writer.WriteAsync(feedEvent, cancellationToken).ConfigureAwait(false);
					}

					// Track the producer's Cosmos read position in memory only (drives the iterator reset
					// below). The DURABLE checkpoint is persisted on the CONSUMER side (ReadChangesAsync),
					// after the consumer has actually processed the events — NOT here, where the events have
					// only been written to the in-memory channel. Persisting producer read-ahead would lose
					// channel-buffered events on a crash (at-most-once). bd-ydln24 / SA seam 17195.
					_continuationToken = response.ContinuationToken;
				}

				// Poll interval
				await Task.Delay(_options.ChangeFeedPollIntervalMs, cancellationToken).ConfigureAwait(false);

				// Reset iterator for continuous polling
				var changeFeedOptions = new ChangeFeedRequestOptions { PageSizeHint = _options.MaxBatchSize };

				_feedIterator = _container.GetChangeFeedIterator<EventDocument>(
					ChangeFeedStartFrom.ContinuationToken(_continuationToken),
					ChangeFeedMode.LatestVersion,
					changeFeedOptions);
			}
			catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error polling change feed for subscription {SubscriptionId}", SubscriptionId);
				await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
			}
		}
	}
}

/// <summary>
/// Change feed event for event store changes.
/// </summary>
internal sealed class EventStoreChangeFeedEvent : IChangeFeedEvent<CloudStoredEvent>
{
	public EventStoreChangeFeedEvent(
		ChangeFeedEventType eventType,
		CloudStoredEvent? document,
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
	public CloudStoredEvent? Document { get; }

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
