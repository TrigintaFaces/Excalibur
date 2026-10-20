// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.Json;
using System.Text.Json.Serialization;

using Amazon.DynamoDBStreams;
using Amazon.DynamoDBStreams.Model;
using Amazon.DynamoDBv2;

using Excalibur.Data.CloudNative;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using StreamsRecord = Amazon.DynamoDBStreams.Model.Record;

namespace Excalibur.Data.DynamoDb.Tests.Streams;

[Trait("Category", "Unit")]
[Trait("Component", "DynamoDb")]
[Trait("Pattern", "Serialization")]
public sealed class DynamoDbStreamsSubscriptionDocumentSerializationShould
{
	private const string ShardId = "shardId-000000000000000000000001";
	private const string StreamArn = "arn:aws:dynamodb:us-east-1:123456789012:table/table/stream/2026-01-01T00:00:00.000";
	private const string OtherShardId = "shardId-000000000000000000000002";
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReadActualNewAndOldImagesUsingConsumerMetadata(bool oldImage)
	{
		var harness = new Harness([ImageRecord("1", oldImage)]);
		await using var subscription = harness.Create();
		await subscription.StartAsync(CancellationToken.None);
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		await using var reader = subscription.ReadChangesAsync(timeout.Token).GetAsyncEnumerator(timeout.Token);
		(await reader.MoveNextAsync()).ShouldBeTrue();
		var document = reader.Current.Document;
		document.ShouldNotBeNull();
		document.Name.ShouldBe("image");
		document.Count.ShouldBe(42);
		document.Optional.ShouldBeNull();
		document.Nested.ShouldNotBeNull();
		document.Nested.Value.ShouldBe("nested");
	}

	[Fact]
	public async Task WireProviderOptionsToTheActualSubscription()
	{
		var harness = new Harness([ImageRecord("1")]);
		await using var provider = new DynamoDbPersistenceProvider(harness.Client, harness.Streams,
			Options.Create(new DynamoDbOptions { StreamDocumentTypeInfoResolver = StreamDocumentTestJsonContext.Default }),
			NullLogger<DynamoDbPersistenceProvider>.Instance);
		await using var subscription = await provider.CreateChangeFeedSubscriptionAsync<StreamTestDocument>("table", harness.Options, CancellationToken.None);
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		await using var reader = subscription.ReadChangesAsync(timeout.Token).GetAsyncEnumerator(timeout.Token);
		(await reader.MoveNextAsync()).ShouldBeTrue();
		reader.Current.Document.ShouldNotBeNull().Name.ShouldBe("image");
	}

	[Fact]
	public async Task HonorTheConsumerContextConverterAndOptionsForActualImages()
	{
		var converter = new StreamDocumentTestConverter();
		var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
		options.Converters.Add(converter);
		var harness = new Harness([ImageRecord("1")]);
		await using var subscription = harness.Create(new StreamDocumentTestJsonContext(options));
		await subscription.StartAsync(CancellationToken.None);
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		await using var reader = subscription.ReadChangesAsync(timeout.Token).GetAsyncEnumerator(timeout.Token);
		(await reader.MoveNextAsync()).ShouldBeTrue();
		reader.Current.Document.ShouldNotBeNull().Name.ShouldBe("converted:image");
		converter.Reads.ShouldBe(1);
		converter.CamelCaseObserved.ShouldBeTrue();
	}

	[Fact]
	public async Task RefuseMissingMetadataAtTheFirstImageAndLeaveTheCheckpointUnchanged()
	{
		var harness = new Harness([ImageRecord("1")]);
		await using var subscription = harness.Create(new StreamDocumentEmptyJsonContext());
		await subscription.StartAsync(CancellationToken.None);
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		await using var reader = subscription.ReadChangesAsync(timeout.Token).GetAsyncEnumerator(timeout.Token);
		var exception = await Should.ThrowAsync<NotSupportedException>(() => reader.MoveNextAsync().AsTask());
		exception.Message.ShouldContain("StreamDocumentTypeInfoResolver");
		subscription.CurrentContinuationToken.ShouldBeNull();
	}

