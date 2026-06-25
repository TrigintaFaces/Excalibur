// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;

using Amazon.DynamoDBStreams;
using Amazon.DynamoDBStreams.Model;
using Amazon.DynamoDBv2;

using Excalibur.Cdc.DynamoDb;

using Microsoft.Extensions.Logging.Abstractions;

using StreamsRecord = Amazon.DynamoDBStreams.Model.Record;

namespace Excalibur.Data.Tests.DynamoDb.Cdc;

/// <summary>
/// Regression lock for <c>bd-vabp2n</c> (Sprint 847, Lane D — CDC ack-ordering): the DynamoDB CDC
/// processor MUST NOT advance the shard cursor past a change that has not been successfully handled.
/// </summary>
/// <remarks>
/// <para>
/// Authored independently of the implementer (author ≠ impl, <c>pin-interface-seam-before-tests</c>);
/// the pinned non-vacuity seam is the <see cref="DynamoDbDataChangeEvent"/> <c>eventHandler</c> delegate.
/// </para>
/// <para>
/// <b>Pre-fix behaviour (RED on HEAD):</b> <c>ProcessBatchInternalAsync</c> assigns
/// <c>_shardIterators[shardId] = response.NextShardIterator</c> immediately after <c>GetRecordsAsync</c>
/// (before the per-record handler loop). When the handler throws mid-batch, the exception unwinds with the
/// iterator already advanced past the whole batch, so the next poll reads PAST the failed record and the
/// remaining records are silently skipped (data loss; at-least-once violated).
/// </para>
/// <para>
/// <b>Post-fix behaviour (GREEN):</b> the cursor advances only after a record is successfully handed off,
/// so a throwing handler leaves the shard at (or before) the failed record and it is re-delivered. This
/// lock asserts the observable contract (re-delivery), not a specific iterator mechanism, so it holds for
/// any correct fix.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "DynamoDb")]
[Trait(TraitNames.Feature, TestFeatures.CDC)]
public sealed class DynamoDbCdcProcessorAckOrderingShould
{
	private const string ShardId = "shardId-000000000001";

	private const string StreamArn =
		"arn:aws:dynamodb:us-east-1:000000000000:table/Excalibur/stream/2026-01-01T00:00:00.000";

	private const string IteratorPrefix = "AT-";

	[Fact]
	public async Task RedeliverRecordsAfterAHandlerFailure_NotSkipPastTheFailedChange()
	{
		// Arrange — a single shard carrying three records r1, r2, r3.
		var records = new[]
		{
			BuildRecord("1"),
			BuildRecord("2"),
			BuildRecord("3"),
		};

		var streams = A.Fake<IAmazonDynamoDBStreams>();

		A.CallTo(() => streams.DescribeStreamAsync(A<DescribeStreamRequest>._, A<CancellationToken>._))
			.ReturnsLazily(() => new DescribeStreamResponse
			{
				StreamDescription = new StreamDescription
				{
					Shards =
					[
						new Shard
						{
							ShardId = ShardId,
							SequenceNumberRange = new SequenceNumberRange { StartingSequenceNumber = "1" },
						},
					],
				},
			});

		// Stateful stream model: an iterator token "AT-<lastConsumedSeq>" yields all records with a
		// strictly greater sequence number. This models real DynamoDB iterator semantics so the lock is
		// agnostic to HOW a correct processor re-reads after a failure (hold the iterator, or re-acquire
		// AFTER_SEQUENCE_NUMBER) — both re-deliver the unhandled records.
		A.CallTo(() => streams.GetShardIteratorAsync(A<GetShardIteratorRequest>._, A<CancellationToken>._))
			.ReturnsLazily((GetShardIteratorRequest req, CancellationToken _) =>
			{
				var position = req.ShardIteratorType == ShardIteratorType.AFTER_SEQUENCE_NUMBER
					? int.Parse(req.SequenceNumber, CultureInfo.InvariantCulture)
					: 0;
				return new GetShardIteratorResponse { ShardIterator = IteratorPrefix + position.ToString(CultureInfo.InvariantCulture) };
			});

		A.CallTo(() => streams.GetRecordsAsync(A<GetRecordsRequest>._, A<CancellationToken>._))
			.ReturnsLazily((GetRecordsRequest req, CancellationToken _) =>
			{
				var consumed = int.Parse(req.ShardIterator[IteratorPrefix.Length..], CultureInfo.InvariantCulture);
				var batch = records
					.Where(r => int.Parse(r.Dynamodb.SequenceNumber, CultureInfo.InvariantCulture) > consumed)
					.ToList();
				var lastSeq = batch.Count > 0
					? int.Parse(batch[^1].Dynamodb.SequenceNumber, CultureInfo.InvariantCulture)
					: consumed;
				return new GetRecordsResponse
				{
					Records = batch,
					NextShardIterator = IteratorPrefix + lastSeq.ToString(CultureInfo.InvariantCulture),
				};
			});

		var stateStore = A.Fake<IDynamoDbCdcStateStore>();
		A.CallTo(() => stateStore.GetPositionAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult<DynamoDbCdcPosition?>(null));

		var options = Options.Create(new DynamoDbCdcOptions
		{
			StreamArn = StreamArn,
			ProcessorName = "vabp2n-ack-ordering-lock",
			AutoDiscoverShards = false,
			MaxBatchSize = 100,
			StartPosition = DynamoDbCdcPosition.Beginning(StreamArn),
		});

		var handledSequences = new List<string>();
		var hasFailedOnce = false;

		Task Handler(DynamoDbDataChangeEvent change, CancellationToken _)
		{
			handledSequences.Add(change.SequenceNumber);

			// Fail on r2 the first time it is seen, then recover — models a transient handler failure.
			if (change.SequenceNumber == "2" && !hasFailedOnce)
			{
				hasFailedOnce = true;
				throw new InvalidOperationException("Simulated transient handler failure on r2.");
			}

			return Task.CompletedTask;
		}

		await using var processor = new DynamoDbCdcProcessor(
			A.Fake<IAmazonDynamoDB>(),
			streams,
			stateStore,
			options,
			NullLogger<DynamoDbCdcProcessor>.Instance);

		// Act — first poll: r1 is handled, r2 throws. The exception must propagate (no swallow).
		_ = await Should.ThrowAsync<InvalidOperationException>(
			() => processor.ProcessBatchAsync(Handler, CancellationToken.None));

		var handledOnFirstPoll = handledSequences.Count;

		// Second poll: a correct processor has NOT advanced the cursor past the failed change, so r2 (and
		// r3) are re-delivered. The pre-fix processor advanced the iterator before handling, so the second
		// poll reads past r3 and r2/r3 are silently skipped.
		_ = await processor.ProcessBatchAsync(Handler, CancellationToken.None);

		// Assert — r2 and r3 were re-delivered on the second poll (at-least-once preserved, no silent skip).
		var secondPollSequences = handledSequences.Skip(handledOnFirstPoll).ToList();

		secondPollSequences.ShouldContain(
			"2",
			customMessage:
			"Expected r2 to be re-delivered after the handler failed: the cursor must not advance past an " +
			"unhandled change (RED on pre-fix HEAD, which advances the iterator before handling).");
		secondPollSequences.ShouldContain(
			"3",
			customMessage: "Expected r3 (the unhandled suffix after the failure) to be re-delivered, not skipped.");

		// And the first poll handled exactly the prefix up to and including the failed record (r1, r2).
		handledSequences.Take(handledOnFirstPoll).ShouldBe(["1", "2"]);
	}

	private static StreamsRecord BuildRecord(string sequenceNumber) => new()
	{
		EventID = "event-" + sequenceNumber,
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
