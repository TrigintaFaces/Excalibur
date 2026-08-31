// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Amazon.DynamoDBStreams;
using Amazon.DynamoDBStreams.Model;
using Amazon.DynamoDBv2;

using Microsoft.Extensions.Logging.Abstractions;

using StreamsRecord = Amazon.DynamoDBStreams.Model.Record;

namespace Excalibur.Data.Tests.DynamoDb.Cdc;

/// <summary>
/// Regression lock (SAFETY-CRITICAL, data loss) for the shard-iterator opening position chosen by
/// <c>DynamoDbCdcProcessor.InitializeShardIteratorAsync</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>The invariant:</b> for every shard, the iterator opened is at or before the first record on that
/// shard this consumer has not handled. A shard we hold a position for resumes <c>AFTER_SEQUENCE_NUMBER</c>
/// it; a shard we have never seen starts at <c>TRIM_HORIZON</c> — the beginning of what the stream still
/// holds — unless this is a genuinely fresh start-from-now deployment, which opens at <c>LATEST</c>.
/// </para>
/// <para>
/// <b>The defect this locks:</b> an unknown shard on a <i>resumed</i> consumer was opened at
/// <c>LATEST</c>. An unknown shard on a running consumer is overwhelmingly a shard SPLIT child, and the
/// child carries every write since the split — so opening it at <c>LATEST</c> silently drops everything
/// written between the split and that moment. No exception, no gap counter, no log line; the processor
/// appears to make healthy progress past records it never saw. Redelivery is the only acceptable cost
/// here, and at-least-once was always going to charge it.
/// </para>
/// <para>
/// <b>What is asserted:</b> the <see cref="GetShardIteratorRequest"/> the processor actually hands to the
/// streams client — the iterator type it <i>asks AWS for</i> — captured off the fake, never an internal
/// field. All arms drive the real public entry point (<c>ProcessBatchAsync</c> → <c>InitializeAsync</c> →
/// <c>DiscoverShardsAsync</c> → <c>InitializeShardIteratorAsync</c>).
/// </para>
/// <para>
/// <b>Non-vacuity (proven by cp-backup mutate-restore of the committed impl, never a <c>git checkout</c>):</b>
/// forcing the branch to unconditional <c>LATEST</c> turns
/// <see cref="OpenAnUnknownShardAtTrimHorizon_WhenTheConsumerIsResuming"/> RED; forcing it to unconditional
/// <c>TRIM_HORIZON</c> turns <see cref="OpenAtLatest_OnAFreshStartFromNowDeployment"/> RED. The two arms
/// are each other's liveness check: neither a constant-<c>LATEST</c> nor a constant-<c>TRIM_HORIZON</c>
/// implementation can satisfy both.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "DynamoDb")]
[Trait(TraitNames.Feature, TestFeatures.CDC)]
public sealed class DynamoDbCdcProcessorShardIteratorShould
{
	private const string StreamArn =
		"arn:aws:dynamodb:us-east-1:000000000000:table/Excalibur/stream/2026-01-01T00:00:00.000";

	/// <summary>The shard the consumer already holds a checkpoint for (the split PARENT in the split arms).</summary>
	private const string KnownShardId = "shardId-00000001700000000000-a1b2c3d4";

	/// <summary>A shard the consumer has never seen — a split CHILD on a resumed consumer.</summary>
	private const string UnknownShardId = "shardId-00000001700000000001-e5f6a7b8";

	private const string SavedSequence = "000000000000000000100";

	private static readonly MethodInfo DiscoverShardsMethod =
		typeof(DynamoDbCdcProcessor)
			.GetMethod("DiscoverShardsAsync", BindingFlags.NonPublic | BindingFlags.Instance)
		?? throw new InvalidOperationException(
			"shard-iterator lock: 'DiscoverShardsAsync' private method not found — if it was renamed or "
			+ "inlined, update this lock to bind the new shard-discovery entry point.");

