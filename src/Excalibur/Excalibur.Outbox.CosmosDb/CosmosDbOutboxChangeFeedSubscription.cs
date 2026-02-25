// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;

using Excalibur.Data.Abstractions.CloudNative;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

using CloudPartitionKey = Excalibur.Data.Abstractions.CloudNative.PartitionKey;

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
	private readonly CancellationTokenSource _cts = new();

	private bool _isActive;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbOutboxChangeFeedSubscription"/> class.
	/// </summary>
	/// <param name="container">The Cosmos DB container.</param>
	/// <param name="options">The change feed options.</param>
	/// <param name="logger">The logger.</param>
	public CosmosDbOutboxChangeFeedSubscription(
		Container container,
		IChangeFeedOptions options,
		ILogger logger)
	{
		_container = container ?? throw new ArgumentNullException(nameof(container));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

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
				catch (OperationCanceledException)
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
				catch (OperationCanceledException)
				{
					yield break;
				}

				continue;
			}
			catch (OperationCanceledException)
			{
				yield break;
			}

			if (response == null || response.Count == 0)
			{
				continue;
			}

			LogReceivedBatch(SubscriptionId, response.Count);
			CurrentContinuationToken = response.ContinuationToken;

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
			Headers = !string.IsNullOrEmpty(doc.Headers)
				? JsonSerializer.Deserialize<Dictionary<string, string>>(doc.Headers, JsonOptions)
				: null,
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
