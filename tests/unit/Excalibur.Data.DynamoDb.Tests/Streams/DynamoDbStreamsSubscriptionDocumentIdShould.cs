// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Amazon.DynamoDBStreams;
using Amazon.DynamoDBStreams.Model;
using Amazon.DynamoDBv2;

using Excalibur.Data.CloudNative;

using Microsoft.Extensions.Logging.Abstractions;

using StreamsRecord = Amazon.DynamoDBStreams.Model.Record;
using TableDescription = Amazon.DynamoDBv2.Model.TableDescription;

namespace Excalibur.Data.DynamoDb.Tests.Streams;

/// <summary>
/// Regression lock (SAFETY-CRITICAL, silent idempotency defeat) for the identity
/// <see cref="DynamoDbStreamsSubscription{TDocument}"/> puts on a change event.
/// </summary>
/// <remarks>
/// <para>
/// <b>The invariant:</b> <c>DocumentId</c> is the item's real key value, read from the key attributes the
/// table actually uses — or the subscription fails. It is never a value the subscription made up.
/// </para>
/// <para>
/// <b>The defect this locks:</b> the key attribute names were hardcoded to the defaults while the
/// provider's key-attribute options are public, settable and shipped. On a table whose keys are named
/// anything else the lookups all missed, and each miss returned a fresh <c>Guid</c>. Every change event
/// then carried a unique, meaningless id — so a consumer keying idempotency on <c>DocumentId</c> saw the
/// same record arrive under a different id every time it was re-read, and dedup stopped working with no
/// exception and no log.
/// </para>
/// <para>
/// <b>Why fail-closed:</b> a record whose configured key attributes are absent is a misconfigured
/// subscription or a foreign table. Neither is an occasion to invent an identity, and a fabricated one is
/// strictly worse than an error because it cannot be noticed.
/// </para>
/// <para>
/// <b>Non-vacuity:</b> <see cref="ReportTheRealKeyValue_WhenTheKeyAttributesAreRenamed"/> is RED against
/// the pre-fix implementation, which answered with a fresh <c>Guid</c> for exactly this table shape;
/// <see cref="ReportTheRealKeyValue_UnderTheDefaultAttributeNames"/> is its counterweight, failing against
/// any implementation that simply throws.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "DynamoDb")]
public sealed class DynamoDbStreamsSubscriptionDocumentIdShould
{
	private const string TableName = "orders";
	private const string StreamArn = "arn:aws:dynamodb:us-east-1:000000000000:table/orders/stream/x";
	private const string ShardId = "shardId-00000001700000000000-a1b2c3d4";

