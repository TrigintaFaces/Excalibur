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
/// Regression lock (SAFETY-CRITICAL, silent stall) for shard enumeration in
/// <see cref="DynamoDbStreamsSubscription{TDocument}"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>The invariant:</b> while a subscription is active, its change feed keeps delivering — across shard
/// closure, shard succession, and shards that did not exist when it began.
/// </para>
/// <para>
/// <b>The defect this locks:</b> the shard set was enumerated exactly ONCE, before the poll loop, and the
/// loop was guarded by that set being non-empty. A shard is removed when <c>GetRecords</c> returns a null
/// next-iterator, which is precisely how DynamoDB reports a shard CLOSED — so once every shard that
/// existed at subscribe time had closed, the set emptied, the loop condition went false, and the
/// <c>IAsyncEnumerable</c> COMPLETED NORMALLY. The consumer's await-foreach simply ended: no exception, no
/// log, and <c>IsActive</c> still true. A subscription created before its first shard existed had the same
/// shape from the start and delivered nothing, ever.
/// </para>
/// <para>
/// <b>Non-vacuity:</b> <see cref="KeepDelivering_AfterEveryShardOpenAtSubscribeTimeHasClosed"/> and
/// <see cref="KeepTheFeedOpen_WhenNoShardExistsYet"/> are RED against the pre-fix implementation — in both
/// the enumeration ends and the very first assertion (that another element arrives) fails.
/// <see cref="NotReopenAShardThatHasBeenReadToItsEnd"/> is their counterweight: it fails against an
/// implementation that "keeps delivering" by reopening closed shards and redelivering them forever.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "DynamoDb")]
public sealed class DynamoDbStreamsSubscriptionShardRotationShould
{
	private const string TableName = "orders";
	private const string StreamArn = "arn:aws:dynamodb:us-east-1:000000000000:table/orders/stream/x";
	private const string FirstShardId = "shardId-00000001700000000000-first";
	private const string SuccessorShardId = "shardId-00000001700000000001-successor";

