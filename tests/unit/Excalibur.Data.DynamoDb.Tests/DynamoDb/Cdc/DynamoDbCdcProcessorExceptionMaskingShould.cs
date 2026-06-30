// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;
using System.Reflection;

using Amazon.DynamoDBStreams;
using Amazon.DynamoDBStreams.Model;
using Amazon.DynamoDBv2;

using Microsoft.Extensions.Logging.Abstractions;

using StreamsRecord = Amazon.DynamoDBStreams.Model.Record;

namespace Excalibur.Data.Tests.DynamoDb.Cdc;

/// <summary>
/// Regression lock for <c>bd-8p7qwj</c> (S856, SAFETY-RELEVANT): the DynamoDB CDC processor's
/// in-catch confirm step MUST NOT mask the original handler exception.
/// </summary>
/// <remarks>
/// <para>
/// <b>Pre-fix behaviour (RED):</b> inside the <c>catch</c> block, when <c>autoConfirm=true</c>,
/// <c>ConfirmPositionAsync</c> (or <c>GetCurrentPositionAsync</c>) was called without a guard. If it threw,
/// the confirm exception escaped the catch and replaced the original handler exception — the root cause
/// was silently replaced by a secondary failure, masking the real error.
/// </para>
/// <para>
/// <b>Post-fix behaviour (GREEN):</b> the confirm step is wrapped in its own <c>try/catch</c>.
/// A confirm/position-read failure is logged but never rethrows; <c>batchFailure</c> always holds the
/// original handler exception, which is re-thrown after the structural durability gate.
/// </para>
/// <para>
/// <b>Test seam:</b> <c>ProcessBatchInternalAsync</c> is <c>private</c>. The public <c>ProcessBatchAsync</c>
/// uses <c>autoConfirm=false</c> and therefore never exercises the in-catch confirm path. This lock
/// invokes the private method via reflection with <c>autoConfirm=true</c> to reach the guarded path.
/// Per the project testing-patterns rule: "use the ecosystem's friend-assembly / reflection mechanism
/// rather than widening production visibility."
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "DynamoDb")]
[Trait(TraitNames.Feature, TestFeatures.CDC)]
public sealed class DynamoDbCdcProcessorExceptionMaskingShould
{
	private const string StreamArn =
		"arn:aws:dynamodb:us-east-1:000000000000:table/Excalibur/stream/2026-01-01T00:00:00.000";

	private const string ShardId = "shardId-000000000001";
	private const string InitialIterator = "ITER-0";
	private const string NextIterator = "ITER-1";

	// The private method that exercises the autoConfirm=true path (unreachable via the public API).
	private static readonly MethodInfo ProcessBatchInternalMethod =
		typeof(DynamoDbCdcProcessor)
			.GetMethod("ProcessBatchInternalAsync", BindingFlags.NonPublic | BindingFlags.Instance)
		?? throw new InvalidOperationException(
			"bd-8p7qwj lock: 'ProcessBatchInternalAsync' private method not found. " +
			"This is the non-vacuity proof anchor — if the method is renamed/removed, update this lock.");

	/// <summary>
	/// N4.1 — handler throws HandlerEx, ConfirmPositionAsync (SavePositionAsync) throws ConfirmEx:
	/// HandlerEx must propagate, ConfirmEx must not replace it.
	/// </summary>
	[Fact]
	public async Task PropagateHandlerException_WhenConfirmPositionThrows()
	{
		// Arrange
		var (streams, stateStore, options) = BuildFixture();

		// Set up SavePositionAsync to throw a confirm exception (the secondary failure).
		var confirmEx = new InvalidOperationException("bd-8p7qwj: simulated ConfirmPositionAsync failure");
		A.CallTo(() => stateStore.SavePositionAsync(A<string>._, A<DynamoDbCdcPosition>._, A<CancellationToken>._))
			.ThrowsAsync(confirmEx);

		await using var processor = BuildProcessor(streams, stateStore, options);

		// Initialize shards by running one successful batch (autoConfirm=false via public API).
		await processor.ProcessBatchAsync((_, _) => Task.CompletedTask, CancellationToken.None);

		// Handler that throws the original (root-cause) exception.
		var handlerEx = new ArgumentException("bd-8p7qwj: simulated handler failure");
		Task ThrowingHandler(DynamoDbDataChangeEvent _, CancellationToken __) => throw handlerEx;

		// Act — invoke the private method with autoConfirm=true so the in-catch confirm path runs.
		var resultTask = InvokeProcessBatchInternal(processor, ThrowingHandler, autoConfirm: true);

		// Assert — the HANDLER exception propagates; the confirm exception must NOT replace it.
		// Non-vacuous: pre-fix (no inner try/catch), ConfirmPositionAsync throws confirmEx
		// which propagates out of the outer catch → test sees confirmEx, not handlerEx → FAIL → RED.
		var thrown = await Should.ThrowAsync<ArgumentException>(() => resultTask);
		thrown.ShouldBeSameAs(handlerEx,
			"bd-8p7qwj N4.1: the root-cause handler exception must propagate. " +
			"Pre-fix: ConfirmPositionAsync throws replaces handlerEx with confirmEx → exception-masking bug.");
	}

