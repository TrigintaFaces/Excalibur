// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Reflection;

using Amazon.DynamoDBStreams;
using Amazon.DynamoDBStreams.Model;
using Amazon.DynamoDBv2;

using Microsoft.Extensions.Logging.Abstractions;

using StreamsRecord = Amazon.DynamoDBStreams.Model.Record;

namespace Excalibur.Data.Tests.DynamoDb.Cdc;

/// <summary>
/// Behavioral regression lock for bead <c>q3w5cv</c> (S855 REVIEW_ARCH / pxhqri, FR-B2 / AC-N3.1,
/// SAFETY-CRITICAL): the DynamoDB CDC poll-batch processor must NOT advance its shard iterator past a
/// record whose handler faulted — the failed record is <b>re-delivered</b> on the next poll (at-least-once),
/// never silently skipped.
/// </summary>
/// <remarks>
/// <para>
/// This is the <em>behavioral</em> half of the q3w5cv checkpoint-advance proof (the <em>structural</em>
/// half — the gate literally reads <c>decision.AdvanceCheckpoint</c> in both poll-batch processors — is
/// <c>Excalibur.Cdc.Tests.CdcCheckpointAdvanceGateShould</c>). DynamoDb is the one poll-batch provider whose
/// streams client is unit-fakeable (Cosmos takes a real <c>CosmosClient</c>); the Postgres/Mongo streaming
/// restart-redelivery real-infra proof is carved to <c>e9u90j</c> (next sprint, needs <c>wal_level=logical</c>
/// / Mongo replica-set fixtures) — SA/PM ruling B (msg 17590/17594).
/// </para>
/// <para>
/// <b>Mechanism under test:</b> when the handler throws on the <i>first</i> record of a batch, no prefix
/// position is recorded, so <c>RepointShardIteratorAsync</c> leaves the shard iterator untouched and the
/// structural gate <c>if (decision.AdvanceCheckpoint)</c> (false on any fault) skips
/// <c>AdvanceOrRetireShard</c> — the iterator stays put, so the next poll re-reads the same record.
/// </para>
/// <para>
/// <b>Non-vacuous → RED on the <c>if(true)</c> mutant:</b> mutating the gate at
/// <c>DynamoDbCdcProcessor</c>'s post-batch advance to <c>if (true)</c> runs <c>AdvanceOrRetireShard</c> even
/// on a fault → the iterator advances to <c>NextShardIterator</c> → the faulted record is skipped → the
/// second poll never re-delivers it → this lock's <c>ShouldContain("1")</c> fails. (Proven by a cp-backup
/// mutate-restore of the committed impl — never a <c>git checkout</c> of a shared, uncommitted file,
/// <c>commit-surface-before-parallel-edits</c>.)
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "DynamoDb")]
[Trait(TraitNames.Feature, TestFeatures.CDC)]
public sealed class DynamoDbCdcProcessorRedeliveryShould
{
	private const string StreamArn =
		"arn:aws:dynamodb:us-east-1:000000000000:table/Excalibur/stream/2026-01-01T00:00:00.000";

	private const string ShardId = "shardId-000000000001";

	// Iterator tokens. The fault batch reads StartIterator (record seq "1"); a CORRECT impl leaves the
	// iterator at StartIterator after the fault (redelivery); the if(true) mutant advances it to NextIterator
	// (record seq "2") — skipping seq "1".
	private const string StartIterator = "ITER-START";
	private const string NextIterator = "ITER-NEXT";

	private const string FaultedSequence = "1";
	private const string FollowingSequence = "2";

	private static readonly MethodInfo ProcessBatchInternalMethod =
		typeof(DynamoDbCdcProcessor)
			.GetMethod("ProcessBatchInternalAsync", BindingFlags.NonPublic | BindingFlags.Instance)
		?? throw new InvalidOperationException(
			"q3w5cv redelivery lock: 'ProcessBatchInternalAsync' private method not found — if it was "
			+ "renamed/inlined, update this lock to bind the new poll-batch entry point.");

	private static readonly FieldInfo ShardIteratorsField =
		typeof(DynamoDbCdcProcessor)
			.GetField("_shardIterators", BindingFlags.NonPublic | BindingFlags.Instance)
		?? throw new InvalidOperationException(
			"q3w5cv redelivery lock: '_shardIterators' field not found — the shard-iterator seam moved; "
			+ "update this lock.");

	private static readonly FieldInfo CurrentPositionField =
		typeof(DynamoDbCdcProcessor)
			.GetField("_currentPosition", BindingFlags.NonPublic | BindingFlags.Instance)
		?? throw new InvalidOperationException(
			"q3w5cv redelivery lock: '_currentPosition' field not found — the position seam moved; "
			+ "update this lock.");

