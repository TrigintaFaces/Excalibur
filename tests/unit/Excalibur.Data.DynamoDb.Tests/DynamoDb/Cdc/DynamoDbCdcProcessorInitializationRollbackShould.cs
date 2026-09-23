// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Amazon.DynamoDBStreams;
using Amazon.DynamoDBStreams.Model;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

using Excalibur.Cdc.DynamoDb;

using Microsoft.Extensions.Logging.Abstractions;

using StreamsRecord = Amazon.DynamoDBStreams.Model.Record;

namespace Excalibur.Data.Tests.DynamoDb.Cdc;

/// <summary>
/// Holds the CDC processor to discarding the shard iterators a FAILED initialization opened, so the
/// retry re-opens every shard at the position the retry itself computes.
/// </summary>
/// <remarks>
/// <para>
/// <b>What this covers that the sibling initialization arms do not.</b> Those arms prove the retry
/// happens at all — that readiness is published last and a failed attempt leaves the processor
/// retryable. They are satisfied whether or not the catch block rolls partial state back, because in
/// their scenarios the retry recomputes every field with an identical value, so a leftover is
/// indistinguishable from a clean slate.
/// </para>
/// <para>
/// <b>The leftover only bites when the retry would compute something DIFFERENT — and then it loses
/// records.</b> <c>DiscoverShardsAsync</c> skips any shard already present in the iterator map
/// (<c>if (!_shardIterators.ContainsKey(...))</c>). An iterator opened by an attempt that went on to
/// fail therefore survives into the retry and is never replaced, even though the retry's inputs say it
/// should have been opened somewhere else entirely.
/// </para>
/// <para>
/// <b>The scenario below is the documented start-from-now rule, crossed with a retry.</b> Start-from-now
/// is honoured only on a genuinely fresh start — <c>StartPosition</c> with a timestamp AND no saved
/// position — and a shard opened that way starts at <c>LATEST</c>. The first attempt here is such a
/// start: it opens the first shard at <c>LATEST</c>, then fails while opening the second. By the time
/// the retry runs a checkpoint exists (another instance wrote one, which is the ordinary multi-instance
/// case), so start-from-now no longer applies and every shard must open at <c>TRIM_HORIZON</c>. A
/// surviving <c>LATEST</c> iterator means that shard silently skips its entire history — no exception,
/// no log line, and a processor that reports healthy progress past records it never read.
/// </para>
/// <para>
/// <b>Non-vacuity.</b> <see cref="ReopenEveryShardAfterADiscoveryThatFailedPartWayThrough"/> is RED with
/// the rollback removed from the catch and GREEN with it present.
/// <see cref="NotReopenShardsOnPollsAfterInitializationSucceeds"/> is its counterweight: it fails against
/// the lazy implementation that clears the iterator map on every poll, which would satisfy the first arm
/// while re-opening every shard forever.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.CDC)]
public sealed class DynamoDbCdcProcessorInitializationRollbackShould
{
	private const string StreamArn =
		"arn:aws:dynamodb:us-east-1:000000000000:table/Excalibur/stream/2026-01-01T00:00:00.000";

	private const string TableName = "Excalibur";
	private const string FirstShardId = "shardId-00000001700000000000-aaaaaaaa";
	private const string SecondShardId = "shardId-00000001700000000001-bbbbbbbb";
	private const string CheckpointSequence = "000000000000000000100";

	// Only a TRIM_HORIZON iterator can reach these. A shard left on a LATEST iterator never sees them,
	// which is exactly the loss this arm is about.
	private const string FirstShardHistory = "000000000000000000011";
	private const string SecondShardHistory = "000000000000000000012";

	[Fact]
	public async Task ReopenEveryShardAfterADiscoveryThatFailedPartWayThrough()
	{
		// SAFETY, and a data-loss path rather than untidy state. The first attempt opens the first shard
		// at LATEST and then fails opening the second; the retry runs under a checkpoint, so LATEST is no
		// longer the right answer for any shard.
		var harness = new Harness { FailIteratorForShard = SecondShardId, CheckpointAppearsOnRead = 2 };
		await using var processor = harness.BuildProcessor();

		_ = await Should.ThrowAsync<AmazonDynamoDBStreamsException>(
			() => processor.ProcessBatchAsync(harness.Handler, CancellationToken.None));

		harness.IteratorTypeFor(FirstShardId).ShouldBe(
			ShardIteratorType.LATEST,
			"sanity: the failed attempt was a genuine start-from-now, so it opened the first shard at "
			+ "LATEST. If this is not LATEST the scenario never set up the divergence it exists to test.");

		// The retry, on the SAME instance, now sees a checkpoint — so start-from-now no longer applies.
		var processed = await processor.ProcessBatchAsync(harness.Handler, CancellationToken.None);

		harness.IteratorRequestsFor(FirstShardId).ShouldBe(
			2,
			"the first shard's iterator was opened by an attempt that failed, so it must be discarded and "
			+ "re-opened. Discovery skips any shard already in the iterator map, so a leftover entry is "
			+ "never replaced — the shard stays wherever the abandoned attempt happened to put it.");

		harness.IteratorTypeFor(FirstShardId).ShouldBe(
			ShardIteratorType.TRIM_HORIZON,
			"start-from-now is honoured only on a fresh start, and the retry is not one: a checkpoint "
			+ "exists. A shard still reading from LATEST skips every record written before this moment.");

		processed.ShouldBe(2, "both shards must deliver on the retry.");

		harness.Delivered.ShouldBe(
			[FirstShardHistory, SecondShardHistory],
			ignoreOrder: true,
			customMessage:
			"the first shard's history was never delivered. Its iterator survived a failed attempt at "
			+ "LATEST, so the records written before the processor started are skipped silently — the "
			+ "processor reports healthy progress and the consumer never learns those changes happened.");
	}

