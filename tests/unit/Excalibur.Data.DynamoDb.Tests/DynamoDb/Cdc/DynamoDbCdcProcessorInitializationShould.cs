// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Amazon.DynamoDBStreams;
using Amazon.DynamoDBStreams.Model;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

using Microsoft.Extensions.Logging.Abstractions;

using StreamsRecord = Amazon.DynamoDBStreams.Model.Record;

namespace Excalibur.Data.Tests.DynamoDb.Cdc;

/// <summary>
/// Regression lock (SAFETY-CRITICAL, silent stall) for <c>DynamoDbCdcProcessor</c> initialization.
/// </summary>
/// <remarks>
/// <para>
/// <b>The invariant:</b> a caller either observes a processor whose stream ARN, saved checkpoint and
/// initial shard discovery have ALL succeeded, or observes the failure. There is no third state.
/// </para>
/// <para>
/// <b>The defect this locks:</b> initialization used the stream ARN as its own "already initialized"
/// guard, and assigned it BEFORE reading the checkpoint and before discovering shards. One throw from
/// either left the ARN set, so every later call returned immediately from initialization having skipped
/// the parts that failed. The processor then held no shard iterators at all, so each poll iterated an
/// empty map and returned zero — indistinguishable from an idle stream, with no exception, no log and no
/// gap counter, while the checkpoint that was never read was never retried either.
/// </para>
/// <para>
/// <b>What is asserted:</b> observable behaviour only — how many times the checkpoint was read, how many
/// times shards were discovered, what iterator position was requested, and whether records reached the
/// handler. No private field is inspected, so the lock survives a rename of the guard.
/// </para>
/// <para>
/// <b>Non-vacuity:</b> the failure arms are RED against the pre-fix implementation (a second poll on the
/// same instance delivered nothing and re-read nothing), and
/// <see cref="InitializeExactlyOnce_WhenInitializationSucceeds"/> is their liveness counterweight — it
/// fails against any implementation that "fixes" retry by re-initializing on every call.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "DynamoDb")]
[Trait(TraitNames.Feature, TestFeatures.CDC)]
public sealed class DynamoDbCdcProcessorInitializationShould
{
	private const string StreamArn =
		"arn:aws:dynamodb:us-east-1:000000000000:table/Excalibur/stream/2026-01-01T00:00:00.000";

	private const string TableName = "Excalibur";
	private const string ShardId = "shardId-00000001700000000000-a1b2c3d4";
	private const string SavedSequence = "000000000000000000100";
	private const string RecordSequence = "000000000000000000101";

	/// <summary>
	/// THE ARM THAT MATTERS. A checkpoint read that fails once must leave the processor retryable: the
	/// NEXT call on the SAME instance re-reads the checkpoint, discovers shards, and delivers.
	/// </summary>
	[Fact]
	public async Task RetryTheCheckpointRead_OnTheSameInstance_AfterItFailsOnce()
	{
		var harness = new Harness { FailCheckpointReads = 1 };
		await using var processor = harness.BuildProcessor();

		// Act — the first poll fails while loading the saved position.
		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => processor.ProcessBatchAsync(harness.Handler, CancellationToken.None));

		harness.Delivered.ShouldBeEmpty("nothing can have been delivered by a failed initialization.");

		// Act — the second poll on the SAME instance.
		var processed = await processor.ProcessBatchAsync(harness.Handler, CancellationToken.None);

		// Assert — initialization actually re-ran rather than short-circuiting on half-built state.
		harness.CheckpointReads.ShouldBe(
			2,
			"a failed checkpoint read must be retried; the pre-fix guard was satisfied by the stream ARN "
			+ "that had already been assigned, so the checkpoint was never read again.");

		harness.Discoveries.ShouldBe(
			1,
			"shard discovery never ran during the failed attempt, so the retry must be the first one.");