	[Fact]
	public async Task RedeliverFaultedRecord_OnNextPoll_WhenHandlerThrows_NotAdvancePastIt()
	{
		// Arrange — an iterator-aware streams fake: StartIterator → [seq 1], NextIterator → [seq 2].
		var (streams, stateStore, options) = BuildFixture();
		await using var processor = BuildProcessor(streams, stateStore, options);

		// Seed the shard iterator + current position directly (deterministic single-shard scenario; bypasses
		// discovery, which would otherwise set both via the streams client).
		SetShardIterator(processor, ShardId, StartIterator);
		CurrentPositionField.SetValue(processor, DynamoDbCdcPosition.Beginning(StreamArn));

		// Batch 1: the handler throws on the first (and only) record of the batch. No prefix is handled,
		// so the iterator must NOT advance past the failed record.
		var handlerEx = new InvalidOperationException("q3w5cv redelivery lock: simulated handler fault on first record");
		Task ThrowingHandler(DynamoDbDataChangeEvent _, CancellationToken __) => throw handlerEx;

		var thrown = await Should.ThrowAsync<InvalidOperationException>(
			() => InvokeProcessBatchInternal(processor, ThrowingHandler, autoConfirm: true));
		thrown.ShouldBeSameAs(handlerEx, "the original handler fault must surface (sanity: the batch did fault).");

		// Batch 2: a collecting handler records every sequence the processor re-delivers on the next poll.
		var redelivered = new List<string>();
		Task CollectingHandler(DynamoDbDataChangeEvent e, CancellationToken __)
		{
			redelivered.Add(e.SequenceNumber);
			return Task.CompletedTask;
		}

		_ = await InvokeProcessBatchInternal(processor, CollectingHandler, autoConfirm: true);

		// Assert — AC-N3.1 at-least-once: the faulted record (seq "1") is re-delivered, not skipped.
		// RED on the if(true) mutant: the gate would advance to NextIterator after the fault, so batch 2 reads
		// seq "2" and the faulted seq "1" is silently lost.
		redelivered.ShouldContain(
			FaultedSequence,
			"q3w5cv AC-N3.1: a record whose handler faulted MUST be re-delivered on the next poll (at-least-once); "
			+ "the checkpoint/iterator must not advance past it. The if(true) mutant advances past the failed batch "
			+ $"→ seq '{FaultedSequence}' is skipped (saw: [{string.Join(", ", redelivered)}]).");
	}

	// ─── Fixture helpers ────────────────────────────────────────────────────

	private static (IAmazonDynamoDBStreams Streams, IDynamoDbCdcStateStore StateStore, IOptions<DynamoDbCdcOptions> Options)
		BuildFixture()
	{
		var streams = A.Fake<IAmazonDynamoDBStreams>();

		// AFTER_SEQUENCE_NUMBER repoint requests resolve to the same StartIterator (so a repoint, if it ran,
		// would still re-read seq 1 — the redelivery semantics are unchanged either way; the gate is what
		// distinguishes redelivery from skip).
		A.CallTo(() => streams.GetShardIteratorAsync(A<GetShardIteratorRequest>._, A<CancellationToken>._))
			.ReturnsLazily(() => new GetShardIteratorResponse { ShardIterator = StartIterator });

		// Iterator-aware records: StartIterator → [seq 1] (Next=NextIterator); NextIterator → [seq 2]
		// (Next=null, shard then exhausted); anything else → empty.
		A.CallTo(() => streams.GetRecordsAsync(A<GetRecordsRequest>._, A<CancellationToken>._))
			.ReturnsLazily((GetRecordsRequest req, CancellationToken _) => req.ShardIterator switch
			{
				StartIterator => new GetRecordsResponse
				{
					Records = [MakeRecord(FaultedSequence)],
					NextShardIterator = NextIterator,
				},
				NextIterator => new GetRecordsResponse
				{
					Records = [MakeRecord(FollowingSequence)],
					NextShardIterator = null,
				},
				_ => new GetRecordsResponse { Records = [], NextShardIterator = null },
			});

		var stateStore = A.Fake<IDynamoDbCdcStateStore>();
		A.CallTo(() => stateStore.GetPositionAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult<DynamoDbCdcPosition?>(null));
		A.CallTo(() => stateStore.SavePositionAsync(A<string>._, A<DynamoDbCdcPosition>._, A<CancellationToken>._))
			.Returns(Task.CompletedTask);

		var options = Options.Create(new DynamoDbCdcOptions
		{
			StreamArn = StreamArn,
			ProcessorName = "q3w5cv-redelivery-lock",
			AutoDiscoverShards = false,
			MaxBatchSize = 100,
			StartPosition = DynamoDbCdcPosition.Beginning(StreamArn),
		});

		return (streams, stateStore, options);
	}

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

	private static DynamoDbCdcProcessor BuildProcessor(
		IAmazonDynamoDBStreams streams,
		IDynamoDbCdcStateStore stateStore,
		IOptions<DynamoDbCdcOptions> options)
		=> new(
			A.Fake<IAmazonDynamoDB>(),
			streams,
			stateStore,
			options,
			NullLogger<DynamoDbCdcProcessor>.Instance);

	private static void SetShardIterator(DynamoDbCdcProcessor processor, string shardId, string iterator)
	{
		var dict = (ConcurrentDictionary<string, string>)ShardIteratorsField.GetValue(processor)!;
		dict[shardId] = iterator;
	}

	private static Task<int> InvokeProcessBatchInternal(
		DynamoDbCdcProcessor processor,
		Func<DynamoDbDataChangeEvent, CancellationToken, Task> handler,
		bool autoConfirm)
	{
		var result = ProcessBatchInternalMethod.Invoke(
			processor,
			[handler, autoConfirm, CancellationToken.None]);

		result.ShouldNotBeNull("ProcessBatchInternalAsync must return a non-null Task<int>.");
		return (Task<int>)result;
	}
}