	[Fact]
	public async Task KeepOnlyTheOfferedFirstRecordWhenTheSecondImageFails()
	{
		var malformed = ImageRecord("2");
		malformed.Dynamodb.NewImage["Count"] = new AttributeValue { S = "not a number" };
		var harness = new Harness([ImageRecord("1"), malformed]);
		await using var subscription = harness.Create();
		await subscription.StartAsync(CancellationToken.None);
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		await using var reader = subscription.ReadChangesAsync(timeout.Token).GetAsyncEnumerator(timeout.Token);
		(await reader.MoveNextAsync()).ShouldBeTrue();
		var offered = reader.Current.ContinuationToken;
		subscription.CurrentContinuationToken.ShouldBe(offered);
		await Should.ThrowAsync<JsonException>(() => reader.MoveNextAsync().AsTask());
		subscription.CurrentContinuationToken.ShouldBe(offered);
		DynamoDbStreamsContinuationToken.Decode(offered).ShouldContainKeyAndValue(ShardId, "1");
	}

	[Fact]
	public async Task AvoidMetadataResolutionForAKeysOnlyEvent()
	{
		var record = ImageRecord("1");
		record.Dynamodb.NewImage = null;
		var harness = new Harness([record]);
		await using var subscription = harness.Create(new StreamDocumentEmptyJsonContext());
		await subscription.StartAsync(CancellationToken.None);
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		await using var reader = subscription.ReadChangesAsync(timeout.Token).GetAsyncEnumerator(timeout.Token);
		(await reader.MoveNextAsync()).ShouldBeTrue();
		reader.Current.Document.ShouldBeNull();
	}

	[Fact]
	public async Task KeepTheFirstOfferedPositionWhenTheConsumerDisposesEarly()
	{
		var harness = new Harness([ImageRecord("1"), ImageRecord("2")]);
		await using var subscription = harness.Create();
		await subscription.StartAsync(CancellationToken.None);
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		var reader = subscription.ReadChangesAsync(timeout.Token).GetAsyncEnumerator(timeout.Token);
		try
		{
			(await reader.MoveNextAsync()).ShouldBeTrue();
			subscription.CurrentContinuationToken.ShouldBe(reader.Current.ContinuationToken);
		}
		finally { await reader.DisposeAsync(); }
		DynamoDbStreamsContinuationToken.Decode(subscription.CurrentContinuationToken!).ShouldContainKeyAndValue(ShardId, "1");
		harness.Enqueue(ImageRecord("2"));
		await using var next = subscription.ReadChangesAsync(timeout.Token).GetAsyncEnumerator(timeout.Token);
		(await next.MoveNextAsync()).ShouldBeTrue();
		harness.IteratorRequests.Last().ShardIteratorType.ShouldBe(ShardIteratorType.AFTER_SEQUENCE_NUMBER);
		harness.IteratorRequests.Last().SequenceNumber.ShouldBe("1");
	}

	[Fact]
	public async Task AvoidPublishingTheUnprocessedTailAfterCancellation()
	{
		var harness = new Harness([ImageRecord("1"), ImageRecord("2")]);
		await using var subscription = harness.Create();
		await subscription.StartAsync(CancellationToken.None);
		using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		await using var reader = subscription.ReadChangesAsync(cancellation.Token).GetAsyncEnumerator(cancellation.Token);
		(await reader.MoveNextAsync()).ShouldBeTrue();
		var first = reader.Current.ContinuationToken;
		await cancellation.CancelAsync();
		await Should.ThrowAsync<OperationCanceledException>(() => reader.MoveNextAsync().AsTask());
		subscription.CurrentContinuationToken.ShouldBe(first);
	}

	[Fact]
	public async Task PreserveOtherShardPositionsAndEmitAResumableFullFeedToken()
	{
		var harness = new Harness([ImageRecord("2")], [ShardId, OtherShardId]);
		var startingToken = DynamoDbStreamsContinuationToken.Encode(new Dictionary<string, string>
		{ [ShardId] = "1", [OtherShardId] = "9" });
		A.CallTo(() => harness.Options.StartPosition).Returns(ChangeFeedStartPosition.FromContinuationToken);
		A.CallTo(() => harness.Options.ContinuationToken).Returns(startingToken);
		await using var subscription = harness.Create();
		await subscription.StartAsync(CancellationToken.None);
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		await using var reader = subscription.ReadChangesAsync(timeout.Token).GetAsyncEnumerator(timeout.Token);
		(await reader.MoveNextAsync()).ShouldBeTrue();
		var offered = reader.Current.ContinuationToken;
		var positions = DynamoDbStreamsContinuationToken.Decode(offered);
		positions.ShouldContainKeyAndValue(ShardId, "2");
		positions.ShouldContainKeyAndValue(OtherShardId, "9");
		subscription.CurrentContinuationToken.ShouldBe(offered);

		var resumed = new Harness([ImageRecord("3")], [ShardId, OtherShardId]);
		A.CallTo(() => resumed.Options.StartPosition).Returns(ChangeFeedStartPosition.FromContinuationToken);
		A.CallTo(() => resumed.Options.ContinuationToken).Returns(offered);
		await using var resumedSubscription = resumed.Create();
		await resumedSubscription.StartAsync(CancellationToken.None);
		await using var resumedReader = resumedSubscription.ReadChangesAsync(timeout.Token).GetAsyncEnumerator(timeout.Token);
		(await resumedReader.MoveNextAsync()).ShouldBeTrue();
		resumed.IteratorRequests.Single(r => r.ShardId == ShardId).SequenceNumber.ShouldBe("2");
		resumed.IteratorRequests.Single(r => r.ShardId == OtherShardId).SequenceNumber.ShouldBe("9");
		resumed.IteratorRequests.ShouldAllBe(r => r.ShardIteratorType == ShardIteratorType.AFTER_SEQUENCE_NUMBER);
	}

