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
/// Binds the rule that a change feed resumed from a continuation token asks the service for a position it
/// can actually serve, and refuses when it cannot.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect this locks:</b> resuming selected <c>AFTER_SEQUENCE_NUMBER</c> but the request was built
/// with only <c>StreamArn</c>, <c>ShardId</c> and the iterator type -- <c>SequenceNumber</c> was never
/// set. DynamoDB Streams requires one for that iterator type, so the service rejected the call and the
/// subscription could not start at all.
/// </para>
/// <para>
/// <b>And a scalar token could not have been right anyway.</b> A stream has many shards, each with its own
/// sequence space, so one sequence number is meaningless in every shard but the one that produced it. The
/// position is now a per-shard map carried inside the single opaque token, which is the shape the Cosmos
/// change feed uses for the same reason.
/// </para>
/// <para>
/// <b>The refusal arms protect a decision, not just a behaviour.</b> Stream shards expire after about 24
/// hours, so a stored token can name a shard that no longer exists. Both silent recoveries are data
/// defects -- starting that shard at <c>TRIM_HORIZON</c> replays everything already processed, and
/// starting it at <c>LATEST</c> skips whatever arrived in the gap, unrecoverably and without a word. These
/// arms exist so that a later change making the feed "more robust" cannot quietly reintroduce either.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "DynamoDb")]
public sealed class DynamoDbStreamsSubscriptionResumeShould
{
	private const string TableName = "orders";
	private const string StreamArn = "arn:aws:dynamodb:us-east-1:000000000000:table/orders/stream/x";
	private const string ShardA = "shardId-00000001700000000000-a";
	private const string ShardB = "shardId-00000001700000000001-b";

	/// <summary>
	/// SAFETY -- the ruled arm. A resumed subscription asks for <c>AFTER_SEQUENCE_NUMBER</c> AND supplies
	/// the sequence number for the shard it names. RED against the previous shape, where the request
	/// carried the iterator type with no sequence number and the service rejected it.
	/// </summary>
	[Fact]
	public async Task RequestAnIteratorCarryingTheSequenceNumberForTheShardItNames()
	{
		// Establish a real position by reading, then resume from the token the feed itself produced.
		var source = new Harness();
		source.AddShard(ShardA);
		source.Enqueue(ShardA, "record-1");
		var token = await CaptureTokenAsync(source, expectedRecords: 1);

		token.ShouldNotBeNullOrEmpty("the feed must publish a continuation token once it has read a record");

		var resumed = new Harness
		{
			StartPosition = ChangeFeedStartPosition.FromContinuationToken,
			ContinuationToken = token,
		};
		resumed.AddShard(ShardA);

		await DriveOnceAsync(resumed);

		var request = resumed.IteratorRequests.Find(r => r.ShardId == ShardA);
		request.ShouldNotBeNull();
		request.ShardIteratorType.ShouldBe(ShardIteratorType.AFTER_SEQUENCE_NUMBER);
		request.SequenceNumber.ShouldBe(
			"record-1",
			"the sequence number must come from the entry for THIS shard; DynamoDB rejects "
			+ "AFTER_SEQUENCE_NUMBER without one, which is why the subscription could not start.");
	}

	/// <summary>
	/// SAFETY -- each shard resumes at its OWN position. A single scalar token would give both shards the
	/// same sequence number, which is meaningless in the shard that did not produce it.
	/// </summary>
	[Fact]
	public async Task ResumeEachShardAtItsOwnPosition()
	{
		var source = new Harness();
		source.AddShard(ShardA);
		source.AddShard(ShardB);
		source.Enqueue(ShardA, "a-1");
		source.Enqueue(ShardB, "b-1");

		var token = await CaptureTokenAsync(source, expectedRecords: 2);

		var resumed = new Harness
		{
			StartPosition = ChangeFeedStartPosition.FromContinuationToken,
			ContinuationToken = token,
		};
		resumed.AddShard(ShardA);
		resumed.AddShard(ShardB);

		await DriveOnceAsync(resumed);

		resumed.IteratorRequests.Find(r => r.ShardId == ShardA)?.SequenceNumber.ShouldBe("a-1");
		resumed.IteratorRequests.Find(r => r.ShardId == ShardB)?.SequenceNumber.ShouldBe("b-1");
	}

	/// <summary>
	/// SAFETY -- a token naming a shard the stream no longer has is REFUSED, and the message names the
	/// shard. Neither silent recovery is acceptable: replaying from the horizon redelivers processed work,
	/// and jumping to the end drops the gap unrecoverably.
	/// </summary>
	[Fact]
	public async Task RefuseATokenNamingAShardTheStreamNoLongerHas()
	{
		var source = new Harness();
		source.AddShard(ShardA);
		source.Enqueue(ShardA, "record-1");
		var token = await CaptureTokenAsync(source, expectedRecords: 1);

		// The shard has expired: the stream now reports a different one entirely.
		var resumed = new Harness
		{
			StartPosition = ChangeFeedStartPosition.FromContinuationToken,
			ContinuationToken = token,
		};
		resumed.AddShard(ShardB);

		var thrown = await Should.ThrowAsync<InvalidOperationException>(() => DriveOnceAsync(resumed));

		thrown.Message.ShouldContain(
			ShardA,
			Case.Sensitive,
			"the operator cannot act on a refusal that does not name the shard");

		resumed.IteratorRequests.ShouldNotContain(
			r => r.ShardIteratorType == ShardIteratorType.TRIM_HORIZON
				|| r.ShardIteratorType == ShardIteratorType.LATEST,
			"refusing means opening NOTHING -- falling back to either end is the data defect this arm exists to prevent");
	}

