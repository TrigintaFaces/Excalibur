// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;

using Excalibur.Data.Abstractions.CloudNative;

using Google.Cloud.Firestore;

using Microsoft.Extensions.Logging;

namespace Excalibur.Data.Firestore;

/// <summary>
/// Firestore implementation of change feed subscription using real-time listeners.
/// </summary>
/// <typeparam name="TDocument">The document type.</typeparam>
[SuppressMessage(
	"Maintainability",
	"CA1506:Avoid excessive class coupling",
	Justification = "Change feed implementations inherently couple with many SDK and abstraction types.")]
public sealed partial class FirestoreListenerSubscription<
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TDocument>
	: IChangeFeedSubscription<TDocument>
	where TDocument : class
{
	private readonly FirestoreDb _db;
	private readonly string _collectionPath;
	private readonly IChangeFeedOptions _options;
	private readonly ILogger _logger;
	private readonly CancellationTokenSource _cts = new();
	private readonly Channel<IChangeFeedEvent<TDocument>> _channel;

	private FirestoreChangeListener? _listener;
	private bool _isActive;
	private volatile bool _disposed;
	private long _sequenceNumber;

	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreListenerSubscription{TDocument}"/> class.
	/// </summary>
	/// <param name="db">The Firestore database.</param>
	/// <param name="collectionPath">The collection path to listen to.</param>
	/// <param name="options">The change feed options.</param>
	/// <param name="logger">The logger.</param>
	public FirestoreListenerSubscription(
		FirestoreDb db,
		string collectionPath,
		IChangeFeedOptions options,
		ILogger logger)
	{
		_db = db ?? throw new ArgumentNullException(nameof(db));
		_collectionPath = collectionPath ?? throw new ArgumentNullException(nameof(collectionPath));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		SubscriptionId = $"firestore-{collectionPath}-{Guid.NewGuid():N}";

		// Create bounded channel for backpressure
		_channel = Channel.CreateBounded<IChangeFeedEvent<TDocument>>(
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
	public string? CurrentContinuationToken { get; private set; }

	/// <inheritdoc/>
	public Task StartAsync(CancellationToken cancellationToken)
	{
		if (_disposed)
		{
			throw new ObjectDisposedException(nameof(FirestoreListenerSubscription<>));
		}

		LogStarting(SubscriptionId);

		var collectionRef = _db.Collection(_collectionPath);
		Query query = collectionRef;

		// Start the listener
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
	public async IAsyncEnumerable<IChangeFeedEvent<TDocument>> ReadChangesAsync(
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		if (_disposed)
		{
			throw new ObjectDisposedException(nameof(FirestoreListenerSubscription<>));
		}

		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
		var linkedToken = linkedCts.Token;

		while (!linkedToken.IsCancellationRequested && _isActive)
		{
			var items = new List<IChangeFeedEvent<TDocument>>();
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

			// Yield outside of try-catch block
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

	[return: MaybeNull]
	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	private static TDocument DeserializeDocument(DocumentSnapshot snapshot)
	{
		var dict = snapshot.ToDictionary();
		var json = JsonSerializer.Serialize(dict);
		return JsonSerializer.Deserialize<TDocument>(json);
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

		LogReceivedChanges(SubscriptionId, changes.Count);

		foreach (var change in changes)
		{
			var eventType = change.ChangeType switch
			{
				DocumentChange.Type.Added => ChangeFeedEventType.Created,
				DocumentChange.Type.Modified => ChangeFeedEventType.Updated,
				DocumentChange.Type.Removed => ChangeFeedEventType.Deleted,
				_ => ChangeFeedEventType.Updated
			};

			TDocument? document = null;
			if (change.Document.Exists)
			{
				document = DeserializeDocument(change.Document);
			}

			var documentId = change.Document.Id;
			var timestamp = change.Document.UpdateTime ?? change.Document.CreateTime ?? Timestamp.GetCurrentTimestamp();
			var sequenceNum = Interlocked.Increment(ref _sequenceNumber);

			CurrentContinuationToken = $"{documentId}:{timestamp.ToDateTime().Ticks}";

			var feedEvent = new FirestoreChangeEvent<TDocument>(
				eventType,
				document,
				documentId,
				new PartitionKey(_collectionPath),
				timestamp.ToDateTimeOffset(),
				CurrentContinuationToken,
				sequenceNum);

			// Try to write to channel, don't block
			_ = _channel.Writer.TryWrite(feedEvent);
		}
	}
}

/// <summary>
/// Firestore change event implementation.
/// </summary>
/// <typeparam name="TDocument">The document type.</typeparam>
public sealed class FirestoreChangeEvent<TDocument> : IChangeFeedEvent<TDocument>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="FirestoreChangeEvent{TDocument}"/> class.
	/// </summary>
	public FirestoreChangeEvent(
		ChangeFeedEventType eventType,
		TDocument? document,
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
	public TDocument? Document { get; }

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