	/// <summary>
	/// N4.2 — handler throws HandlerEx, ConfirmPositionAsync succeeds:
	/// HandlerEx must still propagate (the normal flow after the fix is identical).
	/// </summary>
	[Fact]
	public async Task PropagateHandlerException_WhenConfirmPositionSucceeds()
	{
		// Arrange
		var (streams, stateStore, options) = BuildFixture();

		// SavePositionAsync succeeds — confirm step does not interfere.
		A.CallTo(() => stateStore.SavePositionAsync(A<string>._, A<DynamoDbCdcPosition>._, A<CancellationToken>._))
			.Returns(Task.CompletedTask);

		await using var processor = BuildProcessor(streams, stateStore, options);

		// Initialize shards.
		await processor.ProcessBatchAsync((_, _) => Task.CompletedTask, CancellationToken.None);

		var handlerEx = new InvalidOperationException("bd-8p7qwj N4.2: handler failure with confirm succeeding");
		Task ThrowingHandler(DynamoDbDataChangeEvent _, CancellationToken __) => throw handlerEx;

		// Act
		var resultTask = InvokeProcessBatchInternal(processor, ThrowingHandler, autoConfirm: true);

		// Assert — handlerEx propagates even when confirm succeeds; ExceptionDispatchInfo re-throws it.
		// Non-vacuous: verifies the full exception-propagation path (batchFailure → Throw) is wired.
		var thrown = await Should.ThrowAsync<InvalidOperationException>(() => resultTask);
		thrown.ShouldBeSameAs(handlerEx,
			"bd-8p7qwj N4.2: the handler exception must propagate through the structural durability gate.");
	}

	/// <summary>
	/// N4.3 — validates the method's non-vacuity anchor: the private method must exist and accept a bool.
	/// A rename/removal breaks the reflection lookup → test fails immediately → non-vacuous signal.
	/// </summary>
	[Fact]
	public void PrivateMethod_MustExistWithAutoConfirmParameter()
	{
		// Assert — the static initialiser already threw if the method is missing. This test exists to
		// surface that failure as a named test rather than a test-class instantiation error.
		ProcessBatchInternalMethod.ShouldNotBeNull(
			"bd-8p7qwj: 'ProcessBatchInternalAsync' must exist as a private instance method on DynamoDbCdcProcessor. " +
			"If it was renamed/inlined, update this lock to bind the new name.");

		var parameters = ProcessBatchInternalMethod.GetParameters();
		var hasAutoConfirm = parameters.Any(p =>
			p.ParameterType == typeof(bool)
			&& p.Name?.Contains("auto", StringComparison.OrdinalIgnoreCase) == true);

		hasAutoConfirm.ShouldBeTrue(
			"bd-8p7qwj: the private method must accept a bool autoConfirm parameter. " +
			"If the signature changed, the lock needs to be updated.");
	}

	// ─── Fixture helpers ────────────────────────────────────────────────────

	private static (IAmazonDynamoDBStreams Streams, IDynamoDbCdcStateStore StateStore, IOptions<DynamoDbCdcOptions> Options)
		BuildFixture()
	{
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
							SequenceNumberRange = new SequenceNumberRange { StartingSequenceNumber = "0" },
						},
					],
				},
			});

		A.CallTo(() => streams.GetShardIteratorAsync(A<GetShardIteratorRequest>._, A<CancellationToken>._))
			.ReturnsLazily((GetShardIteratorRequest req, CancellationToken _) =>
			{
				// Return a simple iterator token; the exact value doesn't matter for this lock.
				var token = req.ShardIteratorType == ShardIteratorType.AFTER_SEQUENCE_NUMBER
					? $"ITER-{req.SequenceNumber}"
					: InitialIterator;
				return new GetShardIteratorResponse { ShardIterator = token };
			});

		// Always return one record so each ProcessBatchInternalAsync call has something to process.
		var record = new StreamsRecord
		{
			EventID = "event-1",
			EventName = OperationType.INSERT,
			Dynamodb = new StreamRecord
			{
				SequenceNumber = "1",
				ApproximateCreationDateTime = DateTime.UtcNow,
				Keys = new Dictionary<string, AttributeValue>(StringComparer.Ordinal)
				{
					["id"] = new AttributeValue { S = "1" },
				},
				NewImage = new Dictionary<string, AttributeValue>(StringComparer.Ordinal)
				{
					["id"] = new AttributeValue { S = "1" },
				},
			},
		};

		A.CallTo(() => streams.GetRecordsAsync(A<GetRecordsRequest>._, A<CancellationToken>._))
			.Returns(new GetRecordsResponse
			{
				Records = [record],
				NextShardIterator = NextIterator,
			});

		var stateStore = A.Fake<IDynamoDbCdcStateStore>();
		A.CallTo(() => stateStore.GetPositionAsync(A<string>._, A<CancellationToken>._))
			.Returns(Task.FromResult<DynamoDbCdcPosition?>(null));

		var options = Options.Create(new DynamoDbCdcOptions
		{
			StreamArn = StreamArn,
			ProcessorName = "8p7qwj-exception-masking-lock",
			AutoDiscoverShards = false,
			MaxBatchSize = 100,
			StartPosition = DynamoDbCdcPosition.Beginning(StreamArn),
		});

		return (streams, stateStore, options);
	}

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

	/// <summary>
	/// Invokes the private <c>ProcessBatchInternalAsync</c> via reflection with the given handler and
	/// <paramref name="autoConfirm"/> flag, returning the resulting <see cref="Task"/>.
	/// </summary>
	private static Task InvokeProcessBatchInternal(
		DynamoDbCdcProcessor processor,
		Func<DynamoDbDataChangeEvent, CancellationToken, Task> handler,
		bool autoConfirm)
	{
		var result = ProcessBatchInternalMethod.Invoke(
			processor,
			[handler, autoConfirm, CancellationToken.None]);

		result.ShouldNotBeNull("ProcessBatchInternalAsync must return a non-null Task.");
		return (Task)result;
	}
}
