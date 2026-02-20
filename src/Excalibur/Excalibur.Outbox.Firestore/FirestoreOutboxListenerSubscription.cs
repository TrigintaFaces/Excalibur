// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;

using Excalibur.Data.Abstractions.CloudNative;

using Google.Cloud.Firestore;

using Microsoft.Extensions.Logging;

namespace Excalibur.Outbox.Firestore;

/// <summary>
/// Firestore real-time listener subscription for outbox messages.
/// Only emits newly added unpublished messages.
/// </summary>
public sealed partial class FirestoreOutboxListenerSubscription : IChangeFeedSubscription<CloudOutboxMessage>
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = false
	};

	private readonly FirestoreDb _db;
	private readonly FirestoreOutboxOptions _options;
	private readonly ILogger _logger;
	private readonly CancellationTokenSource _cts = new();
	private readonly Channel<IChangeFeedEvent<CloudOutboxMessage>> _channel;

	private FirestoreChangeListener? _listener;
	private bool _isActive;
	private volatile bool _disposed;
	private string? _continuationToken;
	private long _sequenceNumber;

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreOutboxListenerSubscription"/> class.
	/// </summary>
	/// <param name="db">The Firestore database.</param>
	/// <param name="options">The outbox options.</param>
	/// <param name="logger">The logger.</param>
	public FirestoreOutboxListenerSubscription(
		FirestoreDb db,
		FirestoreOutboxOptions options,
		ILogger logger)
	{
		_db = db ?? throw new ArgumentNullException(nameof(db));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		SubscriptionId = $"firestore-outbox-{Guid.NewGuid():N}";

		_channel = Channel.CreateBounded<IChangeFeedEvent<CloudOutboxMessage>>(
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
	public Task StartAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		LogStarting(SubscriptionId);

		var collectionRef = _db.Collection(_options.CollectionName);

		// Listen to unpublished messages only
		var query = collectionRef
			.WhereEqualTo("isPublished", false)
			.OrderBy("createdAt");

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

	/// <inheritdoc/>
	public async Task StopAsync(CancellationToken cancellationToken)
	{
		if (_disposed)
		{
			return;
		}

		LogStopping(SubscriptionId);
		_isActive = false;

		if (_listener != null)
		{
			await _listener.StopAsync().ConfigureAwait(false);
		}

		await _cts.CancelAsync().ConfigureAwait(false);
		_channel.Writer.Complete();
	}

	/// <inheritdoc/>
	public async IAsyncEnumerable<IChangeFeedEvent<CloudOutboxMessage>> ReadChangesAsync(
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
		var linkedToken = linkedCts.Token;

		while (!linkedToken.IsCancellationRequested && _isActive)
		{
			var items = new List<IChangeFeedEvent<CloudOutboxMessage>>();
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

	/// <inheritdoc/>
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

	private static CloudOutboxMessage FromFirestoreDocument(DocumentSnapshot doc)
	{
		return new CloudOutboxMessage
		{
			MessageId = doc.GetValue<string>("messageId"),
			MessageType = doc.GetValue<string>("messageType"),
			Payload = Convert.FromBase64String(doc.GetValue<string>("payload")),
			Headers = doc.ContainsField("headers") && doc.GetValue<string?>("headers") != null
				? JsonSerializer.Deserialize<Dictionary<string, string>>(doc.GetValue<string>("headers"), JsonOptions)
				: null,
			AggregateId = doc.ContainsField("aggregateId") ? doc.GetValue<string?>("aggregateId") : null,
			AggregateType = doc.ContainsField("aggregateType") ? doc.GetValue<string?>("aggregateType") : null,
			CorrelationId = doc.ContainsField("correlationId") ? doc.GetValue<string?>("correlationId") : null,
			CausationId = doc.ContainsField("causationId") ? doc.GetValue<string?>("causationId") : null,
			CreatedAt = DateTimeOffset.Parse(doc.GetValue<string>("createdAt"), CultureInfo.InvariantCulture),
			PublishedAt = doc.ContainsField("publishedAt") && doc.GetValue<string?>("publishedAt") != null
				? DateTimeOffset.Parse(doc.GetValue<string>("publishedAt"), CultureInfo.InvariantCulture)
				: null,
			RetryCount = doc.ContainsField("retryCount") ? doc.GetValue<int>("retryCount") : 0,
			LastError = doc.ContainsField("lastError") ? doc.GetValue<string?>("lastError") : null,
			PartitionKeyValue = doc.GetValue<string>("partitionKey")
		};
	}

	private void ProcessSnapshot(QuerySnapshot snapshot)
	{
		if (!_isActive || _disposed)
		{
			return;
		}

		var changes = snapshot.Changes.ToList();

		// Only process Added events for unpublished messages
		var unpublishedInserts = changes
			.Where(c => c.ChangeType == DocumentChange.Type.Added)
			.ToList();

		if (changes.Count > 0)
		{
			LogReceivedBatch(SubscriptionId, changes.Count, unpublishedInserts.Count);
		}

		foreach (var change in unpublishedInserts)
		{
			if (!change.Document.Exists)
			{
				continue;
			}

			// Double-check the message is unpublished
			if (change.Document.ContainsField("isPublished") && change.Document.GetValue<bool>("isPublished"))
			{
				continue;
			}

			var message = FromFirestoreDocument(change.Document);
			var documentId = change.Document.Id;
			var timestamp = change.Document.UpdateTime ?? change.Document.CreateTime ?? Timestamp.GetCurrentTimestamp();
			var sequenceNum = Interlocked.Increment(ref _sequenceNumber);

			_continuationToken = $"{documentId}:{timestamp.ToDateTime().Ticks}";

			var feedEvent = new FirestoreOutboxFeedEvent(
				ChangeFeedEventType.Created,
				message,
				documentId,
				new PartitionKey(message.PartitionKeyValue ?? string.Empty),
				timestamp.ToDateTimeOffset(),
				_continuationToken,
				sequenceNum);

			_ = _channel.Writer.TryWrite(feedEvent);
		}
	}
}

/// <summary>
/// Change feed event for Firestore outbox messages.
/// </summary>
internal sealed class FirestoreOutboxFeedEvent : IChangeFeedEvent<CloudOutboxMessage>
{
	public FirestoreOutboxFeedEvent(
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