	[Fact]
	public async Task RejectOverlappingEnumerationsAndReleaseOwnershipOnDispose()
	{
		var harness = new Harness([ImageRecord("1")]);
		await using var subscription = harness.Create();
		await subscription.StartAsync(CancellationToken.None);
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		var owner = subscription.ReadChangesAsync(timeout.Token).GetAsyncEnumerator(timeout.Token);
		try
		{
			(await owner.MoveNextAsync()).ShouldBeTrue();
			await using var overlap = subscription.ReadChangesAsync(timeout.Token).GetAsyncEnumerator(timeout.Token);
			await Should.ThrowAsync<InvalidOperationException>(() => overlap.MoveNextAsync().AsTask());
		}
		finally { await owner.DisposeAsync(); }
		harness.Enqueue(ImageRecord("2"));
		await using var replacement = subscription.ReadChangesAsync(timeout.Token).GetAsyncEnumerator(timeout.Token);
		(await replacement.MoveNextAsync()).ShouldBeTrue();
		replacement.Current.Document.ShouldNotBeNull();
	}

	[Fact]
	public async Task RefuseAMissingSequenceWithoutPublishingAndReleaseOwnershipAfterFailure()
	{
		var invalid = ImageRecord("");
		var harness = new Harness([invalid]);
		await using var subscription = harness.Create();
		await subscription.StartAsync(CancellationToken.None);
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		await using var failed = subscription.ReadChangesAsync(timeout.Token).GetAsyncEnumerator(timeout.Token);
		await Should.ThrowAsync<InvalidOperationException>(() => failed.MoveNextAsync().AsTask());
		subscription.CurrentContinuationToken.ShouldBeNull();
		harness.Enqueue(ImageRecord("1"));
		await using var replacement = subscription.ReadChangesAsync(timeout.Token).GetAsyncEnumerator(timeout.Token);
		(await replacement.MoveNextAsync()).ShouldBeTrue();
	}

	[Fact]
	public async Task ReopenAnExpiredIteratorAfterTheLastOfferedPosition()
	{
		var harness = new Harness([ImageRecord("1")]);
		var calls = 0;
		A.CallTo(() => harness.Streams.GetRecordsAsync(A<GetRecordsRequest>._, A<CancellationToken>._)).ReturnsLazily(() =>
		{
			calls++;
			if (calls == 2) throw new ExpiredIteratorException("controlled expiry");
			return new GetRecordsResponse { Records = [ImageRecord(calls == 1 ? "1" : "2")], NextShardIterator = "iterator" };
		});
		await using var subscription = harness.Create();
		await subscription.StartAsync(CancellationToken.None);
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		await using var reader = subscription.ReadChangesAsync(timeout.Token).GetAsyncEnumerator(timeout.Token);
		(await reader.MoveNextAsync()).ShouldBeTrue();
		(await reader.MoveNextAsync()).ShouldBeTrue();
		harness.IteratorRequests.Count.ShouldBe(2);
		harness.IteratorRequests[1].ShardIteratorType.ShouldBe(ShardIteratorType.AFTER_SEQUENCE_NUMBER);
		harness.IteratorRequests[1].SequenceNumber.ShouldBe("1");
	}

