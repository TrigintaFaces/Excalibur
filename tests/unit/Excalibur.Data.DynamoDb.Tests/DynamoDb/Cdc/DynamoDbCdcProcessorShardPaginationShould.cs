// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

using Amazon.DynamoDBStreams;
using Amazon.DynamoDBStreams.Model;
using Amazon.DynamoDBv2;

using Microsoft.Extensions.Logging.Abstractions;

using StreamsRecord = Amazon.DynamoDBStreams.Model.Record;

namespace Excalibur.Data.Tests.DynamoDb.Cdc;

/// <summary>
/// Regression lock (SAFETY-CRITICAL, data loss) for shard discovery pagination in
/// <c>DynamoDbCdcProcessor.DiscoverShardsAsync</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>The invariant:</b> every shard the stream reports is considered, not merely every shard in the
/// first response. <c>DescribeStream</c> returns a bounded number of shards and signals that more remain
/// with <c>LastEvaluatedShardId</c>, which the next request echoes back as <c>ExclusiveStartShardId</c>.
/// </para>
/// <para>
/// <b>The defect this locks:</b> discovery sent exactly one request and treated its shard list as the
/// whole population. Every shard past the first page was therefore never given an iterator and its
/// records were never delivered — and because each periodic discovery repeated the same first request,
/// the gap never closed. Nothing surfaced: the processor made healthy, checkpointed progress over the
/// shards it did open.
/// </para>
/// <para>
/// <b>Why a small fixture cannot catch this:</b> below the page limit a single request IS the whole
/// population, so the defect is invisible until the stream is large. These arms make the page boundary
/// explicit instead of large.
/// </para>
/// <para>
/// <b>Non-vacuity:</b> <see cref="OpenAndDeliverFromAShardOnASecondPage"/> and
/// <see cref="RequestEachPageWithThePrecedingPagesContinuationToken"/> are both RED against a
/// single-request implementation — the page-two shard gets no iterator and only one request is sent.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "DynamoDb")]
[Trait(TraitNames.Feature, TestFeatures.CDC)]
public sealed class DynamoDbCdcProcessorShardPaginationShould
{
	private const string StreamArn =
		"arn:aws:dynamodb:us-east-1:000000000000:table/Excalibur/stream/2026-01-01T00:00:00.000";

	private const string PageOneShardId = "shardId-00000001700000000000-page1";
	private const string PageTwoShardId = "shardId-00000001700000000001-page2";
	private const string PageThreeShardId = "shardId-00000001700000000002-page3";

	private static readonly MethodInfo DiscoverShardsMethod =
		typeof(DynamoDbCdcProcessor)
			.GetMethod("DiscoverShardsAsync", BindingFlags.NonPublic | BindingFlags.Instance)
		?? throw new InvalidOperationException(
			"shard-pagination lock: 'DiscoverShardsAsync' private method not found — if it was renamed or "
			+ "inlined, update this lock to bind the new shard-discovery entry point.");

	private static readonly FieldInfo LastShardDiscoveryField =
		typeof(DynamoDbCdcProcessor)
			.GetField("_lastShardDiscovery", BindingFlags.NonPublic | BindingFlags.Instance)
		?? throw new InvalidOperationException(
			"shard-pagination lock: '_lastShardDiscovery' field not found — it is the value that suppresses "
			+ "discovery for a whole interval, so this lock must follow its rename.");

	/// <summary>
	/// THE ARM THAT MATTERS (liveness). A shard that only appears on the second page is opened, and the
	/// records on it reach the handler — through the public entry point, not a reflected helper.
	/// </summary>
	[Fact]
	public async Task OpenAndDeliverFromAShardOnASecondPage()
	{
		var harness = new Harness();
		harness.AddPage(after: null, next: PageOneShardId, OpenShard(PageOneShardId));
		harness.AddPage(after: PageOneShardId, next: null, OpenShard(PageTwoShardId));
		harness.SeedRecord(PageOneShardId, "000000000000000000001");
		harness.SeedRecord(PageTwoShardId, "000000000000000000002");

		await using var processor = harness.BuildProcessor();

		var processed = await processor.ProcessBatchAsync(harness.Handler, CancellationToken.None);

		harness.IteratorRequests.Select(r => r.ShardId).ShouldBe(
			[PageOneShardId, PageTwoShardId],
			ignoreOrder: true,
			customMessage: "a shard on a later page is a shard like any other; leaving it without an "
				+ "iterator strands its records permanently, because every later discovery repeats the "
				+ "same first request.");

		processed.ShouldBe(2);
		harness.Delivered.ShouldBe(
			["000000000000000000001", "000000000000000000002"],
			ignoreOrder: true,
			customMessage: "the page-two shard's records must actually be delivered — opening an iterator "
				+ "the batch never reads would be a fix in name only.");
	}