	/// <summary>
	/// Case 1 — a shard the consumer holds a checkpoint for resumes AFTER that sequence number. Unchanged
	/// behaviour, asserted so a future edit cannot collapse the branches into one.
	/// </summary>
	[Fact]
	public async Task ResumeAfterTheSavedSequenceNumber_WhenTheShardHasASavedPosition()
	{
		// Arrange — a resumed consumer: the state store hands back a position for the one open shard.
		var fixture = new StreamsFixture(
			shards: [OpenShard(KnownShardId)],
			savedPosition: DynamoDbCdcPosition.FromShardPositions(
				StreamArn,
				new Dictionary<string, string>(StringComparer.Ordinal) { [KnownShardId] = SavedSequence }),
			startPosition: null);

		await using var processor = fixture.BuildProcessor();

		// Act — the public entry point runs initialization + shard discovery.
		_ = await processor.ProcessBatchAsync(NoOpHandler, CancellationToken.None);

		// Assert — on the request actually sent to AWS, not on an internal field.
		var request = fixture.SingleIteratorRequestFor(KnownShardId);

		request.ShardIteratorType.ShouldBe(
			ShardIteratorType.AFTER_SEQUENCE_NUMBER,
			"a shard the consumer holds a checkpoint for must resume AFTER that sequence number — re-reading "
			+ "from TRIM_HORIZON would re-deliver the whole shard, and LATEST would skip everything since the "
			+ "checkpoint.");

		request.SequenceNumber.ShouldBe(
			SavedSequence,
			"the resume point must be the saved sequence number for THAT shard.");
	}

	/// <summary>
	/// Case 2 — THE ARM THAT MATTERS. A resumed consumer that discovers a shard it has no position for
	/// (a split child) must open it at TRIM_HORIZON, never LATEST.
	/// </summary>
	[Fact]
	public async Task OpenAnUnknownShardAtTrimHorizon_WhenTheConsumerIsResuming()
	{
		// Arrange — a shard split as the processor sees it on resume: the parent is CLOSED (it has an
		// ending sequence number) and checkpointed, and a child has appeared that the consumer has never
		// seen. The saved position is what makes this a RESUMED consumer rather than a fresh start.
		var fixture = new StreamsFixture(
			shards:
			[
				ClosedShard(KnownShardId),
				OpenShard(UnknownShardId, parentShardId: KnownShardId),
			],
			savedPosition: DynamoDbCdcPosition.FromShardPositions(
				StreamArn,
				new Dictionary<string, string>(StringComparer.Ordinal) { [KnownShardId] = SavedSequence }),
			startPosition: null);

		await using var processor = fixture.BuildProcessor();

		// Act
		_ = await processor.ProcessBatchAsync(NoOpHandler, CancellationToken.None);

		// Assert — the child is opened at the start of retained history, so nothing written between the
		// split and this moment is skipped.
		var request = fixture.SingleIteratorRequestFor(UnknownShardId);

		request.ShardIteratorType.ShouldBe(
			ShardIteratorType.TRIM_HORIZON,
			"an unknown shard on a RESUMED consumer is a split child carrying every write since the split; "
			+ "opening it at LATEST silently drops those records with no exception, no gap counter and no "
			+ "log line. TRIM_HORIZON re-delivers instead — the cost at-least-once was always going to "
			+ "charge, and handlers must be idempotent regardless.");

		request.SequenceNumber.ShouldBeNullOrEmpty(
			"a TRIM_HORIZON iterator carries no sequence number.");
	}

	/// <summary>
	/// Case 3 — the liveness arm. A genuinely fresh start-from-now deployment (explicit
	/// <c>Now()</c> start position AND no saved position) still opens at LATEST. Without this, case 2 is
	/// satisfied by an implementation that always answers TRIM_HORIZON and start-from-now is quietly broken.
	/// </summary>
	[Fact]
	public async Task OpenAtLatest_OnAFreshStartFromNowDeployment()
	{
		// Arrange — fresh start: explicit start-from-now, empty state store, no shard positions.
		var fixture = new StreamsFixture(
			shards: [OpenShard(KnownShardId)],
			savedPosition: null,
			startPosition: DynamoDbCdcPosition.Now(StreamArn));

		await using var processor = fixture.BuildProcessor();

		// Act
		_ = await processor.ProcessBatchAsync(NoOpHandler, CancellationToken.None);

		// Assert
		var request = fixture.SingleIteratorRequestFor(KnownShardId);

		request.ShardIteratorType.ShouldBe(
			ShardIteratorType.LATEST,
			"start-from-now is a deliberate INITIAL choice (explicit Now() start position + no saved "
			+ "position) and must still be honoured; if this arm fails, the safe-by-default TRIM_HORIZON "
			+ "branch has swallowed the fresh-start case and start-from-now no longer works.");
	}