	[Fact]
	public async Task NotReopenShardsOnPollsAfterInitializationSucceeds()
	{
		// LIVENESS, and the counterweight to the arm above. "Discard the iterators" is satisfied just as
		// well by discarding them on EVERY poll, which re-opens every shard at TRIM_HORIZON forever and
		// redelivers the whole stream on each poll. The rollback belongs on the failure path only.
		//
		// A checkpoint exists from the first read and nothing fails, so this is the plain steady state:
		// both shards open once, at TRIM_HORIZON, and each record is handed over exactly once.
		var harness = new Harness { CheckpointAppearsOnRead = 1 };
		await using var processor = harness.BuildProcessor();

		_ = await processor.ProcessBatchAsync(harness.Handler, CancellationToken.None);
		_ = await processor.ProcessBatchAsync(harness.Handler, CancellationToken.None);
		_ = await processor.ProcessBatchAsync(harness.Handler, CancellationToken.None);

		harness.IteratorRequestsFor(FirstShardId).ShouldBe(1, "a successful shard is opened once.");
		harness.IteratorRequestsFor(SecondShardId).ShouldBe(1, "a successful shard is opened once.");

		harness.Delivered.ShouldBe(
			[FirstShardHistory, SecondShardHistory],
			ignoreOrder: true,
			customMessage: "each record is delivered once across the three polls, not once per poll.");
	}

	[Fact]
	public async Task OpenAShardAtTrimHorizonWhenACheckpointExistsFromTheStart()
	{
		// CONTROL for the harness itself. If the fake could not tell LATEST from TRIM_HORIZON, the arm
		// above would be measuring nothing at all — so prove the distinction is real and that the
		// no-failure, checkpoint-present path opens at TRIM_HORIZON and delivers.
		var harness = new Harness { CheckpointAppearsOnRead = 1 };
		await using var processor = harness.BuildProcessor();

		var processed = await processor.ProcessBatchAsync(harness.Handler, CancellationToken.None);

		harness.IteratorTypeFor(FirstShardId).ShouldBe(ShardIteratorType.TRIM_HORIZON);
		processed.ShouldBe(2);
		harness.Delivered.ShouldBe([FirstShardHistory, SecondShardHistory], ignoreOrder: true);
	}

	/// <summary>
	/// A two-shard stream whose fake iterators carry the type they were opened with, so a shard left on a
	/// <c>LATEST</c> iterator behaves the way a real one does: it returns nothing, forever, without error.
	/// </summary>
	private sealed class Harness
	{
		private static readonly Dictionary<string, string> HistoryByShard =
			new(StringComparer.Ordinal)
			{
				[FirstShardId] = FirstShardHistory,
				[SecondShardId] = SecondShardHistory,
			};

		private readonly List<GetShardIteratorRequest> _iteratorRequests = [];
		private readonly HashSet<string> _drainedIterators = new(StringComparer.Ordinal);
		private bool _iteratorFailureSpent;

