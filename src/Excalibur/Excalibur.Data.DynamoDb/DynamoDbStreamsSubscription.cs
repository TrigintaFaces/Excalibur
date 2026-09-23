// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;

using Amazon.DynamoDBStreams;
using Amazon.DynamoDBStreams.Model;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;

using Excalibur.Data.CloudNative;

using Microsoft.Extensions.Logging;

namespace Excalibur.Data.DynamoDb;

/// <summary>
/// DynamoDB Streams implementation of change feed subscription.
/// </summary>
/// <typeparam name="TDocument"> The document type. </typeparam>
/// <remarks>
/// This implementation uses the DynamoDB Streams API via <see cref="IAmazonDynamoDBStreams" /> client for real-time change data capture.
/// The table must have streams enabled.
/// </remarks>
[SuppressMessage(
	"Maintainability",
	"CA1506:Avoid excessive class coupling",
	Justification = "Change feed implementations inherently couple with many SDK and abstraction types.")]
public sealed partial class DynamoDbStreamsSubscription<
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
TDocument>
	: IChangeFeedSubscription<TDocument>
	where TDocument : class
{
	private readonly IAmazonDynamoDB _client;
	private readonly IAmazonDynamoDBStreams _streamsClient;
	private readonly string _tableName;
	private readonly string _partitionKeyAttribute;
	private readonly string _sortKeyAttribute;
	private readonly IChangeFeedOptions _options;
	private readonly ILogger _logger;
	// Guards _cts so a stop/start cycle can atomically retire the canceled source and install a fresh one
	// StopAsync cancels _cts, and a subsequent StartAsync must recreate it or every new
	// ReadChangesAsync would link a permanently-canceled token and yield-break immediately.
	private readonly System.Threading.Lock _ctsLock = new();

	// The position reached in EACH shard. A stream has many shards with independent sequence spaces, so a
	// single scalar cannot describe where this subscription is; the whole map is what the opaque
	// continuation token carries.
	private readonly Dictionary<string, string> _shardPositions = new(StringComparer.Ordinal);
	private CancellationTokenSource _cts = new();

	private string? _streamArn;
	private volatile bool _isActive;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamoDbStreamsSubscription{TDocument}" /> class.
	/// </summary>
	/// <param name="client"> The DynamoDB client. </param>
	/// <param name="streamsClient"> The DynamoDB Streams client. </param>
	/// <param name="tableName"> The table name. </param>
	/// <param name="partitionKeyAttribute"> The name of the table's partition key attribute. </param>
	/// <param name="sortKeyAttribute"> The name of the table's sort key attribute. </param>
	/// <param name="options"> The change feed options. </param>
	/// <param name="logger"> The logger. </param>
	/// <remarks>
	/// The key attribute names are required rather than assumed. They identify the change feed event a
	/// consumer keys its idempotency on, and a table whose keys are named anything other than the
	/// defaults would otherwise produce an identity this subscription invented.
	/// </remarks>
	public DynamoDbStreamsSubscription(
		IAmazonDynamoDB client,
		IAmazonDynamoDBStreams streamsClient,
		string tableName,
		string partitionKeyAttribute,
		string sortKeyAttribute,
		IChangeFeedOptions options,
		ILogger logger)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(partitionKeyAttribute);
		ArgumentException.ThrowIfNullOrWhiteSpace(sortKeyAttribute);

		_client = client ?? throw new ArgumentNullException(nameof(client));
		_streamsClient = streamsClient ?? throw new ArgumentNullException(nameof(streamsClient));
		_tableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
		_partitionKeyAttribute = partitionKeyAttribute;
		_sortKeyAttribute = sortKeyAttribute;
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		SubscriptionId = $"streams-{tableName}-{Guid.NewGuid():N}";
	}

	/// <inheritdoc />
	public string SubscriptionId { get; }

	/// <inheritdoc />
	public bool IsActive => _isActive && !_disposed;

	/// <inheritdoc />
	public string? CurrentContinuationToken { get; private set; }

	/// <inheritdoc />
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		if (_disposed)
		{
			throw new ObjectDisposedException(nameof(DynamoDbStreamsSubscription<>));
		}

		LogStarting(SubscriptionId);

		// Get stream ARN from table
		var describeResponse = await _client.DescribeTableAsync(_tableName, cancellationToken).ConfigureAwait(false);
		_streamArn = describeResponse.Table.LatestStreamArn;

		if (string.IsNullOrEmpty(_streamArn))
		{
			throw new InvalidOperationException($"Table '{_tableName}' does not have streams enabled.");
		}

		// Recreate the CTS if a prior StopAsync canceled it, so stop→start actually resumes.
		lock (_ctsLock)
		{
			if (_cts.IsCancellationRequested)
			{
				_cts.Dispose();
				_cts = new CancellationTokenSource();
			}
		}

		_isActive = true;
	}

	/// <inheritdoc />
	public async Task StopAsync(CancellationToken cancellationToken)
	{
		if (_disposed)
		{
			return;
		}

		LogStopping(SubscriptionId);
		_isActive = false;

		CancellationTokenSource cts;
		lock (_ctsLock)
		{
			cts = _cts;
		}

		await cts.CancelAsync().ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async IAsyncEnumerable<IChangeFeedEvent<TDocument>> ReadChangesAsync(
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		if (_disposed)
		{
			throw new ObjectDisposedException(nameof(DynamoDbStreamsSubscription<>));
		}

		if (string.IsNullOrEmpty(_streamArn))
		{
			throw new InvalidOperationException("Subscription not started. Call StartAsync first.");
		}

		CancellationTokenSource currentCts;
		lock (_ctsLock)
		{
			currentCts = _cts;
		}

		using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, currentCts.Token);
		var linkedToken = linkedCts.Token;

		var shardIterators = new Dictionary<string, string>(StringComparer.Ordinal);
		var retiredShards = new HashSet<string>(StringComparer.Ordinal);
		var firstDiscovery = true;

		long sequenceNumber = 0;

		// The enumeration lives as long as the subscription does, and is deliberately NOT bounded by the
		// shard set that happened to exist when it began. Shards close -- that is how DynamoDB retires a
		// shard and opens its successors -- so a set enumerated once drains to empty in the ordinary
		// course of events. Ending the enumeration there returns a normal completion to the consumer's
		// await-foreach: no exception, no log, IsActive still true, and a change feed that has silently
		// stopped. A subscription created before its first shard exists has the same shape from the start.
		while (_isActive && !linkedToken.IsCancellationRequested)
		{
			try
			{
				await DiscoverShardsAsync(shardIterators, retiredShards, firstDiscovery, linkedToken)
					.ConfigureAwait(false);
			}
			catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
			{
				yield break;
			}

			firstDiscovery = false;

			foreach (var shardId in shardIterators.Keys.ToList())
			{
				var iterator = shardIterators[shardId];
				if (string.IsNullOrEmpty(iterator))
				{
					_ = shardIterators.Remove(shardId);
					continue;
				}

				GetRecordsResponse? recordsResponse = null;
				try
				{
					var recordsRequest = new GetRecordsRequest { ShardIterator = iterator, Limit = _options.MaxBatchSize };

					recordsResponse = await _streamsClient.GetRecordsAsync(recordsRequest, linkedToken)
						.ConfigureAwait(false);
				}
				catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
				{
					yield break;
				}
				catch (ExpiredIteratorException)
				{
					// Shard iterator expired, remove and continue
					_ = shardIterators.Remove(shardId);
					continue;
				}

				// Update iterator for next call
				if (!string.IsNullOrEmpty(recordsResponse.NextShardIterator))
				{
					shardIterators[shardId] = recordsResponse.NextShardIterator;
				}
				else
				{
					// A null next-iterator is how DynamoDB reports that this shard is CLOSED and fully
					// read. Record it so the re-discovery above does not reopen it every poll and
					// redeliver the whole shard; the successors carry on from here.
					_ = shardIterators.Remove(shardId);
					_ = retiredShards.Add(shardId);
				}

				if (recordsResponse.Records.Count == 0)
				{
					continue;
				}

				LogReceivedBatch(SubscriptionId, recordsResponse.Records.Count);

				// Record the position PER SHARD. Assigning one shard's sequence number to a stream-wide
				// scalar is last-writer-wins across shards: whichever shard was polled last overwrites the
				// others, and resuming from it replays or skips every other shard's tail.
				var lastSequenceNumber = recordsResponse.Records[^1].Dynamodb?.SequenceNumber;
				if (!string.IsNullOrEmpty(lastSequenceNumber))
				{
					_shardPositions[shardId] = lastSequenceNumber;
					CurrentContinuationToken = DynamoDbStreamsContinuationToken.Encode(_shardPositions);
				}

				foreach (var record in recordsResponse.Records)
				{
					var eventType = MapEventType(record.EventName);
					TDocument? document = null;

					// Get document from NewImage or OldImage depending on event type
					var image = record.Dynamodb?.NewImage ?? record.Dynamodb?.OldImage;
					if (image != null)
					{
#pragma warning disable IL2026, IL3050
						document = DeserializeDocument(image);
#pragma warning restore IL2026, IL3050
					}

					var documentId = GetDocumentIdFromRecord(record);
					var partitionKey = GetPartitionKeyFromRecord(record);

					yield return new DynamoDbStreamEvent<TDocument>(
						eventType,
						document,
						documentId,
						partitionKey,
						record.Dynamodb?.ApproximateCreationDateTime ?? DateTimeOffset.UtcNow,
						record.Dynamodb?.SequenceNumber ?? string.Empty,
						sequenceNumber++);
				}
			}

			// Wait before polling again. This is NOT conditioned on holding an open shard: with no shard
			// the loop is waiting for one to appear, and skipping the delay would spin.
			if (_isActive && !linkedToken.IsCancellationRequested)
			{
				try
				{
					await Task.Delay(_options.PollingInterval, linkedToken).ConfigureAwait(false);
				}
				catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
				{
					yield break;
				}
			}
		}
	}

	/// <summary>
	/// Re-enumerates the stream's shards and opens an iterator for each one this subscription is not
	/// already reading.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Called on every poll, not once at subscribe time: successor shards created by a split, and shards
	/// that did not exist when the subscription began, are only reachable this way.
	/// </para>
	/// <para>
	/// The FIRST enumeration is required -- there is no shard set to fall back on, so a failure surfaces
	/// to the consumer. Later ones are refreshes: a transient failure keeps the shards already open and
	/// retries on the next poll rather than killing a feed that is delivering.
	/// </para>
	/// </remarks>
	private async Task DiscoverShardsAsync(
		Dictionary<string, string> shardIterators,
		HashSet<string> retiredShards,
		bool required,
		CancellationToken cancellationToken)
	{
		List<Shard> shards;
		string? streamStatus;

		try
		{
			(shards, streamStatus) = await DescribeAllShardsAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (AmazonDynamoDBStreamsException ex) when (!required)
		{
			LogShardRefreshFailed(SubscriptionId, shardIterators.Count, ex);
			return;
		}

		// DISABLED is terminal: the stream is gone, so no shard will ever produce another record and no
		// amount of polling will recover. Surfacing it is the whole point -- the alternative reads to a
		// consumer exactly like a quiet table.
		if (string.Equals(streamStatus, "DISABLED", StringComparison.Ordinal))
		{
			throw new InvalidOperationException(
				$"The DynamoDB stream for table '{_tableName}' is DISABLED; this change feed cannot deliver "
				+ "further changes. Re-enable streams on the table and start a new subscription.");
		}

		// Keep the retired set bounded by what the stream still reports. A shard that has aged out of the
		// description can never come back, so remembering it forever would only grow.
		retiredShards.IntersectWith(shards.Select(static shard => shard.ShardId));

		// Resolved against the LIVE shard set, and only on the initial discovery: a token naming a shard
		// the stream has since dropped is refused here, before any shard is opened, so the subscription
		// never half-starts on a position it cannot honour.
		var resumePositions = required
			? ResolveResumePositions(shards.Select(static shard => shard.ShardId).ToHashSet(StringComparer.Ordinal))
			: null;

		foreach (var shard in shards)
		{
			if (retiredShards.Contains(shard.ShardId) || shardIterators.ContainsKey(shard.ShardId))
			{
				continue;
			}

			// A shard that appears AFTER this subscription began is a successor, and it carries every
			// write since its parent closed. Opening it at LATEST would skip that span silently, which is
			// precisely the gap re-enumeration exists to close -- so only the initial shard set is opened
			// at the position the consumer configured.
			var iteratorRequest = required
				? BuildInitialIteratorRequest(shard.ShardId, resumePositions)
				: new GetShardIteratorRequest
				{
					StreamArn = _streamArn,
					ShardId = shard.ShardId,
					ShardIteratorType = ShardIteratorType.TRIM_HORIZON,
				};

			var iteratorResponse = await _streamsClient.GetShardIteratorAsync(iteratorRequest, cancellationToken)
				.ConfigureAwait(false);

			if (!string.IsNullOrEmpty(iteratorResponse.ShardIterator))
			{
				shardIterators[shard.ShardId] = iteratorResponse.ShardIterator;
			}
		}

		if (shardIterators.Count == 0)
		{
			// Not terminal -- a stream that is still ENABLING, or one whose shards have all been drained,
			// will report shards later. It IS reportable: a change feed delivering nothing used to be
			// indistinguishable from one with nothing to deliver.
			LogNoOpenShards(SubscriptionId, _tableName);
		}
	}

	/// <summary>
	/// Reads the stream description across ALL of its pages.
	/// </summary>
	/// <remarks>
	/// <c>DescribeStream</c> returns a bounded number of shards per response and reports that more remain
	/// with <c>LastEvaluatedShardId</c>, echoed back as <c>ExclusiveStartShardId</c>. Reading the first
	/// response only caps the subscription at that first page -- every later shard is invisible, and the
	/// feed reports healthy progress over the shards it did open.
	/// </remarks>
	private async Task<(List<Shard> Shards, string? StreamStatus)> DescribeAllShardsAsync(
		CancellationToken cancellationToken)
	{
		var shards = new List<Shard>();
		string? streamStatus = null;
		string? exclusiveStartShardId = null;

		do
		{
			var request = new DescribeStreamRequest
			{
				StreamArn = _streamArn,
				ExclusiveStartShardId = exclusiveStartShardId,
			};

			var response = await _streamsClient.DescribeStreamAsync(request, cancellationToken)
				.ConfigureAwait(false);

			streamStatus = response.StreamDescription.StreamStatus?.Value;

			if (response.StreamDescription.Shards is { } page)
			{
				shards.AddRange(page);
			}

			var nextShardId = response.StreamDescription.LastEvaluatedShardId;

			// A continuation that does not advance would loop forever, which is a hang rather than an
			// error. Treat a repeated token as the end of the description.
			exclusiveStartShardId = string.Equals(nextShardId, exclusiveStartShardId, StringComparison.Ordinal)
				? null
				: nextShardId;
		}
		while (!string.IsNullOrEmpty(exclusiveStartShardId));

		return (shards, streamStatus);
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

		CancellationTokenSource cts;
		lock (_ctsLock)
		{
			cts = _cts;
		}

		await cts.CancelAsync().ConfigureAwait(false);
		cts.Dispose();
	}

	private static ChangeFeedEventType MapEventType(OperationType operationType)
	{
		return operationType.Value switch
		{
			"INSERT" => ChangeFeedEventType.Created,
			"MODIFY" => ChangeFeedEventType.Updated,
			"REMOVE" => ChangeFeedEventType.Deleted,
			_ => ChangeFeedEventType.Updated
		};
	}

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)")]
	private static TDocument? DeserializeDocument(
		Dictionary<string, AttributeValue> item)
	{
		var converted = DynamoDbAttributeValueConverter.ToAttributeValueMap(item);
		if (converted is null)
		{
			return null;
		}

		var doc = Document.FromAttributeMap(converted);
		var json = doc.ToJson();
		return JsonSerializer.Deserialize<TDocument>(json);
	}

	/// <summary>
	/// Resolves the change event's document id from the record's key attributes.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The sort key identifies the item within its partition, so it is preferred; a table with no sort key
	/// is identified by its partition key. Both names come from configuration, because they are
	/// configurable: reading a fixed "pk"/"sk" off a table whose keys are named anything else finds
	/// nothing at all.
	/// </para>
	/// <para>
	/// <b>Fail-closed.</b> A record whose configured key attributes are absent is a misconfigured
	/// subscription or a foreign table, and neither is an occasion to invent an identity. The previous
	/// behaviour returned a fresh <c>Guid</c>, which is the worst possible answer for a value consumers
	/// key idempotency on: the same record re-read arrives under a different id, so dedup silently stops
	/// working with no exception and no log to show for it.
	/// </para>
	/// </remarks>
	/// <exception cref="InvalidOperationException">
	/// The record carries neither configured key attribute, or the one it carries holds no scalar value.
	/// </exception>
	private string GetDocumentIdFromRecord(Record record)
	{
		var keys = RequireKeys(record);

		if (keys.TryGetValue(_sortKeyAttribute, out var sortKeyValue))
		{
			return ScalarOrThrow(sortKeyValue, _sortKeyAttribute);
		}

		if (keys.TryGetValue(_partitionKeyAttribute, out var partitionKeyValue))
		{
			return ScalarOrThrow(partitionKeyValue, _partitionKeyAttribute);
		}

		throw new InvalidOperationException(
			$"A change record on table '{_tableName}' carries neither the configured partition key "
			+ $"attribute '{_partitionKeyAttribute}' nor the configured sort key attribute "
			+ $"'{_sortKeyAttribute}' (it carries: {string.Join(", ", keys.Keys)}). The change feed cannot "
			+ "identify the document, and inventing an id would silently defeat consumer idempotency.");
	}

	/// <summary>
	/// Resolves the change event's partition key from the record's configured partition key attribute.
	/// </summary>
	/// <exception cref="InvalidOperationException">
	/// The record does not carry the configured partition key attribute, or it holds no scalar value.
	/// </exception>
	private IPartitionKey GetPartitionKeyFromRecord(Record record)
	{
		var keys = RequireKeys(record);

		if (keys.TryGetValue(_partitionKeyAttribute, out var partitionKeyValue))
		{
			return new PartitionKey(ScalarOrThrow(partitionKeyValue, _partitionKeyAttribute));
		}

		throw new InvalidOperationException(
			$"A change record on table '{_tableName}' does not carry the configured partition key attribute "
			+ $"'{_partitionKeyAttribute}' (it carries: {string.Join(", ", keys.Keys)}). Reporting an empty "
			+ "partition key would route the change as though it belonged to no partition.");
	}

	private Dictionary<string, AttributeValue> RequireKeys(Record record)
	{
		var keys = record.Dynamodb?.Keys;

		return keys is { Count: > 0 }
			? keys
			: throw new InvalidOperationException(
				$"A change record on table '{_tableName}' carries no key attributes. The stream view type "
				+ "must include the item keys for a change feed to identify what changed.");
	}

	/// <summary>
	/// Reads a DynamoDB key attribute's scalar value. Key attributes are S, N or B; only the first two
	/// have a textual form, and a key with no value at all is not an identity.
	/// </summary>
	private string ScalarOrThrow(AttributeValue value, string attributeName)
	{
		var scalar = value.S ?? value.N;

		return !string.IsNullOrEmpty(scalar)
			? scalar
			: throw new InvalidOperationException(
				$"The key attribute '{attributeName}' on a change record from table '{_tableName}' holds no "
				+ "string or numeric value, so it cannot identify the document.");
	}

	/// <summary>
	/// Builds the iterator request for a shard in the INITIAL shard set.
	/// </summary>
	/// <param name="shardId">The shard being opened.</param>
	/// <param name="resumePositions">The decoded per-shard positions, or <see langword="null"/> when not resuming.</param>
	/// <returns>A request carrying the sequence number for this shard when one is being resumed.</returns>
	/// <remarks>
	/// <para>
	/// <c>AFTER_SEQUENCE_NUMBER</c> requires a sequence number, and DynamoDB rejects the request without
	/// one. It must come from the entry for <b>this</b> shard: a stream has many shards and each has its
	/// own sequence space, so another shard's number is not merely imprecise, it is meaningless here.
	/// </para>
	/// <para>
	/// A shard present in the stream but absent from the token is opened at <c>TRIM_HORIZON</c>. It is
	/// either a successor created since the token was taken or a shard nothing had yet been read from;
	/// in both cases its records have not been delivered, and starting at its beginning delivers them
	/// rather than skipping them.
	/// </para>
	/// </remarks>
	private GetShardIteratorRequest BuildInitialIteratorRequest(
		string shardId,
		IReadOnlyDictionary<string, string>? resumePositions)
	{
		if (resumePositions is not null)
		{
			return resumePositions.TryGetValue(shardId, out var sequenceNumber)
				? new GetShardIteratorRequest
				{
					StreamArn = _streamArn,
					ShardId = shardId,
					ShardIteratorType = ShardIteratorType.AFTER_SEQUENCE_NUMBER,
					SequenceNumber = sequenceNumber,
				}
				: new GetShardIteratorRequest
				{
					StreamArn = _streamArn,
					ShardId = shardId,
					ShardIteratorType = ShardIteratorType.TRIM_HORIZON,
				};
		}

		return new GetShardIteratorRequest
		{
			StreamArn = _streamArn,
			ShardId = shardId,
			ShardIteratorType = _options.StartPosition switch
			{
				ChangeFeedStartPosition.Beginning => ShardIteratorType.TRIM_HORIZON,
				ChangeFeedStartPosition.Now => ShardIteratorType.LATEST,
				_ => ShardIteratorType.LATEST,
			},
		};
	}

	/// <summary>
	/// Reads the resume positions from the configured continuation token, refusing a token whose shards
	/// the stream no longer has.
	/// </summary>
	/// <param name="liveShardIds">The shard ids the stream currently reports.</param>
	/// <returns>The per-shard positions, or <see langword="null"/> when not resuming from a token.</returns>
	/// <exception cref="InvalidOperationException">
	/// The token names a shard that no longer exists, so the consumer's position in it is unknown.
	/// </exception>
	/// <remarks>
	/// DynamoDB Streams shards expire after roughly 24 hours, so a stored token can name a shard the
	/// stream has since dropped. Both silent recoveries are data defects and the second is unrecoverable:
	/// starting that shard at <c>TRIM_HORIZON</c> replays everything the consumer already processed, and
	/// starting it at <c>LATEST</c> skips whatever arrived in the gap without saying so. The position is
	/// genuinely unknown, so this refuses and names the shard, leaving the operator to choose replay or
	/// skip deliberately.
	/// </remarks>
	private IReadOnlyDictionary<string, string>? ResolveResumePositions(HashSet<string> liveShardIds)
	{
		if (_options.StartPosition != ChangeFeedStartPosition.FromContinuationToken
			|| string.IsNullOrEmpty(_options.ContinuationToken))
		{
			return null;
		}

		var positions = DynamoDbStreamsContinuationToken.Decode(_options.ContinuationToken);

		var missing = positions.Keys.Where(shardId => !liveShardIds.Contains(shardId)).ToList();
		if (missing.Count > 0)
		{
			throw new InvalidOperationException(
				$"The change-feed continuation token names shard(s) the DynamoDB stream for table '{_tableName}' no longer has: "
				+ $"{string.Join(", ", missing)}. Stream shards expire after about 24 hours, so this position can no longer be "
				+ "resumed and the records in that span cannot be located. Resuming from the start of the stream would redeliver "
				+ "everything already processed, and resuming from the end would silently skip whatever arrived in the gap, so "
				+ "neither is chosen for you. Start a new subscription from an explicit position once you have decided which is correct.");
		}

		return positions;
	}
}

/// <summary>
/// DynamoDB Stream event implementation.
/// </summary>
/// <typeparam name="TDocument"> The document type. </typeparam>
public sealed class DynamoDbStreamEvent<TDocument> : IChangeFeedEvent<TDocument>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="DynamoDbStreamEvent{TDocument}" /> class.
	/// </summary>
	public DynamoDbStreamEvent(
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

	/// <inheritdoc />
	public ChangeFeedEventType EventType { get; }

	/// <inheritdoc />
	public TDocument? Document { get; }

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