	/// <summary>
	/// SAFETY. The continuation token is propagated exactly: the first request carries none, and each
	/// later request carries the previous response's <c>LastEvaluatedShardId</c>.
	/// </summary>
	[Fact]
	public async Task RequestEachPageWithThePrecedingPagesContinuationToken()
	{
		var harness = new Harness();
		harness.AddPage(after: null, next: PageOneShardId, OpenShard(PageOneShardId));
		harness.AddPage(after: PageOneShardId, next: PageTwoShardId, OpenShard(PageTwoShardId));
		harness.AddPage(after: PageTwoShardId, next: null, OpenShard(PageThreeShardId));

		await using var processor = harness.BuildProcessor();

		_ = await processor.ProcessBatchAsync(harness.Handler, CancellationToken.None);

		harness.DescribeTokens.ShouldBe(
			[null, PageOneShardId, PageTwoShardId],
			"each request must resume exactly where the previous response stopped; a larger page limit is "
			+ "not a substitute for following the continuation.");
	}

	/// <summary>
	/// SAFETY. A failure part-way through pagination surfaces and does NOT mark discovery as done —
	/// otherwise one transient error on page two suppresses discovery for a whole interval while the
	/// shards on that page sit unopened.
	/// </summary>
	[Fact]
	public async Task LeaveDiscoveryDue_WhenAnIntermediatePageFails()
	{
		var harness = new Harness();
		harness.AddPage(after: null, next: null, OpenShard(PageOneShardId));

		await using var processor = harness.BuildProcessor();

		// Initialize successfully on a single page so the arm measures discovery, not initialization.
		_ = await processor.ProcessBatchAsync(harness.Handler, CancellationToken.None);
		var discoveryAfterSuccess = (DateTimeOffset)LastShardDiscoveryField.GetValue(processor)!;
		discoveryAfterSuccess.ShouldNotBe(
			DateTimeOffset.MinValue,
			"sanity: a successful discovery must stamp the time, or the arm below proves nothing.");

		// A split has produced a second page, and that page now fails.
		harness.AddPage(after: null, next: PageOneShardId, OpenShard(PageOneShardId));
		harness.FailPage(after: PageOneShardId);

		_ = await Should.ThrowAsync<AmazonDynamoDBStreamsException>(
			() => InvokeDiscoverShardsAsync(processor));

		((DateTimeOffset)LastShardDiscoveryField.GetValue(processor)!).ShouldBe(
			discoveryAfterSuccess,
			"a partial enumeration is not a discovery; stamping it would hide the unopened page-two shards "
			+ "until the whole interval elapsed again.");

		// The page now succeeds: the previously unreachable shard is opened.
		harness.HealPage(after: PageOneShardId, next: null, OpenShard(PageTwoShardId));
		await InvokeDiscoverShardsAsync(processor);

		harness.IteratorRequests.Select(r => r.ShardId).ShouldContain(PageTwoShardId);
		((DateTimeOffset)LastShardDiscoveryField.GetValue(processor)!).ShouldBeGreaterThan(
			discoveryAfterSuccess,
			"a complete enumeration stamps the time.");
	}