	/// <summary>
	/// SAFETY -- a token issued by a different provider is refused rather than misread.
	/// </summary>
	[Fact]
	public async Task RefuseATokenIssuedByAnotherProvider()
	{
		var resumed = new Harness
		{
			StartPosition = ChangeFeedStartPosition.FromContinuationToken,
			ContinuationToken = "cosmos:1:eyJhIjoiYiJ9",
		};
		resumed.AddShard(ShardA);

		_ = await Should.ThrowAsync<ArgumentException>(() => DriveOnceAsync(resumed));
	}

	/// <summary>
	/// SAFETY -- a token whose encoding version this build cannot read is refused, so the encoding stays
	/// changeable without an older token being reinterpreted under new rules.
	/// </summary>
	[Fact]
	public async Task RefuseATokenWithAnUnreadableEncodingVersion()
	{
		var resumed = new Harness
		{
			StartPosition = ChangeFeedStartPosition.FromContinuationToken,
			ContinuationToken = "ddbstreams:9999:eyJhIjoiYiJ9",
		};
		resumed.AddShard(ShardA);

		_ = await Should.ThrowAsync<ArgumentException>(() => DriveOnceAsync(resumed));
	}

	/// <summary>
	/// LIVENESS -- a subscription with no token still opens and delivers. Without this arm an
	/// implementation that refused every start would satisfy all the refusal arms above.
	/// </summary>
	[Fact]
	public async Task StillDeliverWhenStartingWithoutAToken()
	{
		var harness = new Harness();
		harness.AddShard(ShardA);
		harness.Enqueue(ShardA, "record-1");

		var delivered = await ReadDocumentIdsAsync(harness, expected: 1);

		delivered.ShouldBe(["record-1"]);
		harness.IteratorRequests[0].ShardIteratorType.ShouldBe(ShardIteratorType.TRIM_HORIZON);
	}

	private static async Task<string?> CaptureTokenAsync(Harness harness, int expectedRecords)
	{
		await using var subscription = harness.BuildSubscription();
		await subscription.StartAsync(CancellationToken.None);

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		var seen = 0;

		await foreach (var _ in subscription.ReadChangesAsync(cts.Token).ConfigureAwait(false))
		{
			if (++seen >= expectedRecords)
			{
				break;
			}
		}

		return subscription.CurrentContinuationToken;
	}

	private static async Task<List<string>> ReadDocumentIdsAsync(Harness harness, int expected)
	{
		await using var subscription = harness.BuildSubscription();
		await subscription.StartAsync(CancellationToken.None);

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		var ids = new List<string>();

		await foreach (var change in subscription.ReadChangesAsync(cts.Token).ConfigureAwait(false))
		{
			ids.Add(change.DocumentId);
			if (ids.Count >= expected)
			{
				break;
			}
		}

		return ids;
	}

	/// <summary>
	/// Starts the subscription and advances it far enough for the initial shard discovery to run, which is
	/// where the iterator requests are built and where a bad token is refused.
	/// </summary>
	private static async Task DriveOnceAsync(Harness harness)
	{
		await using var subscription = harness.BuildSubscription();
		await subscription.StartAsync(CancellationToken.None);

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		await using var enumerator = subscription.ReadChangesAsync(cts.Token).GetAsyncEnumerator(cts.Token);

		try
		{
			_ = await enumerator.MoveNextAsync();
		}
		catch (OperationCanceledException)
		{
			// These arms have nothing to deliver; discovery has already run by the time the wait expires.
		}
	}

	private sealed class Harness
	{
		private readonly object _gate = new();
		private readonly List<string> _shards = [];
		private readonly Dictionary<string, Queue<string>> _pending = new(StringComparer.Ordinal);

		public Harness()
		{
			Streams = A.Fake<IAmazonDynamoDBStreams>();

			A.CallTo(() => Streams.DescribeStreamAsync(A<DescribeStreamRequest>._, A<CancellationToken>._))
				.ReturnsLazily((DescribeStreamRequest _, CancellationToken __) =>
				{
					lock (_gate)
					{
						return new DescribeStreamResponse
						{
							StreamDescription = new StreamDescription
							{
								StreamStatus = StreamStatus.ENABLED,
								Shards = [.. _shards.Select(id => new Shard { ShardId = id })],
								LastEvaluatedShardId = null,
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

						return new GetRecordsResponse { Records = batch, NextShardIterator = request.ShardIterator };
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

		public ChangeFeedStartPosition StartPosition { get; init; } = ChangeFeedStartPosition.Beginning;

		public string? ContinuationToken { get; init; }

		public void AddShard(string shardId)
		{
			lock (_gate)
			{
				_shards.Add(shardId);
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

		public DynamoDbStreamsSubscription<Doc> BuildSubscription()
		{
			var options = A.Fake<IChangeFeedOptions>();
			A.CallTo(() => options.PollingInterval).Returns(TimeSpan.FromMilliseconds(1));
			A.CallTo(() => options.MaxBatchSize).Returns(100);
			A.CallTo(() => options.StartPosition).Returns(StartPosition);
			A.CallTo(() => options.ContinuationToken).Returns(ContinuationToken);

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

	private sealed class Doc
	{
		public string Id { get; set; } = string.Empty;
	}
}