	/// <summary>
	/// Case 4 — the fresh-start licence expires on first progress. Once a record has been handled, this
	/// consumer has history to protect, so a shard discovered afterwards is read from TRIM_HORIZON even in
	/// a start-from-now deployment.
	/// </summary>
	[Fact]
	public async Task OpenALaterDiscoveredShardAtTrimHorizon_OnceTheStartFromNowConsumerHasMadeProgress()
	{
		// Arrange — a fresh start-from-now consumer with one shard that yields exactly one record.
		var fixture = new StreamsFixture(
			shards: [OpenShard(KnownShardId)],
			savedPosition: null,
			startPosition: DynamoDbCdcPosition.Now(StreamArn));

		fixture.YieldOneRecordFor(KnownShardId, SavedSequence);

		await using var processor = fixture.BuildProcessor();

		var handled = new List<string>();
		Task Handler(DynamoDbDataChangeEvent change, CancellationToken _)
		{
			handled.Add(change.SequenceNumber);
			return Task.CompletedTask;
		}

		// Act — first poll: the initial shard opens at LATEST (fresh start) and one record is handled.
		_ = await processor.ProcessBatchAsync(Handler, CancellationToken.None);

		handled.ShouldBe(
			[SavedSequence],
			"sanity: the consumer must actually have made progress, otherwise this arm proves nothing.");

		fixture.SingleIteratorRequestFor(KnownShardId).ShardIteratorType.ShouldBe(
			ShardIteratorType.LATEST,
			"sanity: the FIRST shard of a fresh start-from-now consumer opens at LATEST.");

		// A split happens after that progress and a child shard appears.
		fixture.AddShard(OpenShard(UnknownShardId, parentShardId: KnownShardId));
		await InvokeDiscoverShardsAsync(processor);

		// Assert — the fresh start is over: the newly discovered shard is read from the beginning of
		// retained history, not skipped to LATEST.
		fixture.SingleIteratorRequestFor(UnknownShardId).ShardIteratorType.ShouldBe(
			ShardIteratorType.TRIM_HORIZON,
			"start-from-now is honoured only until the first handled record; after that the consumer has "
			+ "history to protect and an unfamiliar shard must be read from TRIM_HORIZON, or a split "
			+ "immediately after startup silently loses the child's records.");
	}

	// ─── Fixture helpers ────────────────────────────────────────────────────

	private static Task NoOpHandler(DynamoDbDataChangeEvent change, CancellationToken cancellationToken)
		=> Task.CompletedTask;

	private static Shard OpenShard(string shardId, string? parentShardId = null) => new()
	{
		ShardId = shardId,
		ParentShardId = parentShardId,
		SequenceNumberRange = new SequenceNumberRange { StartingSequenceNumber = SavedSequence },
	};

	/// <summary>A shard DynamoDB has closed (it has an ending sequence number) — the parent of a split.</summary>
	private static Shard ClosedShard(string shardId) => new()
	{
		ShardId = shardId,
		SequenceNumberRange = new SequenceNumberRange
		{
			StartingSequenceNumber = SavedSequence,
			EndingSequenceNumber = SavedSequence,
		},
	};

	private static async Task InvokeDiscoverShardsAsync(DynamoDbCdcProcessor processor)
	{
		var result = DiscoverShardsMethod.Invoke(processor, [CancellationToken.None]);
		result.ShouldNotBeNull("DiscoverShardsAsync must return a non-null Task.");
		await (Task)result;
	}

	/// <summary>
	/// An <see cref="IAmazonDynamoDBStreams"/> fake that records every <see cref="GetShardIteratorRequest"/>
	/// the processor sends, so the arms can assert the iterator type actually requested.
	/// </summary>
	private sealed class StreamsFixture
	{
		private readonly List<Shard> _shards;
		private readonly Dictionary<string, StreamsRecord> _pendingRecords = new(StringComparer.Ordinal);
		private readonly HashSet<string> _drainedIterators = new(StringComparer.Ordinal);

