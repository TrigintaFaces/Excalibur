// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

using Excalibur.Data.Abstractions.CloudNative;

using Google.Cloud.Firestore;

using Microsoft.Extensions.Logging;

namespace Excalibur.EventSourcing.Firestore;

/// <summary>
/// Firestore real-time listener subscription for the event store.
/// </summary>
public sealed class FirestoreEventStoreListenerSubscription : IChangeFeedSubscription<CloudStoredEvent>
{
	private readonly FirestoreDb _db;
	private readonly FirestoreEventStoreOptions _options;
	private readonly ILogger _logger;
	private readonly CancellationTokenSource _cts = new();
	private readonly Channel<IChangeFeedEvent<CloudStoredEvent>> _channel;

	private FirestoreChangeListener? _listener;
	private bool _isActive;
	private volatile bool _disposed;
	private string? _continuationToken;
	private long _sequenceNumber;

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreEventStoreListenerSubscription" /> class.
	/// </summary>
	/// <param name="db"> The Firestore database. </param>
	/// <param name="options"> The event store options. </param>
	/// <param name="logger"> The logger. </param>
	public FirestoreEventStoreListenerSubscription(
		FirestoreDb db,
		FirestoreEventStoreOptions options,
		ILogger logger)
	{
		_db = db ?? throw new ArgumentNullException(nameof(db));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		SubscriptionId = $"firestore-eventstore-{Guid.NewGuid():N}";

		_channel = Channel.CreateBounded<IChangeFeedEvent<CloudStoredEvent>>(
			new BoundedChannelOptions(_options.MaxBatchSize * 10)
			{
				FullMode = BoundedChannelFullMode.Wait,
				SingleReader = true,
				SingleWriter = true
			});
	}

	/// <inheritdoc />
	public string SubscriptionId { get; }

	/// <inheritdoc />
	public bool IsActive => _isActive && !_disposed;

	/// <inheritdoc />
	public string? CurrentContinuationToken => _continuationToken;

	/// <inheritdoc />
	public Task StartAsync(CancellationToken cancellationToken)
	{
		if (_disposed)
		{
			throw new ObjectDisposedException(nameof(FirestoreEventStoreListenerSubscription));
		}

		var collectionRef = _db.Collection(_options.EventsCollectionName);
		var query = collectionRef.OrderBy("timestamp");

		_listener = query.Listen(snapshot =>
		{
			try
			{
				ProcessSnapshot(snapshot);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error processing Firestore snapshot for subscription {SubscriptionId}", SubscriptionId);
			}
		});

		_isActive = true;
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public async Task StopAsync(CancellationToken cancellationToken)
	{
		if (_disposed)
		{
			return;
		}

		_isActive = false;

		if (_listener != null)
		{
			await _listener.StopAsync().ConfigureAwait(false);
		}

		await _cts.CancelAsync().ConfigureAwait(false);
		_channel.Writer.Complete();
	}

	/// <inheritdoc />
	public async IAsyncEnumerable<IChangeFeedEvent<CloudStoredEvent>> ReadChangesAsync(
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		if (_disposed)
		{
			throw new ObjectDisposedException(nameof(FirestoreEventStoreListenerSubscription));
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
			catch (OperationCanceledException)
			{
				shouldBreak = true;
			}
			catch (ChannelClosedException)
			{
				shouldBreak = true;
			}

			foreach (var item in items)
			{
				yield return item;
			}

			if (shouldBreak)
			{
				yield break;
			}
		}
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_isActive = false;

		if (_listener != null)
		{
			await _listener.StopAsync().ConfigureAwait(false);
		}

		await _cts.CancelAsync().ConfigureAwait(false);
		_cts.Dispose();
		_channel.Writer.Complete();
	}

	private static CloudStoredEvent ToCloudStoredEvent(DocumentSnapshot doc)
	{
		var streamId = doc.ContainsField("streamId") ? doc.GetValue<string>("streamId") : string.Empty;

		return new CloudStoredEvent
		{
			EventId = doc.ContainsField("eventId") ? doc.GetValue<string>("eventId") : doc.Id,
			AggregateId = doc.ContainsField("aggregateId") ? doc.GetValue<string>("aggregateId") : string.Empty,
			AggregateType = doc.ContainsField("aggregateType") ? doc.GetValue<string>("aggregateType") : string.Empty,
			EventType = doc.ContainsField("eventType") ? doc.GetValue<string>("eventType") : string.Empty,
			Version = doc.ContainsField("version") ? doc.GetValue<long>("version") : 0,
			Timestamp = doc.ContainsField("timestamp")
				? DateTimeOffset.Parse(doc.GetValue<string>("timestamp"), CultureInfo.InvariantCulture)
				: DateTimeOffset.UtcNow,
			EventData = doc.ContainsField("eventData")
				? Convert.FromBase64String(doc.GetValue<string>("eventData"))
				: [],
			Metadata = doc.ContainsField("metadata") && doc.GetValue<string?>("metadata") != null
				? Convert.FromBase64String(doc.GetValue<string>("metadata"))
				: null,
			PartitionKeyValue = streamId,
			DocumentId = doc.Id,
			IsDispatched = doc.ContainsField("isDispatched") && doc.GetValue<bool>("isDispatched")
		};
	}

	private void ProcessSnapshot(QuerySnapshot snapshot)
	{
		if (!_isActive || _disposed)
		{
			return;
		}

		var changes = snapshot.Changes.ToList();
		if (changes.Count == 0)
		{
			return;
		}

		foreach (var change in changes)
		{
			var eventType = change.ChangeType switch
			{
				DocumentChange.Type.Added => ChangeFeedEventType.Created,
				DocumentChange.Type.Modified => ChangeFeedEventType.Updated,
				DocumentChange.Type.Removed => ChangeFeedEventType.Deleted,
				_ => ChangeFeedEventType.Updated
			};

			CloudStoredEvent? cloudEvent = null;
			if (change.Document.Exists)
			{
				cloudEvent = ToCloudStoredEvent(change.Document);
			}

			var documentId = change.Document.Id;
			var timestamp = change.Document.UpdateTime ?? change.Document.CreateTime ?? Timestamp.GetCurrentTimestamp();
			var sequenceNum = Interlocked.Increment(ref _sequenceNumber);

			_continuationToken = $"{documentId}:{timestamp.ToDateTime().Ticks}";

			var feedEvent = new FirestoreEventStoreFeedEvent(
				eventType,
				cloudEvent,
				documentId,
				new PartitionKey(cloudEvent?.PartitionKeyValue ?? string.Empty),
				timestamp.ToDateTimeOffset(),
				_continuationToken,
				sequenceNum);

			_ = _channel.Writer.TryWrite(feedEvent);
		}
	}
}

/// <summary>
/// Change feed event for Firestore event store changes.
/// </summary>
internal sealed class FirestoreEventStoreFeedEvent : IChangeFeedEvent<CloudStoredEvent>
{
	public FirestoreEventStoreFeedEvent(
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

	/// <inheritdoc />
	public ChangeFeedEventType EventType { get; }

	/// <inheritdoc />
	public CloudStoredEvent? Document { get; }

	/// <inheritdoc />
	public string DocumentId { get; }

	/// <inheritdoc />
	public IPartitionKey PartitionKey { get; }

	/// <inheritdoc />
	public DateTimeOffset Timestamp { get; }

	/// <inheritdoc />
	public string ContinuationToken { get; }

	/// <inheritdoc />
	public long SequenceNumber { get; }
}