	/// <summary>
	/// PRECISION. The same shard appearing on more than one page is opened once, not once per appearance.
	/// </summary>
	/// <remarks>
	/// Without this arm the pagination fix is satisfied by an implementation that re-opens an iterator for
	/// every shard it sees, discarding the progress already made on it.
	/// </remarks>
	[Fact]
	public async Task OpenASingleIterator_WhenAShardAppearsOnTwoPages()
	{
		var harness = new Harness();
		harness.AddPage(after: null, next: PageOneShardId, OpenShard(PageOneShardId));
		harness.AddPage(after: PageOneShardId, next: null, OpenShard(PageOneShardId), OpenShard(PageTwoShardId));

		await using var processor = harness.BuildProcessor();

		_ = await processor.ProcessBatchAsync(harness.Handler, CancellationToken.None);

		harness.IteratorRequests
			.Count(r => string.Equals(r.ShardId, PageOneShardId, StringComparison.Ordinal))
			.ShouldBe(1, "a duplicate entry in the stream description is not a second shard.");
	}

	/// <summary>
	/// SAFETY (liveness of the process itself). A continuation token that never advances must end the
	/// enumeration rather than loop.
	/// </summary>
	/// <remarks>
	/// Pagination runs inside the initialization lock, so a non-advancing token would hang the processor
	/// outright — no exception, no progress, and nothing to diagnose it with.
	/// </remarks>
	[Fact]
	public async Task StopEnumerating_WhenTheContinuationTokenDoesNotAdvance()
	{
		var harness = new Harness();
		harness.AddPage(after: null, next: PageOneShardId, OpenShard(PageOneShardId));
		harness.AddPage(after: PageOneShardId, next: PageOneShardId, OpenShard(PageTwoShardId));

		await using var processor = harness.BuildProcessor();

		var poll = processor.ProcessBatchAsync(harness.Handler, CancellationToken.None);

		_ = await poll.WaitAsync(TimeSpan.FromSeconds(30));

		harness.DescribeTokens.ShouldBe(
			[null, PageOneShardId],
			"the repeated token ends the enumeration; anything else is an unbounded loop holding the "
			+ "initialization lock.");
	}

	/// <summary>
	/// SAFETY. Cancellation during pagination propagates rather than silently truncating the shard set to
	/// the pages that happened to arrive first.
	/// </summary>
	[Fact]
	public async Task PropagateCancellation_WhenItArrivesBetweenPages()
	{
		using var cts = new CancellationTokenSource();

		var harness = new Harness();
		harness.AddPage(after: null, next: PageOneShardId, OpenShard(PageOneShardId));
		harness.CancelBefore(after: PageOneShardId, cts);

		await using var processor = harness.BuildProcessor();

		_ = await Should.ThrowAsync<OperationCanceledException>(
			() => processor.ProcessBatchAsync(harness.Handler, cts.Token));

		harness.IteratorRequests.Select(r => r.ShardId).ShouldNotContain(
			PageTwoShardId,
			"a canceled enumeration must not be mistaken for a complete one.");
	}

	// ─── Helpers ────────────────────────────────────────────────────────────

	private static Shard OpenShard(string shardId) => new()
	{
		ShardId = shardId,
		SequenceNumberRange = new SequenceNumberRange { StartingSequenceNumber = "000000000000000000001" },
	};

	private static async Task InvokeDiscoverShardsAsync(DynamoDbCdcProcessor processor)
	{
		var result = DiscoverShardsMethod.Invoke(processor, [CancellationToken.None]);
		result.ShouldNotBeNull("DiscoverShardsAsync must return a non-null Task.");
		await (Task)result;
	}

	private sealed class Harness
	{
		private const string NoToken = " none";

		private readonly Dictionary<string, (List<Shard> Shards, string? Next)> _pages =
			new(StringComparer.Ordinal);

		private readonly HashSet<string> _failingPages = new(StringComparer.Ordinal);
		private readonly Dictionary<string, CancellationTokenSource> _cancelBefore = new(StringComparer.Ordinal);
		private readonly Dictionary<string, StreamsRecord> _pendingRecords = new(StringComparer.Ordinal);
		private readonly HashSet<string> _drainedIterators = new(StringComparer.Ordinal);