	private static StreamsRecord ImageRecord(string sequence, bool oldImage = false)
	{
		var image = new Dictionary<string, AttributeValue>
		{
			["Name"] = new() { S = "image" },
			["Count"] = new() { N = "42" },
			["Optional"] = new() { NULL = true },
			["Nested"] = new() { M = new Dictionary<string, AttributeValue> { ["Value"] = new() { S = "nested" } } },
		};
		return new StreamsRecord
		{
			EventName = oldImage ? OperationType.REMOVE : OperationType.INSERT,
			Dynamodb = new StreamRecord
			{
				SequenceNumber = sequence,
				Keys = new Dictionary<string, AttributeValue> { ["pk"] = new() { S = "partition" }, ["sk"] = new() { S = "document" } },
				NewImage = oldImage ? null : image,
				OldImage = oldImage ? image : null,
			},
		};
	}

	private sealed class Harness
	{
		private List<StreamsRecord> _pending;
		internal Harness(List<StreamsRecord> records, string[]? shardIds = null)
		{
			_pending = records;
			Client = A.Fake<IAmazonDynamoDB>();
			Streams = A.Fake<IAmazonDynamoDBStreams>();
			Options = A.Fake<IChangeFeedOptions>();
			A.CallTo(() => Options.MaxBatchSize).Returns(100);
			A.CallTo(() => Options.PollingInterval).Returns(TimeSpan.FromMilliseconds(1));
			A.CallTo(() => Options.StartPosition).Returns(ChangeFeedStartPosition.Beginning);
			A.CallTo(() => Client.DescribeTableAsync("table", A<CancellationToken>._)).Returns(new Amazon.DynamoDBv2.Model.DescribeTableResponse
			{ Table = new Amazon.DynamoDBv2.Model.TableDescription { LatestStreamArn = StreamArn } });
			A.CallTo(() => Streams.DescribeStreamAsync(A<DescribeStreamRequest>._, A<CancellationToken>._)).Returns(new DescribeStreamResponse
			{ StreamDescription = new StreamDescription { StreamStatus = StreamStatus.ENABLED, Shards = (shardIds ?? [ShardId]).Select(id => new Shard { ShardId = id }).ToList() } });
			A.CallTo(() => Streams.GetShardIteratorAsync(A<GetShardIteratorRequest>._, A<CancellationToken>._)).ReturnsLazily((GetShardIteratorRequest request, CancellationToken _) =>
			{
				IteratorRequests.Add(request);
				return new GetShardIteratorResponse { ShardIterator = "iterator" };
			});
			A.CallTo(() => Streams.GetRecordsAsync(A<GetRecordsRequest>._, A<CancellationToken>._)).ReturnsLazily(() =>
			{
				var batch = _pending;
				_pending = [];
				return new GetRecordsResponse { Records = batch, NextShardIterator = "iterator" };
			});
		}

		internal IAmazonDynamoDB Client { get; }
		internal IAmazonDynamoDBStreams Streams { get; }
		internal IChangeFeedOptions Options { get; }
		internal List<GetShardIteratorRequest> IteratorRequests { get; } = [];
		internal void Enqueue(StreamsRecord record) => _pending.Add(record);
		internal DynamoDbStreamsSubscription<StreamTestDocument> Create(JsonSerializerContext? context = null) => new(
			Client, Streams, "table", "pk", "sk", Options, NullLogger.Instance, context ?? StreamDocumentTestJsonContext.Default);
	}
}

internal sealed class StreamTestDocument
{
	public string? Name { get; set; }
	public int Count { get; set; }
	public string? Optional { get; set; }
	public StreamTestNested? Nested { get; set; }
}

internal sealed class StreamTestNested
{
	public string? Value { get; set; }
}

[JsonSerializable(typeof(StreamTestDocument))]
internal sealed partial class StreamDocumentTestJsonContext : JsonSerializerContext;

[JsonSerializable(typeof(string))]
internal sealed partial class StreamDocumentEmptyJsonContext : JsonSerializerContext;

internal sealed class StreamDocumentTestConverter : JsonConverter<StreamTestDocument>
{
	internal int Reads { get; private set; }
	internal bool CamelCaseObserved { get; private set; }
	public override StreamTestDocument Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		Reads++;
		CamelCaseObserved = options.PropertyNamingPolicy == JsonNamingPolicy.CamelCase;
		using var document = JsonDocument.ParseValue(ref reader);
		return new StreamTestDocument { Name = "converted:" + document.RootElement.GetProperty("Name").GetString() };
	}

	public override void Write(Utf8JsonWriter writer, StreamTestDocument value, JsonSerializerOptions options) =>
		throw new NotSupportedException("This control reads stream images only.");
}
