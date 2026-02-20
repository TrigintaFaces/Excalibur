// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using Excalibur.Data.Abstractions.CloudNative;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

using CloudPartitionKey = Excalibur.Data.Abstractions.CloudNative.PartitionKey;

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
	private readonly CancellationTokenSource _cts = new();

	private bool _isActive;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbChangeFeedSubscription{TDocument}"/> class.
	/// </summary>
	/// <param name="container">The Cosmos DB container.</param>
	/// <param name="options">The change feed options.</param>
	/// <param name="logger">The logger.</param>
	public CosmosDbChangeFeedSubscription(
		Container container,
		IChangeFeedOptions options,
		ILogger logger)
	{
		_container = container ?? throw new ArgumentNullException(nameof(container));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

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
				catch (OperationCanceledException)
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