	// Scaled by TEST_TIMEOUT_MULTIPLIER (3 on CI): this budget bounds a wait on real background
	// work, so a fixed value times out on a loaded runner with nothing actually wrong.
	private static readonly TimeSpan Timeout =
		global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(30));

	/// <summary>
	/// THE ARM THAT MATTERS. A table whose key attributes are named anything other than the defaults still
	/// yields the real key values.
	/// </summary>
	[Fact]
	public async Task ReportTheRealKeyValue_WhenTheKeyAttributesAreRenamed()
	{
		var harness = new Harness
		{
			PartitionKeyAttribute = "TenantId",
			SortKeyAttribute = "OrderNumber",
		};

		harness.Enqueue(new Dictionary<string, AttributeValue>(StringComparer.Ordinal)
		{
			["TenantId"] = new() { S = "tenant-7" },
			["OrderNumber"] = new() { S = "order-42" },
		});

		var change = await harness.ReadOneAsync();

		change.DocumentId.ShouldBe(
			"order-42",
			"the pre-fix implementation looked for the DEFAULT attribute names, missed, and returned a "
			+ "fresh Guid — so the same record re-read arrived under a different id every time and any "
			+ "consumer keying idempotency on it silently stopped deduplicating.");

		change.PartitionKey.Value.ShouldBe(
			"tenant-7",
			"the partition key is read from the configured attribute too; reporting an empty one routes "
			+ "the change as though it belonged to no partition.");
	}

	/// <summary>
	/// LIVENESS counterweight. The default attribute names keep working, so the arm above cannot be
	/// satisfied by an implementation that rejects everything.
	/// </summary>
	[Fact]
	public async Task ReportTheRealKeyValue_UnderTheDefaultAttributeNames()
	{
		var harness = new Harness();

		harness.Enqueue(new Dictionary<string, AttributeValue>(StringComparer.Ordinal)
		{
			["pk"] = new() { S = "tenant-7" },
			["sk"] = new() { S = "order-42" },
		});

		var change = await harness.ReadOneAsync();

		change.DocumentId.ShouldBe("order-42");
		change.PartitionKey.Value.ShouldBe("tenant-7");
	}

	/// <summary>
	/// PRECISION. The sort key identifies the item within its partition, so it is preferred over the
	/// partition key when both are present.
	/// </summary>
	[Fact]
	public async Task PreferTheSortKey_WhenTheTableHasBoth()
	{
		var harness = new Harness();

		harness.Enqueue(new Dictionary<string, AttributeValue>(StringComparer.Ordinal)
		{
			["pk"] = new() { S = "same-partition" },
			["sk"] = new() { S = "the-item" },
		});

		(await harness.ReadOneAsync()).DocumentId.ShouldBe(
			"the-item",
			"every item in a partition shares its partition key; using it as the document id would collapse "
			+ "them all onto one identity.");
	}

	/// <summary>
	/// PRECISION. A table with no sort key is identified by its partition key.
	/// </summary>
	[Fact]
	public async Task FallBackToThePartitionKey_WhenTheTableHasNoSortKey()
	{
		var harness = new Harness();

		harness.Enqueue(new Dictionary<string, AttributeValue>(StringComparer.Ordinal)
		{
			["pk"] = new() { S = "the-only-key" },
		});

		(await harness.ReadOneAsync()).DocumentId.ShouldBe("the-only-key");
	}

	/// <summary>
	/// PRECISION. A numeric key is a key.
	/// </summary>
	[Fact]
	public async Task ReadANumericKeyValue()
	{
		var harness = new Harness();

		harness.Enqueue(new Dictionary<string, AttributeValue>(StringComparer.Ordinal)
		{
			["pk"] = new() { N = "1001" },
		});

		(await harness.ReadOneAsync()).DocumentId.ShouldBe("1001");
	}

	/// <summary>
	/// SAFETY. A record carrying neither configured key attribute FAILS rather than being handed an
	/// invented identity.
	/// </summary>
	[Fact]
	public async Task FailClosed_WhenTheRecordCarriesNeitherConfiguredKeyAttribute()
	{
		var harness = new Harness { PartitionKeyAttribute = "TenantId", SortKeyAttribute = "OrderNumber" };

		harness.Enqueue(new Dictionary<string, AttributeValue>(StringComparer.Ordinal)
		{
			["pk"] = new() { S = "tenant-7" },
			["sk"] = new() { S = "order-42" },
		});

		var failure = await Should.ThrowAsync<InvalidOperationException>(harness.ReadOneAsync);

		failure.Message.ShouldContain(
			"TenantId",
			customMessage: "the error must name the attributes that were expected, or a misconfiguration "
				+ "is as hard to diagnose as the silent Guid it replaced.");

		failure.Message.ShouldContain("pk", customMessage: "and the attributes the record actually carries.");
	}

	/// <summary>
	/// SAFETY. A record with no key attributes at all fails rather than inventing one.
	/// </summary>
	[Fact]
	public async Task FailClosed_WhenTheRecordCarriesNoKeyAttributesAtAll()
	{
		var harness = new Harness();
		harness.Enqueue(new Dictionary<string, AttributeValue>(StringComparer.Ordinal));

		_ = await Should.ThrowAsync<InvalidOperationException>(harness.ReadOneAsync);
	}

	/// <summary>
	/// SAFETY. A key attribute present but holding no scalar value is not an identity either.
	/// </summary>
	[Fact]
	public async Task FailClosed_WhenTheKeyAttributeHoldsNoScalarValue()
	{
		var harness = new Harness();

		// A binary key: present, and with no string or numeric form to identify the item by.
		harness.Enqueue(new Dictionary<string, AttributeValue>(StringComparer.Ordinal)
		{
			["pk"] = new() { B = new MemoryStream([1, 2, 3]) },
		});

		_ = await Should.ThrowAsync<InvalidOperationException>(harness.ReadOneAsync);
	}

	/// <summary>
	/// SAFETY. The key attribute names are required at construction: an empty one would silently match
	/// nothing on every record.
	/// </summary>
	[Theory]
	[InlineData("", "sk")]
	[InlineData(" ", "sk")]
	[InlineData("pk", "")]
	[InlineData("pk", " ")]
	public void RejectABlankKeyAttributeName(string partitionKeyAttribute, string sortKeyAttribute)
	{
		var options = A.Fake<IChangeFeedOptions>();

		_ = Should.Throw<ArgumentException>(() => new DynamoDbStreamsSubscription<Doc>(
			A.Fake<IAmazonDynamoDB>(),
			A.Fake<IAmazonDynamoDBStreams>(),
			TableName,
			partitionKeyAttribute,
			sortKeyAttribute,
			options,
			NullLogger.Instance));
	}

	// ─── Helpers ────────────────────────────────────────────────────────────

	private sealed class Doc
	{
		public string? Id { get; set; }
	}

	private sealed class Harness
	{
		private readonly Queue<StreamsRecord> _pending = new();

		public Harness()
		{
			Streams = A.Fake<IAmazonDynamoDBStreams>();

			A.CallTo(() => Streams.DescribeStreamAsync(A<DescribeStreamRequest>._, A<CancellationToken>._))
				.Returns(new DescribeStreamResponse
				{
					StreamDescription = new StreamDescription
					{
						StreamStatus = StreamStatus.ENABLED,
						Shards = [new Shard { ShardId = ShardId }],
					},
				});

			A.CallTo(() => Streams.GetShardIteratorAsync(A<GetShardIteratorRequest>._, A<CancellationToken>._))
				.Returns(new GetShardIteratorResponse { ShardIterator = "iterator" });

			A.CallTo(() => Streams.GetRecordsAsync(A<GetRecordsRequest>._, A<CancellationToken>._))
				.ReturnsLazily((GetRecordsRequest request, CancellationToken _) =>
				{
					List<StreamsRecord> batch = [];
					while (_pending.Count > 0)
					{
						batch.Add(_pending.Dequeue());
					}

					return new GetRecordsResponse { Records = batch, NextShardIterator = request.ShardIterator };
				});

			Client = A.Fake<IAmazonDynamoDB>();
			A.CallTo(() => Client.DescribeTableAsync(TableName, A<CancellationToken>._))
				.Returns(new Amazon.DynamoDBv2.Model.DescribeTableResponse
				{
					Table = new TableDescription { LatestStreamArn = StreamArn },
				});
		}

		public IAmazonDynamoDBStreams Streams { get; }

		public IAmazonDynamoDB Client { get; }

		public string PartitionKeyAttribute { get; init; } = "pk";

		public string SortKeyAttribute { get; init; } = "sk";

		public void Enqueue(Dictionary<string, AttributeValue> keys) =>
			_pending.Enqueue(new StreamsRecord
			{
				EventID = "e1",
				EventName = OperationType.INSERT,
				Dynamodb = new StreamRecord
				{
					SequenceNumber = "000000000000000000001",
					ApproximateCreationDateTime = DateTime.UtcNow,
					Keys = keys,
				},
			});

		/// <summary>Starts the subscription and returns its first change event.</summary>
		public async Task<IChangeFeedEvent<Doc>> ReadOneAsync()
		{
			var options = A.Fake<IChangeFeedOptions>();
			A.CallTo(() => options.PollingInterval).Returns(TimeSpan.FromMilliseconds(1));
			A.CallTo(() => options.MaxBatchSize).Returns(100);
			A.CallTo(() => options.StartPosition).Returns(ChangeFeedStartPosition.Beginning);

			await using var subscription = new DynamoDbStreamsSubscription<Doc>(
				Client, Streams, TableName, PartitionKeyAttribute, SortKeyAttribute, options, NullLogger.Instance);

			await subscription.StartAsync(CancellationToken.None).ConfigureAwait(false);

			using var cts = new CancellationTokenSource();
			await using var enumerator = subscription.ReadChangesAsync(cts.Token)
				.GetAsyncEnumerator(cts.Token);

			var moved = await enumerator.MoveNextAsync().AsTask().WaitAsync(Timeout).ConfigureAwait(false);
			moved.ShouldBeTrue("the harness seeded exactly one record; the feed must deliver it.");

			var change = enumerator.Current;
			await cts.CancelAsync().ConfigureAwait(false);

			return change;
		}
	}
}