		public Harness()
		{
			Streams = A.Fake<IAmazonDynamoDBStreams>();

			_ = A.CallTo(() => Streams.DescribeStreamAsync(A<DescribeStreamRequest>._, A<CancellationToken>._))
				.ReturnsLazily((DescribeStreamRequest _, CancellationToken __) =>
				{
					Discoveries++;
					return new DescribeStreamResponse
					{
						StreamDescription = new StreamDescription
						{
							StreamStatus = StreamStatus.ENABLED,
							Shards = [OpenShard(FirstShardId), OpenShard(SecondShardId)],
						},
					};
				});

			_ = A.CallTo(() => Streams.GetShardIteratorAsync(A<GetShardIteratorRequest>._, A<CancellationToken>._))
				.ReturnsLazily((GetShardIteratorRequest request, CancellationToken _) =>
				{
					_iteratorRequests.Add(request);

					// Fails ONCE, and only for the nominated shard: the point is a discovery that opens
					// some iterators and then fails, not one that fails before opening any.
					if (!_iteratorFailureSpent &&
						string.Equals(request.ShardId, FailIteratorForShard, StringComparison.Ordinal))
					{
						_iteratorFailureSpent = true;
						throw new AmazonDynamoDBStreamsException("injected get-shard-iterator failure");
					}

					// The token carries the type, so GetRecords can behave as the service does.
					return new GetShardIteratorResponse
					{
						ShardIterator = $"{request.ShardIteratorType.Value}|{request.ShardId}",
					};
				});

			_ = A.CallTo(() => Streams.GetRecordsAsync(A<GetRecordsRequest>._, A<CancellationToken>._))
				.ReturnsLazily((GetRecordsRequest request, CancellationToken _) =>
				{
					var parts = request.ShardIterator.Split('|');
					var iteratorType = parts[0];
					var shardId = parts[1];

					// A LATEST iterator sees only what is written after it was opened. Nothing is, so it
					// returns an empty batch every time — no error, no end-of-shard, just silence.
					List<StreamsRecord> batch =
						string.Equals(iteratorType, ShardIteratorType.TRIM_HORIZON.Value, StringComparison.Ordinal)
						&& _drainedIterators.Add(request.ShardIterator)
							? [MakeRecord(HistoryByShard[shardId])]
							: [];

					return new GetRecordsResponse { Records = batch, NextShardIterator = request.ShardIterator };
				});

			Dynamo = A.Fake<IAmazonDynamoDB>();

			StateStore = A.Fake<IDynamoDbCdcStateStore>();
			_ = A.CallTo(() => StateStore.GetPositionAsync(A<string>._, A<CancellationToken>._))
				.ReturnsLazily((string _, CancellationToken __) =>
				{
					CheckpointReads++;

					// Before this read there is no checkpoint, so start-from-now applies. From it onwards
					// one exists — the state another instance leaves behind between a failed attempt and
					// its retry — and start-from-now must stop applying.
					return CheckpointReads < CheckpointAppearsOnRead
						? (DynamoDbCdcPosition?)null
						: DynamoDbCdcPosition.FromShardPositions(
							StreamArn,
							new Dictionary<string, string>(StringComparer.Ordinal)
							{
								["shardId-some-other-shard"] = CheckpointSequence,
							});
				});

			_ = A.CallTo(() => StateStore.SavePositionAsync(
					A<string>._, A<DynamoDbCdcPosition>._, A<CancellationToken>._))
				.Returns(Task.CompletedTask);
		}

		public IAmazonDynamoDBStreams Streams { get; }

		public IAmazonDynamoDB Dynamo { get; }

		public IDynamoDbCdcStateStore StateStore { get; }

		public List<string> Delivered { get; } = [];

		public int CheckpointReads { get; private set; }

		public int Discoveries { get; private set; }

		/// <summary>
		/// Gets the shard whose first <c>GetShardIterator</c> call fails, or <see langword="null"/> for a
		/// discovery that succeeds outright.
		/// </summary>
		public string? FailIteratorForShard { get; init; }

		/// <summary>
		/// Gets the 1-based checkpoint read from which a saved position exists. The default is beyond any
		/// read this fixture performs, so no checkpoint ever appears.
		/// </summary>
		public int CheckpointAppearsOnRead { get; init; } = int.MaxValue;

		public int IteratorRequestsFor(string shardId) =>
			_iteratorRequests.Count(r => string.Equals(r.ShardId, shardId, StringComparison.Ordinal));

		public ShardIteratorType? IteratorTypeFor(string shardId) =>
			_iteratorRequests.LastOrDefault(r => string.Equals(r.ShardId, shardId, StringComparison.Ordinal))
				?.ShardIteratorType;

		public Task Handler(DynamoDbDataChangeEvent change, CancellationToken cancellationToken)
		{
			Delivered.Add(change.SequenceNumber);
			return Task.CompletedTask;
		}

		public DynamoDbCdcProcessor BuildProcessor() => new(
			Dynamo,
			Streams,
			StateStore,
			Microsoft.Extensions.Options.Options.Create(new DynamoDbCdcOptions
			{
				TableName = TableName,
				StreamArn = StreamArn,
				ProcessorName = "initialization-rollback-lock",
				AutoDiscoverShards = false,
				MaxBatchSize = 100,
				PollInterval = TimeSpan.FromMilliseconds(5),

				// Start-from-now: a timestamp and no shard positions. Honoured only while no checkpoint
				// exists, which is the condition the retry in the first arm changes.
				StartPosition = DynamoDbCdcPosition.Now(StreamArn),
			}),
			NullLogger<DynamoDbCdcProcessor>.Instance);

		private static Shard OpenShard(string shardId) => new()
		{
			ShardId = shardId,
			SequenceNumberRange = new SequenceNumberRange { StartingSequenceNumber = "000000000000000000001" },
		};

		private static StreamsRecord MakeRecord(string sequenceNumber) => new()
		{
			EventID = sequenceNumber,
			EventName = OperationType.INSERT,
			Dynamodb = new StreamRecord
			{
				SequenceNumber = sequenceNumber,
				ApproximateCreationDateTime = DateTime.UtcNow,
				Keys = new Dictionary<string, Amazon.DynamoDBStreams.Model.AttributeValue>(StringComparer.Ordinal)
				{
					["pk"] = new() { S = "order#1" },
				},
			},
		};
	}
}
