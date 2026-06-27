// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using Excalibur.Data.CloudNative;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

using CloudPartitionKey = Excalibur.Data.CloudNative.PartitionKey;

namespace Excalibur.Data.CosmosDb;

/// <summary>
/// Azure Cosmos DB implementation of change feed subscription.
/// </summary>
/// <typeparam name="TDocument">The document type.</typeparam>
public sealed partial class CosmosDbChangeFeedSubscription<
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TDocument>
	: IChangeFeedSubscription<TDocument>
	where TDocument : class
{
	private readonly Container _container;
	private readonly IChangeFeedOptions _options;
	private readonly ILogger _logger;
	private readonly IChangeFeedCheckpointStore? _checkpointStore;
	private readonly string _checkpointKey;
	private readonly CancellationTokenSource _cts = new();

	private bool _isActive;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbChangeFeedSubscription{TDocument}"/> class.
	/// </summary>
	/// <param name="container">The Cosmos DB container.</param>
	/// <param name="options">The change feed options.</param>
	/// <param name="logger">The logger.</param>
	/// <param name="checkpointStore">
	/// Optional durable checkpoint store. When supplied, the continuation token is loaded on start and
	/// persisted after each batch so the subscription resumes across restarts instead of replaying from
	/// the configured start position. When <see langword="null"/> (default), behavior is unchanged
	/// (continuation is tracked in memory only). See bd-egwtku.
	/// </param>
	public CosmosDbChangeFeedSubscription(
		Container container,
		IChangeFeedOptions options,
		ILogger logger,
		IChangeFeedCheckpointStore? checkpointStore = null)
	{
		_container = container ?? throw new ArgumentNullException(nameof(container));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_checkpointStore = checkpointStore;

		// Stable, restart-invariant key for checkpoint persistence — deliberately NOT SubscriptionId
		// (which carries a per-process Guid and would never match a prior run's checkpoint).
		_checkpointKey = $"cf-{container.Id}";
		SubscriptionId = $"cf-{container.Id}-{Guid.NewGuid():N}";
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
			throw new ObjectDisposedException(nameof(CosmosDbChangeFeedSubscription<>));
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
	public async IAsyncEnumerable<IChangeFeedEvent<TDocument>> ReadChangesAsync(
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		if (_disposed)
		{
			throw new ObjectDisposedException(nameof(CosmosDbChangeFeedSubscription<>));
		}

		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
		var linkedToken = linkedCts.Token;

		// Resume from the durable checkpoint (if a store is configured) before the first iterator, so a
		// restart continues where it left off instead of replaying from the configured start position.
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
				// Wait before polling again
				try
				{
					await Task.Delay(_options.PollingInterval, linkedToken).ConfigureAwait(false);
				}
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
				{
					yield break;
				}

				// Recreate iterator to continue from last position
				iterator = CreateChangeFeedIterator(null);
				continue;
			}

			FeedResponse<TDocument>? response = null;
			try
			{
				response = await iterator.ReadNextAsync(linkedToken).ConfigureAwait(false);
			}
			catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotModified)
			{
				// No new changes, continue polling
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

			// Capture THIS page's "resume-after-this-page" continuation token before yielding. The durable
			// checkpoint is persisted only AFTER the consumer has processed (pulled) every document in the
			// page (post-yield, below) — never before — so a crash mid-page resumes from BEFORE the page
			// (at-least-once); persisting first would advance past unprocessed changes (at-most-once /
			// silent skip). bd-ydln24 / SA seam 17195.
			var pageContinuationToken = response.ContinuationToken;
			CurrentContinuationToken = pageContinuationToken;

			long sequenceNumber = 0;
			foreach (var document in response)
			{
				yield return new CosmosDbChangeFeedEvent<TDocument>(
					ChangeFeedEventType.Updated, // Cosmos DB change feed only reports creates and updates
					document,
					GetDocumentId(document),
					new CloudPartitionKey(GetPartitionKeyValue(document)),
					DateTimeOffset.UtcNow,
					response.ContinuationToken ?? string.Empty,
					sequenceNumber++);
			}

			// Persist AFTER the whole page has been yielded to (and processed by) the consumer, so progress
			// survives a restart without ever advancing past an unprocessed change. No-op when no store is
			// configured (in-memory-only, prior behavior).
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

	private static string GetDocumentId(TDocument document)
	{
		var idProperty = typeof(TDocument).GetProperty("id") ?? typeof(TDocument).GetProperty("Id");
		return idProperty?.GetValue(document)?.ToString() ?? Guid.NewGuid().ToString();
	}

	private static string GetPartitionKeyValue(TDocument document)
	{
		// Try common partition key property names
		var pkProperty = typeof(TDocument).GetProperty("partitionKey")
						 ?? typeof(TDocument).GetProperty("PartitionKey")
						 ?? typeof(TDocument).GetProperty("tenantId")
						 ?? typeof(TDocument).GetProperty("TenantId")
						 ?? typeof(TDocument).GetProperty("id")
						 ?? typeof(TDocument).GetProperty("Id");

		return pkProperty?.GetValue(document)?.ToString() ?? string.Empty;
	}

	private DateTime? GetStartTime()
	{
		return _options.StartPosition switch
		{
			ChangeFeedStartPosition.Beginning => null, // null means from beginning
			ChangeFeedStartPosition.Now => DateTimeOffset.UtcNow.UtcDateTime,
			ChangeFeedStartPosition.FromTimestamp when _options.StartTimestamp.HasValue =>
				_options.StartTimestamp.Value.UtcDateTime,
			_ => DateTimeOffset.UtcNow.UtcDateTime
		};
	}

	private FeedIterator<TDocument> CreateChangeFeedIterator(DateTime? startTime)
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

		return _container.GetChangeFeedIterator<TDocument>(startFrom, ChangeFeedMode.Incremental, requestOptions);
	}
}

/// <summary>
/// Cosmos DB change feed event implementation.
/// </summary>
/// <typeparam name="TDocument">The document type.</typeparam>
public sealed class CosmosDbChangeFeedEvent<TDocument> : IChangeFeedEvent<TDocument>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbChangeFeedEvent{TDocument}"/> class.
	/// </summary>
	public CosmosDbChangeFeedEvent(
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