		harness.IteratorRequests
			.Where(r => string.Equals(r.ShardId, ShardId, StringComparison.Ordinal))
			.ShouldHaveSingleItem(
				"the retry must open the shard it never opened the first time.")
			.ShardIteratorType.ShouldBe(
				ShardIteratorType.AFTER_SEQUENCE_NUMBER,
				"the saved position must be honoured on the retry — if initialization 'succeeded' without "
				+ "ever reading the checkpoint, this shard would open at TRIM_HORIZON and redeliver "
				+ "everything the stream still holds.");

		processed.ShouldBe(1, "the retry must deliver, not report a healthy zero.");
		harness.Delivered.ShouldBe([RecordSequence]);
	}

	/// <summary>
	/// LIVENESS. The counterweight to every retry arm: a successful initialization happens exactly once,
	/// no matter how many times the processor is polled.
	/// </summary>
	/// <remarks>
	/// Without this arm, every failure arm above is satisfied by an implementation that re-reads the
	/// checkpoint and re-discovers shards on every single poll — correct on retry and ruinous in steady
	/// state.
	/// </remarks>
	[Fact]
	public async Task InitializeExactlyOnce_WhenInitializationSucceeds()
	{
		var harness = new Harness();
		await using var processor = harness.BuildProcessor();

		_ = await processor.ProcessBatchAsync(harness.Handler, CancellationToken.None);
		_ = await processor.ProcessBatchAsync(harness.Handler, CancellationToken.None);
		_ = await processor.ProcessBatchAsync(harness.Handler, CancellationToken.None);

		harness.CheckpointReads.ShouldBe(1, "initialization is once-only when it succeeds.");
		harness.Discoveries.ShouldBe(1, "initial discovery is part of that once-only initialization.");
	}

	/// <summary>
	/// SAFETY. A discovery failure — the LAST step of initialization — is just as retryable as a
	/// checkpoint failure. This is the step the pre-fix code was most exposed on: the ARN and the position
	/// were both already published by the time it ran.
	/// </summary>
	[Fact]
	public async Task RetryShardDiscovery_OnTheSameInstance_AfterItFailsOnce()
	{
		var harness = new Harness { FailDiscoveries = 1 };
		await using var processor = harness.BuildProcessor();

		_ = await Should.ThrowAsync<AmazonDynamoDBStreamsException>(
			() => processor.ProcessBatchAsync(harness.Handler, CancellationToken.None));

		var processed = await processor.ProcessBatchAsync(harness.Handler, CancellationToken.None);

		harness.Discoveries.ShouldBe(2, "the failed discovery must be retried.");
		processed.ShouldBe(
			1,
			"a processor whose only discovery attempt failed held no shard iterators; polling it must not "
			+ "return a healthy zero forever.");
		harness.Delivered.ShouldBe([RecordSequence]);
	}

	/// <summary>
	/// SAFETY. Cancellation part-way through initialization leaves nothing published, so a later call with
	/// a live token initializes cleanly rather than inheriting a half-built processor.
	/// </summary>
	[Fact]
	public async Task StayRetryable_WhenInitializationIsCanceledPartWayThrough()
	{
		using var cts = new CancellationTokenSource();
		var harness = new Harness { CancelDuringCheckpointRead = cts };

		await using var processor = harness.BuildProcessor();

		_ = await Should.ThrowAsync<OperationCanceledException>(
			() => processor.ProcessBatchAsync(harness.Handler, cts.Token));

		harness.Discoveries.ShouldBe(0, "cancellation came before discovery could run.");

		// A fresh, uncanceled call on the SAME instance.
		var processed = await processor.ProcessBatchAsync(harness.Handler, CancellationToken.None);

		harness.CheckpointReads.ShouldBe(2, "the canceled checkpoint read must be retried.");
		processed.ShouldBe(1);
		harness.Delivered.ShouldBe([RecordSequence]);
	}

	/// <summary>
	/// SAFETY (concurrency). Two callers that arrive while initialization is in flight both await the SAME
	/// outcome: the work happens once, and neither is handed a partially-built processor.
	/// </summary>
	/// <remarks>
	/// The pre-fix guard was a field assigned at the very start of initialization, so a second caller
	/// arriving mid-flight saw it set, skipped the lock entirely, and polled a processor whose shard
	/// iterators did not exist yet.
	/// </remarks>
	[Fact]
	public async Task AdmitOnlyOneInitialization_WhenTwoCallersArriveConcurrently()
	{
		var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var firstReaderArrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		var harness = new Harness { PauseFirstCheckpointRead = (firstReaderArrived, released.Task) };
		await using var processor = harness.BuildProcessor();

		var first = processor.ProcessBatchAsync(harness.Handler, CancellationToken.None);

		// The first caller is parked inside initialization, holding the lock.
		await firstReaderArrived.Task.WaitAsync(TimeSpan.FromSeconds(30));

		var second = processor.ProcessBatchAsync(harness.Handler, CancellationToken.None);

		released.SetResult();

		var processedFirst = await first.WaitAsync(TimeSpan.FromSeconds(30));
		var processedSecond = await second.WaitAsync(TimeSpan.FromSeconds(30));

		harness.CheckpointReads.ShouldBe(
			1,
			"the second caller must await the first caller's initialization, not start its own.");
		harness.Discoveries.ShouldBe(1);

		(processedFirst + processedSecond).ShouldBe(
			1,
			"the single seeded record is delivered exactly once across the two polls — a caller that "
			+ "raced past initialization would have polled an empty iterator map and reported zero.");
	}

	/// <summary>
	/// SAFETY. The same guarantee on the streaming entry point, where a half-built processor would
	/// otherwise dereference the position it never loaded.
	/// </summary>
	[Fact]
	public async Task RetryInitialization_OnStartAsync_AfterTheCheckpointReadFailsOnce()
	{
		var harness = new Harness { FailCheckpointReads = 1 };
		await using var processor = harness.BuildProcessor();

		using var cts = new CancellationTokenSource();

		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => processor.StartAsync(harness.Handler, cts.Token));

		// Restart the same instance; stop it as soon as the seeded record has been delivered.
		var delivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		Task Handler(DynamoDbDataChangeEvent change, CancellationToken token)
		{
			harness.Delivered.Add(change.SequenceNumber);
			delivered.TrySetResult();
			return Task.CompletedTask;
		}

		var run = processor.StartAsync(Handler, cts.Token);

		await delivered.Task.WaitAsync(TimeSpan.FromSeconds(30));
		await cts.CancelAsync();
		await run.WaitAsync(TimeSpan.FromSeconds(30));

		harness.CheckpointReads.ShouldBe(2, "the failed checkpoint read must be retried by the restart.");
		harness.Delivered.ShouldBe(
			[RecordSequence],
			"the restart must deliver; the pre-fix path either polled an empty iterator map forever or "
			+ "dereferenced the position that was never loaded.");
	}

	/// <summary>
	/// SAFETY. The same atomicity when the stream ARN is DISCOVERED from the table rather than configured:
	/// a checkpoint failure after a successful table describe must not leave the discovered ARN behind as
	/// an initialization guard.
	/// </summary>
	[Fact]
	public async Task RetryInitialization_WhenTheStreamArnIsDiscoveredFromTheTable()
	{
		var harness = new Harness { ConfigureStreamArn = false, FailCheckpointReads = 1 };
		await using var processor = harness.BuildProcessor();

		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => processor.ProcessBatchAsync(harness.Handler, CancellationToken.None));

		var processed = await processor.ProcessBatchAsync(harness.Handler, CancellationToken.None);

		harness.CheckpointReads.ShouldBe(2);
		harness.TableDescribes.ShouldBe(2, "the discovered ARN is part of the attempt that failed.");
		processed.ShouldBe(1);
	}

	// ─── Harness ────────────────────────────────────────────────────────────

	private sealed class Harness
	{
		private readonly HashSet<string> _drainedIterators = new(StringComparer.Ordinal);

		public Harness()
		{
			Streams = A.Fake<IAmazonDynamoDBStreams>();

			A.CallTo(() => Streams.DescribeStreamAsync(A<DescribeStreamRequest>._, A<CancellationToken>._))
				.ReturnsLazily((DescribeStreamRequest _, CancellationToken __) =>
				{
					Discoveries++;
					return Discoveries <= FailDiscoveries
						? throw new AmazonDynamoDBStreamsException("injected describe-stream failure")
						: new DescribeStreamResponse
						{
							StreamDescription = new StreamDescription
							{
								StreamStatus = StreamStatus.ENABLED,
								Shards =
								[
									new Shard
									{
										ShardId = ShardId,
										SequenceNumberRange = new SequenceNumberRange
										{
											StartingSequenceNumber = SavedSequence,
										},
									},
								],
							},
						};
				});

			A.CallTo(() => Streams.GetShardIteratorAsync(A<GetShardIteratorRequest>._, A<CancellationToken>._))
				.ReturnsLazily((GetShardIteratorRequest request, CancellationToken _) =>
				{
					IteratorRequests.Add(request);
					return new GetShardIteratorResponse { ShardIterator = "iterator-" + request.ShardId };
				});

			A.CallTo(() => Streams.GetRecordsAsync(A<GetRecordsRequest>._, A<CancellationToken>._))
				.ReturnsLazily((GetRecordsRequest request, CancellationToken _) =>
				{
					List<StreamsRecord> batch = _drainedIterators.Add(request.ShardIterator)
						? [MakeRecord(RecordSequence)]
						: [];

					return new GetRecordsResponse { Records = batch, NextShardIterator = request.ShardIterator };
				});

			Dynamo = A.Fake<IAmazonDynamoDB>();
			A.CallTo(() => Dynamo.DescribeTableAsync(TableName, A<CancellationToken>._))
				.ReturnsLazily((string _, CancellationToken __) =>
				{
					TableDescribes++;
					return new DescribeTableResponse
					{
						Table = new TableDescription { LatestStreamArn = StreamArn },
					};
				});

			StateStore = A.Fake<IDynamoDbCdcStateStore>();
			A.CallTo(() => StateStore.GetPositionAsync(A<string>._, A<CancellationToken>._))
				.ReturnsLazily(async (string _, CancellationToken token) =>
				{
					CheckpointReads++;

					if (PauseFirstCheckpointRead is { } pause && CheckpointReads == 1)
					{
						pause.Arrived.TrySetResult();
						await pause.Release.ConfigureAwait(false);
					}

					if (CancelDuringCheckpointRead is { } cts && CheckpointReads == 1)
					{
						await cts.CancelAsync().ConfigureAwait(false);
						token.ThrowIfCancellationRequested();
					}

					return CheckpointReads <= FailCheckpointReads
						? throw new InvalidOperationException("injected checkpoint-read failure")
						: DynamoDbCdcPosition.FromShardPositions(
							StreamArn,
							new Dictionary<string, string>(StringComparer.Ordinal) { [ShardId] = SavedSequence });
				});

			A.CallTo(() => StateStore.SavePositionAsync(
					A<string>._, A<DynamoDbCdcPosition>._, A<CancellationToken>._))
				.Returns(Task.CompletedTask);
		}

		public IAmazonDynamoDBStreams Streams { get; }

		public IAmazonDynamoDB Dynamo { get; }

		public IDynamoDbCdcStateStore StateStore { get; }

		public List<GetShardIteratorRequest> IteratorRequests { get; } = [];

		public List<string> Delivered { get; } = [];

		public int CheckpointReads { get; private set; }

		public int Discoveries { get; private set; }

		public int TableDescribes { get; private set; }

		public int FailCheckpointReads { get; init; }

		public int FailDiscoveries { get; init; }

		public bool ConfigureStreamArn { get; init; } = true;

		public CancellationTokenSource? CancelDuringCheckpointRead { get; init; }

		public (TaskCompletionSource Arrived, Task Release)? PauseFirstCheckpointRead { get; init; }

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
				StreamArn = ConfigureStreamArn ? StreamArn : null,
				ProcessorName = "initialization-lock",
				AutoDiscoverShards = false,
				MaxBatchSize = 100,
				PollInterval = TimeSpan.FromMilliseconds(5),
			}),
			NullLogger<DynamoDbCdcProcessor>.Instance);

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