	// Scaled by TEST_TIMEOUT_MULTIPLIER (3 on CI): this budget bounds a wait on real background
	// work, so a fixed value times out on a loaded runner with nothing actually wrong.
	private static readonly TimeSpan Timeout =
		global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(30));

	/// <summary>
	/// THE ARM THAT MATTERS. The shard open at subscribe time closes and a successor takes over; records
	/// written afterwards must still reach the consumer.
	/// </summary>
	[Fact]
	public async Task KeepDelivering_AfterEveryShardOpenAtSubscribeTimeHasClosed()
	{
		var harness = new Harness();
		harness.AddShard(FirstShardId);
		harness.Enqueue(FirstShardId, "before-the-split");

		await using var subscription = harness.BuildSubscription();
		await subscription.StartAsync(CancellationToken.None);

		using var cts = new CancellationTokenSource();
		await using var enumerator = subscription.ReadChangesAsync(cts.Token).GetAsyncEnumerator(cts.Token);

		(await MoveNextAsync(enumerator)).ShouldBeTrue("sanity: the feed must deliver before the split.");
		enumerator.Current.DocumentId.ShouldBe("before-the-split");

		// The shard closes — DynamoDB reports that with a null next-iterator — and a successor appears
		// carrying everything written from here on.
		harness.CloseShard(FirstShardId);
		harness.AddShard(SuccessorShardId);
		harness.Enqueue(SuccessorShardId, "after-the-split");

		(await MoveNextAsync(enumerator)).ShouldBeTrue(
			"the enumeration must not end when the shards it started with close. Ending here returns a "
			+ "normal completion to the consumer's await-foreach — no exception, no log, IsActive still "
			+ "true — and the change feed is silently dead.");

		enumerator.Current.DocumentId.ShouldBe(
			"after-the-split",
			"the successor shard's records are the whole point: a re-enumeration that opens the shard but "
			+ "never reads it would be a fix in name only.");

		await cts.CancelAsync();
	}

	/// <summary>
	/// SAFETY. A subscription created before the stream has any shard stays open and delivers once one
	/// appears — the state a table whose stream is still enabling is in.
	/// </summary>
	[Fact]
	public async Task KeepTheFeedOpen_WhenNoShardExistsYet()
	{
		var harness = new Harness();

		await using var subscription = harness.BuildSubscription();
		await subscription.StartAsync(CancellationToken.None);

		using var cts = new CancellationTokenSource();
		await using var enumerator = subscription.ReadChangesAsync(cts.Token).GetAsyncEnumerator(cts.Token);

		var pending = MoveNextAsync(enumerator);

		harness.AddShard(FirstShardId);
		harness.Enqueue(FirstShardId, "the-first-record-ever");

		(await pending).ShouldBeTrue(
			"an empty initial shard set ended the enumeration immediately, so a subscription created "
			+ "before its stream was ready delivered nothing for its whole lifetime.");

		enumerator.Current.DocumentId.ShouldBe("the-first-record-ever");

		await cts.CancelAsync();
	}

	/// <summary>
	/// LIVENESS counterweight. A shard read to its end is NOT reopened, so the fix above cannot be
	/// satisfied by redelivering closed shards forever.
	/// </summary>
	[Fact]
	public async Task NotReopenAShardThatHasBeenReadToItsEnd()
	{
		var harness = new Harness();
		harness.AddShard(FirstShardId);
		harness.Enqueue(FirstShardId, "only-record");

		await using var subscription = harness.BuildSubscription();
		await subscription.StartAsync(CancellationToken.None);

		using var cts = new CancellationTokenSource();
		await using var enumerator = subscription.ReadChangesAsync(cts.Token).GetAsyncEnumerator(cts.Token);

		(await MoveNextAsync(enumerator)).ShouldBeTrue();

		// Close it, and keep it listed in the stream description the way DynamoDB does.
		harness.CloseShard(FirstShardId);
		harness.AddShard(SuccessorShardId);
		harness.Enqueue(SuccessorShardId, "successor-record");

		(await MoveNextAsync(enumerator)).ShouldBeTrue();
		enumerator.Current.DocumentId.ShouldBe("successor-record");

		harness.IteratorRequests
			.Count(r => string.Equals(r.ShardId, FirstShardId, StringComparison.Ordinal))
			.ShouldBe(
				1,
				"the closed shard must be opened once and never again; reopening it on every re-enumeration "
				+ "would redeliver the whole shard on every poll.");

		await cts.CancelAsync();
	}

	/// <summary>
	/// SAFETY. A shard that appears AFTER the subscription began is opened at the start of its retained
	/// history, not at LATEST — even when the consumer asked to start from now.
	/// </summary>
	/// <remarks>
	/// A successor carries every write since its parent closed. Opening it at <c>LATEST</c> re-enumerates
	/// the shard while still skipping that span, which is exactly the gap re-enumeration exists to close;
	/// the start position the consumer configured is a statement about where the FEED begins, not about
	/// each shard the feed later inherits.
	/// </remarks>
	[Fact]
	public async Task OpenALaterDiscoveredShardAtTrimHorizon_EvenWhenStartingFromNow()
	{
		var harness = new Harness { StartPosition = ChangeFeedStartPosition.Now };
		harness.AddShard(FirstShardId);
		harness.Enqueue(FirstShardId, "first");

		await using var subscription = harness.BuildSubscription();
		await subscription.StartAsync(CancellationToken.None);

		using var cts = new CancellationTokenSource();
		await using var enumerator = subscription.ReadChangesAsync(cts.Token).GetAsyncEnumerator(cts.Token);

		(await MoveNextAsync(enumerator)).ShouldBeTrue();

		harness.IteratorRequests.Single(r => r.ShardId == FirstShardId).ShardIteratorType.ShouldBe(
			ShardIteratorType.LATEST,
			"sanity: the initial shard set honours the configured start position.");

		harness.CloseShard(FirstShardId);
		harness.AddShard(SuccessorShardId);
		harness.Enqueue(SuccessorShardId, "second");

		(await MoveNextAsync(enumerator)).ShouldBeTrue();

		harness.IteratorRequests.Single(r => r.ShardId == SuccessorShardId).ShardIteratorType.ShouldBe(
			ShardIteratorType.TRIM_HORIZON,
			"a successor discovered mid-subscription must be read from the beginning of what it retains, "
			+ "or every split silently drops the records written between the split and the next poll.");

		await cts.CancelAsync();
	}

	/// <summary>
	/// SAFETY. A shard reported only on a later page of the stream description is opened and delivered.
	/// </summary>
	[Fact]
	public async Task DeliverFromAShardOnASecondDescribeStreamPage()
	{
		var harness = new Harness();
		harness.AddShard(FirstShardId);
		harness.AddShard(SuccessorShardId, onSecondPage: true);
		harness.Enqueue(SuccessorShardId, "page-two-record");

		await using var subscription = harness.BuildSubscription();
		await subscription.StartAsync(CancellationToken.None);

		using var cts = new CancellationTokenSource();
		await using var enumerator = subscription.ReadChangesAsync(cts.Token).GetAsyncEnumerator(cts.Token);

		(await MoveNextAsync(enumerator)).ShouldBeTrue(
			"DescribeStream caps the shards it returns per response and reports the rest with a "
			+ "continuation; reading only the first response strands every shard beyond it.");

		enumerator.Current.DocumentId.ShouldBe("page-two-record");

		harness.DescribeTokens.ShouldBe(
			[null, FirstShardId],
			"the second page must be requested with the first page's continuation token.");

		await cts.CancelAsync();
	}

	/// <summary>
	/// SAFETY. A stream that has been DISABLED can never deliver again, so the subscription says so rather
	/// than ending as though the table had simply gone quiet.
	/// </summary>
	[Fact]
	public async Task SurfaceADisabledStream_RatherThanCompletingSilently()
	{
		var harness = new Harness { StreamStatus = StreamStatus.DISABLED };
		harness.AddShard(FirstShardId);

		await using var subscription = harness.BuildSubscription();
		await subscription.StartAsync(CancellationToken.None);

		using var cts = new CancellationTokenSource();
		await using var enumerator = subscription.ReadChangesAsync(cts.Token).GetAsyncEnumerator(cts.Token);

		// Bounded deliberately. A regression that stops surfacing DISABLED leaves the loop polling a
		// stream that will never produce a shard, so an unbounded enumeration here would HANG the suite
		// instead of failing it -- and a hung run is far harder to diagnose than a red one. The timeout
		// converts that regression into a TimeoutException, which fails this assertion cleanly.
		var failure = await Should.ThrowAsync<InvalidOperationException>(
			() => enumerator.MoveNextAsync().AsTask().WaitAsync(Timeout));

		failure.Message.ShouldContain(
			TableName,
			customMessage: "the consumer needs to know WHICH feed stopped.");
	}

	/// <summary>
	/// SAFETY. A transient failure while REFRESHING the shard set does not kill a feed that is already
	/// delivering; the shards already open keep producing and the refresh retries.
	/// </summary>
	/// <remarks>
	/// The first enumeration is deliberately different — there is no shard set to fall back on, so it
	/// surfaces. Re-enumerating on every poll would otherwise turn every transient throttle into a dead
	/// subscription, which would be a worse feed than the one this change set out to fix.
	/// </remarks>
	[Fact]
	public async Task SurviveATransientFailureWhileRefreshingTheShardSet()
	{
		var harness = new Harness();
		harness.AddShard(FirstShardId);
		harness.Enqueue(FirstShardId, "first");

		await using var subscription = harness.BuildSubscription();
		await subscription.StartAsync(CancellationToken.None);

		using var cts = new CancellationTokenSource();
		await using var enumerator = subscription.ReadChangesAsync(cts.Token).GetAsyncEnumerator(cts.Token);

		(await MoveNextAsync(enumerator)).ShouldBeTrue("sanity: the feed is delivering.");

		harness.FailNextDescribes(2);
		harness.Enqueue(FirstShardId, "second");

		(await MoveNextAsync(enumerator)).ShouldBeTrue(
			"a failed refresh must not end the enumeration — the shards already open can still deliver, "
			+ "and the refresh retries on the next poll.");

		enumerator.Current.DocumentId.ShouldBe("second");

		await cts.CancelAsync();
	}

	// ─── Helpers ────────────────────────────────────────────────────────────

	private static Task<bool> MoveNextAsync(IAsyncEnumerator<IChangeFeedEvent<Doc>> enumerator) =>
		enumerator.MoveNextAsync().AsTask().WaitAsync(Timeout);

	private sealed class Doc
	{
		public string? Id { get; set; }
	}

	private sealed class Harness
	{
		private readonly object _gate = new();
		private readonly List<(string ShardId, bool SecondPage)> _shards = [];
		private readonly HashSet<string> _closedShards = new(StringComparer.Ordinal);
		private readonly Dictionary<string, Queue<string>> _pending = new(StringComparer.Ordinal);
		private int _failingDescribes;

		public Harness()
		{
			Streams = A.Fake<IAmazonDynamoDBStreams>();

			A.CallTo(() => Streams.DescribeStreamAsync(A<DescribeStreamRequest>._, A<CancellationToken>._))
				.ReturnsLazily((DescribeStreamRequest request, CancellationToken _) =>
				{
					lock (_gate)
					{
						DescribeTokens.Add(request.ExclusiveStartShardId);

						if (_failingDescribes > 0)
						{
							_failingDescribes--;
							throw new AmazonDynamoDBStreamsException("injected describe-stream failure");
						}

						// Page one is every shard not marked second-page; page two is the rest. The page-one
						// response carries a continuation only when a second page exists.
						var onSecondPage = request.ExclusiveStartShardId is not null;
						var page = _shards.Where(s => s.SecondPage == onSecondPage).ToList();
						var hasSecondPage = !onSecondPage && _shards.Exists(s => s.SecondPage);

						return new DescribeStreamResponse
						{
							StreamDescription = new StreamDescription
							{
								StreamStatus = StreamStatus,
								Shards = [.. page.Select(s => new Shard { ShardId = s.ShardId })],
								LastEvaluatedShardId = hasSecondPage ? page[^1].ShardId : null,
							},
						};
					}
				});

			A.CallTo(() => Streams.GetShardIteratorAsync(A<GetShardIteratorRequest>._, A<CancellationToken>._))
				.ReturnsLazily((GetShardIteratorRequest request, CancellationToken _) =>
				{
					lock (_gate)
					{
						IteratorRequests.Add(request);
					}

					return new GetShardIteratorResponse { ShardIterator = "iterator|" + request.ShardId };
				});

			A.CallTo(() => Streams.GetRecordsAsync(A<GetRecordsRequest>._, A<CancellationToken>._))
				.ReturnsLazily((GetRecordsRequest request, CancellationToken _) =>
				{
					var shardId = request.ShardIterator.Split('|')[1];

					lock (_gate)
					{
						List<StreamsRecord> batch = [];
						if (_pending.TryGetValue(shardId, out var queue))
						{
							while (queue.Count > 0)
							{
								batch.Add(MakeRecord(queue.Dequeue()));
							}
						}

						// A closed shard hands back its remaining records and then reports closure with a
						// null next-iterator, exactly as DynamoDB does.
						var closed = _closedShards.Contains(shardId) && batch.Count == 0;

						return new GetRecordsResponse
						{
							Records = batch,
							NextShardIterator = closed ? null : request.ShardIterator,
						};
					}
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

		public List<GetShardIteratorRequest> IteratorRequests { get; } = [];

		public List<string?> DescribeTokens { get; } = [];

		public StreamStatus StreamStatus { get; init; } = StreamStatus.ENABLED;

		public ChangeFeedStartPosition StartPosition { get; init; } = ChangeFeedStartPosition.Beginning;

		public void AddShard(string shardId, bool onSecondPage = false)
		{
			lock (_gate)
			{
				_shards.Add((shardId, onSecondPage));
			}
		}

		public void CloseShard(string shardId)
		{
			lock (_gate)
			{
				_ = _closedShards.Add(shardId);
			}
		}

		public void Enqueue(string shardId, string documentId)
		{
			lock (_gate)
			{
				if (!_pending.TryGetValue(shardId, out var queue))
				{
					queue = new Queue<string>();
					_pending[shardId] = queue;
				}

				queue.Enqueue(documentId);
			}
		}

		public void FailNextDescribes(int count)
		{
			lock (_gate)
			{
				_failingDescribes = count;
			}
		}

		public DynamoDbStreamsSubscription<Doc> BuildSubscription()
		{
			var options = A.Fake<IChangeFeedOptions>();
			A.CallTo(() => options.PollingInterval).Returns(TimeSpan.FromMilliseconds(1));
			A.CallTo(() => options.MaxBatchSize).Returns(100);
			A.CallTo(() => options.StartPosition).Returns(StartPosition);

			return new DynamoDbStreamsSubscription<Doc>(
				Client, Streams, TableName, "pk", "sk", options, NullLogger.Instance);
		}

		private static StreamsRecord MakeRecord(string documentId) => new()
		{
			EventID = documentId,
			EventName = OperationType.INSERT,
			Dynamodb = new StreamRecord
			{
				SequenceNumber = documentId,
				ApproximateCreationDateTime = DateTime.UtcNow,
				Keys = new Dictionary<string, AttributeValue>(StringComparer.Ordinal)
				{
					["pk"] = new() { S = "partition-1" },
					["sk"] = new() { S = documentId },
				},
			},
		};
	}
}