		public Harness()
		{
			Streams = A.Fake<IAmazonDynamoDBStreams>();

			A.CallTo(() => Streams.DescribeStreamAsync(A<DescribeStreamRequest>._, A<CancellationToken>._))
				.ReturnsLazily((DescribeStreamRequest request, CancellationToken token) =>
				{
					var key = request.ExclusiveStartShardId ?? NoToken;
					DescribeTokens.Add(request.ExclusiveStartShardId);

					if (_cancelBefore.TryGetValue(key, out var cts))
					{
						cts.Cancel();
						token.ThrowIfCancellationRequested();
					}

					if (_failingPages.Contains(key))
					{
						throw new AmazonDynamoDBStreamsException("injected page failure");
					}

					var page = _pages[key];
					return new DescribeStreamResponse
					{
						StreamDescription = new StreamDescription
						{
							StreamStatus = StreamStatus.ENABLED,
							Shards = [.. page.Shards],
							LastEvaluatedShardId = page.Next,
						},
					};
				});

			A.CallTo(() => Streams.GetShardIteratorAsync(A<GetShardIteratorRequest>._, A<CancellationToken>._))
				.ReturnsLazily((GetShardIteratorRequest request, CancellationToken _) =>
				{
					IteratorRequests.Add(request);
					return new GetShardIteratorResponse { ShardIterator = "iterator|" + request.ShardId };
				});

			A.CallTo(() => Streams.GetRecordsAsync(A<GetRecordsRequest>._, A<CancellationToken>._))
				.ReturnsLazily((GetRecordsRequest request, CancellationToken _) =>
				{
					var shardId = request.ShardIterator.Split('|')[1];

					List<StreamsRecord> batch =
						_drainedIterators.Add(request.ShardIterator) &&
						_pendingRecords.TryGetValue(shardId, out var record)
							? [record]
							: [];

					return new GetRecordsResponse { Records = batch, NextShardIterator = request.ShardIterator };
				});

			StateStore = A.Fake<IDynamoDbCdcStateStore>();
			A.CallTo(() => StateStore.GetPositionAsync(A<string>._, A<CancellationToken>._))
				.Returns(Task.FromResult<DynamoDbCdcPosition?>(null));
			A.CallTo(() => StateStore.SavePositionAsync(
					A<string>._, A<DynamoDbCdcPosition>._, A<CancellationToken>._))
				.Returns(Task.CompletedTask);
		}

		public IAmazonDynamoDBStreams Streams { get; }

		public IDynamoDbCdcStateStore StateStore { get; }

		public List<GetShardIteratorRequest> IteratorRequests { get; } = [];

		public List<string?> DescribeTokens { get; } = [];

		public List<string> Delivered { get; } = [];

		public void AddPage(string? after, string? next, params Shard[] shards) =>
			_pages[after ?? NoToken] = ([.. shards], next);

		public void HealPage(string? after, string? next, params Shard[] shards)
		{
			_ = _failingPages.Remove(after ?? NoToken);
			AddPage(after, next, shards);
		}

		public void FailPage(string? after) => _failingPages.Add(after ?? NoToken);

		public void CancelBefore(string? after, CancellationTokenSource cts) =>
			_cancelBefore[after ?? NoToken] = cts;

		public void SeedRecord(string shardId, string sequenceNumber) =>
			_pendingRecords[shardId] = new StreamsRecord
			{
				EventID = sequenceNumber,
				EventName = OperationType.INSERT,
				Dynamodb = new StreamRecord
				{
					SequenceNumber = sequenceNumber,
					ApproximateCreationDateTime = DateTime.UtcNow,
					Keys = new Dictionary<string, Amazon.DynamoDBStreams.Model.AttributeValue>(StringComparer.Ordinal)
					{
						["pk"] = new() { S = "order#" + sequenceNumber },
					},
				},
			};

		public Task Handler(DynamoDbDataChangeEvent change, CancellationToken cancellationToken)
		{
			Delivered.Add(change.SequenceNumber);
			return Task.CompletedTask;
		}

		public DynamoDbCdcProcessor BuildProcessor() => new(
			A.Fake<IAmazonDynamoDB>(),
			Streams,
			StateStore,
			Microsoft.Extensions.Options.Options.Create(new DynamoDbCdcOptions
			{
				StreamArn = StreamArn,
				ProcessorName = "shard-pagination-lock",
				AutoDiscoverShards = false,
				MaxBatchSize = 100,
			}),
			NullLogger<DynamoDbCdcProcessor>.Instance);
	}
}