		public StreamsFixture(
			IEnumerable<Shard> shards,
			DynamoDbCdcPosition? savedPosition,
			DynamoDbCdcPosition? startPosition)
		{
			_shards = [.. shards];

			Streams = A.Fake<IAmazonDynamoDBStreams>();

			A.CallTo(() => Streams.DescribeStreamAsync(A<DescribeStreamRequest>._, A<CancellationToken>._))
				.ReturnsLazily(() => new DescribeStreamResponse
				{
					StreamDescription = new StreamDescription { Shards = [.. _shards] },
				});

			// Capture the request, and hand back an iterator token that encodes the shard it belongs to so
			// GetRecords can answer per-shard.
			A.CallTo(() => Streams.GetShardIteratorAsync(A<GetShardIteratorRequest>._, A<CancellationToken>._))
				.ReturnsLazily((GetShardIteratorRequest request, CancellationToken _) =>
				{
					IteratorRequests.Add(request);
					return new GetShardIteratorResponse { ShardIterator = IteratorFor(request.ShardId) };
				});

			// Each shard yields its one seeded record exactly once (if seeded), then stays empty. The
			// iterator token is never retired (non-null NextShardIterator), so the shard set is stable.
			A.CallTo(() => Streams.GetRecordsAsync(A<GetRecordsRequest>._, A<CancellationToken>._))
				.ReturnsLazily((GetRecordsRequest request, CancellationToken _) =>
				{
					var iterator = request.ShardIterator;
					var shardId = ShardIdFromIterator(iterator);

					List<StreamsRecord> batch = [];
					if (_drainedIterators.Add(iterator) &&
						_pendingRecords.TryGetValue(shardId, out var record))
					{
						batch = [record];
					}

					return new GetRecordsResponse { Records = batch, NextShardIterator = iterator };
				});

			StateStore = A.Fake<IDynamoDbCdcStateStore>();
			A.CallTo(() => StateStore.GetPositionAsync(A<string>._, A<CancellationToken>._))
				.Returns(Task.FromResult(savedPosition));
			A.CallTo(() => StateStore.SavePositionAsync(A<string>._, A<DynamoDbCdcPosition>._, A<CancellationToken>._))
				.Returns(Task.CompletedTask);

			Options = Microsoft.Extensions.Options.Options.Create(new DynamoDbCdcOptions
			{
				StreamArn = StreamArn,
				ProcessorName = "shard-iterator-lock",
				AutoDiscoverShards = false,
				MaxBatchSize = 100,
				StartPosition = startPosition,
			});
		}

		public IAmazonDynamoDBStreams Streams { get; }

		public IDynamoDbCdcStateStore StateStore { get; }

		public IOptions<DynamoDbCdcOptions> Options { get; }

		public List<GetShardIteratorRequest> IteratorRequests { get; } = [];

		public DynamoDbCdcProcessor BuildProcessor() => new(
			A.Fake<IAmazonDynamoDB>(),
			Streams,
			StateStore,
			Options,
			NullLogger<DynamoDbCdcProcessor>.Instance);

		public void AddShard(Shard shard) => _shards.Add(shard);

		public void YieldOneRecordFor(string shardId, string sequenceNumber)
			=> _pendingRecords[shardId] = MakeRecord(sequenceNumber);

		/// <summary>
		/// The one iterator request the processor sent for <paramref name="shardId"/>. Asserting exactly one
		/// keeps the arms honest: a second request for the same shard would mean the opening position was
		/// re-chosen and the arm could be reading the wrong one.
		/// </summary>
		public GetShardIteratorRequest SingleIteratorRequestFor(string shardId)
		{
			var matches = IteratorRequests
				.Where(r => string.Equals(r.ShardId, shardId, StringComparison.Ordinal))
				.ToList();

			matches.Count.ShouldBe(
				1,
				$"expected exactly one GetShardIterator request for shard '{shardId}', but the processor sent "
				+ $"{matches.Count} (all requests: [{string.Join(", ", IteratorRequests.Select(r => $"{r.ShardId}:{r.ShardIteratorType}"))}]).");

			return matches[0];
		}

		private static string IteratorFor(string shardId) => $"ITER::{shardId}";

		private static string ShardIdFromIterator(string iterator) => iterator["ITER::".Length..];

		private static StreamsRecord MakeRecord(string sequenceNumber) => new()
		{
			EventID = $"event-{sequenceNumber}",
			EventName = OperationType.INSERT,
			Dynamodb = new StreamRecord
			{
				SequenceNumber = sequenceNumber,
				ApproximateCreationDateTime = DateTime.UtcNow,
				Keys = new Dictionary<string, AttributeValue>(StringComparer.Ordinal)
				{
					["id"] = new AttributeValue { S = sequenceNumber },
				},
				NewImage = new Dictionary<string, AttributeValue>(StringComparer.Ordinal)
				{
					["id"] = new AttributeValue { S = sequenceNumber },
				},
			},
		};
	}
}
