// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Amazon.DynamoDBStreams;
using Amazon.DynamoDBStreams.Model;
using Amazon.Runtime;

using Excalibur.Data.CloudNative;
using Excalibur.Dispatch;
using Excalibur.Outbox.DynamoDb;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Real-DynamoDB (LocalStack) lock proving the outbox streams subscription reads the key attribute names
/// the consumer configured, and refuses a record it cannot identify instead of inventing an identity.
/// </summary>
/// <remarks>
/// <para>
/// The subscription used to read the key attributes by the hardcoded literals <c>"pk"</c> and <c>"sk"</c>
/// while the store it belongs to threaded <c>PartitionKeyAttribute</c> / <c>SortKeyAttribute</c> from
/// options. On a table whose key attributes carry any other name, every message arriving off the public
/// <c>SubscribeToNewMessagesAsync</c> was handed a fresh <c>Guid</c> as its message id. Deduplication and
/// inbox idempotency key on message id, so a synthetic one defeats them by construction: the same message
/// redelivered arrives under a different id and is processed again, with no exception and no log.
/// </para>
/// <para>
/// RED against the pre-fix shape: the renamed-attribute test observed
/// <c>MessageId</c> = a random <c>Guid</c> where the written id was expected.
/// </para>
/// <para>
/// NOT skip-gated. A Docker-unavailable run fails loudly rather than passing vacuously.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Outbox")]
[Trait("Database", "DynamoDb")]
public sealed class DynamoDbOutboxStreamsSubscriptionKeyAttributesShould
	: IClassFixture<DynamoDbOutboxStoreContainerFixture>, IAsyncLifetime
{
	private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(90);

	private static readonly ChangeFeedOptions FeedOptions = new()
	{
		StartPosition = ChangeFeedStartPosition.Beginning,
		MaxBatchSize = 100,
		PollingInterval = TimeSpan.FromMilliseconds(250)
	};

	private readonly DynamoDbOutboxStoreContainerFixture _fixture;
	private readonly List<(DynamoDbOutboxStore Store, string TableName)> _created = [];

	public DynamoDbOutboxStreamsSubscriptionKeyAttributesShould(DynamoDbOutboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	public ValueTask InitializeAsync() => ValueTask.CompletedTask;

	public async ValueTask DisposeAsync()
	{
		foreach (var (store, tableName) in _created)
		{
			await store.DisposeAsync().ConfigureAwait(false);
			await _fixture.DeleteTableAsync(tableName, CancellationToken.None).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// SAFETY — the defect. On a table whose key attributes are renamed away from the defaults, the message
	/// id off the change feed must be the id that was written, never a synthesised one.
	/// </summary>
	[Fact]
	public async Task EmitTheWrittenMessageId_WhenTheKeyAttributesAreRenamed()
	{
		var (store, options) = await CreateStoreAsync(
			partitionKeyAttribute: "messagePartition",
			sortKeyAttribute: "outboxMessageId").ConfigureAwait(false);

		var partitionKey = new PartitionKey($"pk-{Guid.NewGuid():N}");
		var message = CreateMessage(partitionKey.Value);

		// Written BEFORE subscribing. A DynamoDB stream shard is materialised by the first write, and
		// ReadChangesAsync enumerates shards once at the start of the enumeration -- subscribing to a
		// freshly-created, never-written table yields an empty shard set and the feed completes
		// immediately. TRIM_HORIZON (StartPosition.Beginning) reads the shard from its start, so a
		// record written first is still delivered.
		var added = await store.AddAsync(message, partitionKey, CancellationToken.None).ConfigureAwait(false);
		added.Success.ShouldBeTrue($"add must succeed: {added.ErrorMessage}");

		await using var subscription = await store
			.SubscribeToNewMessagesAsync(FeedOptions, CancellationToken.None).ConfigureAwait(false);

		var observedIds = new List<string>();
		var observed = await ReadFirstAsync(subscription, message.MessageId, observedIds).ConfigureAwait(false);

		observed.ShouldNotBeNull(
			$"the change feed must emit '{message.MessageId}' from table '{options.TableName}' within "
			+ $"{ReadTimeout}, and instead emitted: {Describe(observedIds)}. Before the fix the "
			+ "subscription read the key attributes by their DEFAULT names, missed them on this renamed "
			+ "table, and fabricated a fresh Guid as the message id for every record.");
		observed.Document.ShouldNotBeNull();
		observed.Document.MessageId.ShouldBe(
			message.MessageId,
			"the message id is the configured sort key attribute's value. A different value means the "
			+ "subscription read a hardcoded key name and synthesised an id, which silently defeats every "
			+ "downstream deduplication check.");
		observed.DocumentId.ShouldBe(message.MessageId);
		observed.Document.PartitionKeyValue.ShouldBe(
			partitionKey.Value,
			"the partition key value is what MarkAsPublishedAsync addresses the item by; an empty or "
			+ "guessed one targets a different item and the message is redelivered forever.");
		observed.PartitionKey.Value.ShouldBe(partitionKey.Value);
	}

	/// <summary>
	/// LIVENESS — "it does not fabricate" is also satisfied by a subscription that emits nothing, so the
	/// default-named case must still deliver the message end to end.
	/// </summary>
	[Fact]
	public async Task EmitTheWrittenMessageId_WhenTheKeyAttributesAreTheDefaults()
	{
		var (store, _) = await CreateStoreAsync(
			partitionKeyAttribute: "pk",
			sortKeyAttribute: "sk").ConfigureAwait(false);

		var partitionKey = new PartitionKey($"pk-{Guid.NewGuid():N}");
		var message = CreateMessage(partitionKey.Value);

		// Written BEFORE subscribing. A DynamoDB stream shard is materialised by the first write, and
		// ReadChangesAsync enumerates shards once at the start of the enumeration -- subscribing to a
		// freshly-created, never-written table yields an empty shard set and the feed completes
		// immediately. TRIM_HORIZON (StartPosition.Beginning) reads the shard from its start, so a
		// record written first is still delivered.
		var added = await store.AddAsync(message, partitionKey, CancellationToken.None).ConfigureAwait(false);
		added.Success.ShouldBeTrue($"add must succeed: {added.ErrorMessage}");

		await using var subscription = await store
			.SubscribeToNewMessagesAsync(FeedOptions, CancellationToken.None).ConfigureAwait(false);

		var observedIds = new List<string>();
		var observed = await ReadFirstAsync(subscription, message.MessageId, observedIds).ConfigureAwait(false);

		observed.ShouldNotBeNull(
			$"the default-named table must still deliver '{message.MessageId}' within {ReadTimeout}, and "
			+ $"instead emitted: {Describe(observedIds)}");
		observed.Document.ShouldNotBeNull();
		observed.Document.MessageId.ShouldBe(message.MessageId);
		observed.Document.PartitionKeyValue.ShouldBe(partitionKey.Value);
	}

	/// <summary>
	/// FAIL-CLOSED — a record that carries no value for the configured sort key cannot be identified, and
	/// must surface as an error rather than as a unique, meaningless, perfectly valid-looking id.
	/// </summary>
	[Fact]
	public async Task ThrowRatherThanFabricateAnId_WhenTheConfiguredSortKeyIsAbsentFromTheRecord()
	{
		// The table's real sort key is "sk"; the subscription is pointed at a name the records do not carry,
		// which is exactly the shape a consumer produces by renaming one option and not the other.
		var (store, options) = await CreateStoreAsync(
			partitionKeyAttribute: "pk",
			sortKeyAttribute: "sk").ConfigureAwait(false);

		var misconfigured = Clone(options);
		misconfigured.SortKeyAttribute = "notTheSortKey";

		var credentials = new BasicAWSCredentials("test", "test");
		using var streamsClient = new AmazonDynamoDBStreamsClient(
			credentials,
			new AmazonDynamoDBStreamsConfig { ServiceURL = _fixture.ServiceUrl });

		await using var subscription = new DynamoDbOutboxStreamsSubscription(
			_fixture.Client,
			streamsClient,
			misconfigured,
			FeedOptions,
			NullLogger.Instance);

		var partitionKey = new PartitionKey($"pk-{Guid.NewGuid():N}");
		var message = CreateMessage(partitionKey.Value);
		var added = await store.AddAsync(message, partitionKey, CancellationToken.None).ConfigureAwait(false);
		added.Success.ShouldBeTrue($"add must succeed: {added.ErrorMessage}");

		await subscription.StartAsync(CancellationToken.None).ConfigureAwait(false);

		var failure = await Should.ThrowAsync<InvalidOperationException>(
			() => DrainAsync(subscription)).ConfigureAwait(false);

		failure.Message.ShouldContain(
			misconfigured.SortKeyAttribute,
			Case.Sensitive,
			"the diagnostic must name the attribute that was expected, so the consumer can see which "
			+ "option is wrong.");
		failure.Message.ShouldContain(
			"sk",
			Case.Sensitive,
			"the diagnostic must also name the attributes the record actually carries.");
	}

	/// <summary>
	/// Reads change events until the one carrying <paramref name="messageId"/> arrives, or the timeout
	/// elapses. Other events on the same shard (from a parallel test) are skipped rather than asserted on.
	/// </summary>
	private static async Task<IChangeFeedEvent<CloudOutboxMessage>?> ReadFirstAsync(
		IChangeFeedSubscription<CloudOutboxMessage> subscription,
		string messageId,
		ICollection<string> observedIds)
	{
		using var cts = new CancellationTokenSource(ReadTimeout);

		try
		{
			await foreach (var change in subscription.ReadChangesAsync(cts.Token).ConfigureAwait(false))
			{
				observedIds.Add(change.Document?.MessageId ?? "(null)");

				if (string.Equals(change.Document?.MessageId, messageId, StringComparison.Ordinal))
				{
					return change;
				}
			}
		}
		catch (OperationCanceledException)
		{
			// Timed out waiting for the record — reported by the null return, with the assertion's message.
		}

		return null;
	}

	/// <summary>
	/// Renders the ids the feed emitted, so a failure says WHAT arrived rather than only that the
	/// expected id did not. A list of unrelated GUIDs is the signature of the fabricated-id defect.
	/// </summary>
	private static string Describe(IReadOnlyCollection<string> observedIds) =>
		observedIds.Count == 0 ? "(nothing)" : string.Join(", ", observedIds);

	/// <summary>
	/// Enumerates the feed until it faults or the timeout elapses, so a throw on the read path surfaces to
	/// the caller instead of being swallowed by an early break.
	/// </summary>
	private static async Task DrainAsync(IChangeFeedSubscription<CloudOutboxMessage> subscription)
	{
		using var cts = new CancellationTokenSource(ReadTimeout);

		await foreach (var _ in subscription.ReadChangesAsync(cts.Token).ConfigureAwait(false))
		{
			// The records are the point of failure, not the values — the enumeration is expected to throw.
		}
	}

	private static CloudOutboxMessage CreateMessage(string partitionKeyValue) =>
		new()
		{
			MessageId = $"msg-{Guid.NewGuid():N}",
			MessageType = "TestMessageType",
			Payload = "test-payload"u8.ToArray(),
			CreatedAt = DateTimeOffset.UtcNow,
			PartitionKeyValue = partitionKeyValue
		};

	private static DynamoDbOutboxOptions Clone(DynamoDbOutboxOptions source) =>
		new()
		{
			TableName = source.TableName,
			PartitionKeyAttribute = source.PartitionKeyAttribute,
			SortKeyAttribute = source.SortKeyAttribute,
			TtlAttribute = source.TtlAttribute,
			CreatedAtIndexName = source.CreatedAtIndexName,
			CreateTableIfNotExists = false,
			EnableStreams = true,
			Connection = source.Connection
		};

	private async Task<(DynamoDbOutboxStore Store, DynamoDbOutboxOptions Options)> CreateStoreAsync(
		string partitionKeyAttribute,
		string sortKeyAttribute)
	{
		_fixture.DockerAvailable.ShouldBeTrue("LocalStack DynamoDB must be available — never skipped.");

		var table = $"outbox_{Guid.NewGuid():N}";
		var opts = new DynamoDbOutboxOptions
		{
			TableName = table,
			PartitionKeyAttribute = partitionKeyAttribute,
			SortKeyAttribute = sortKeyAttribute,
			CreateTableIfNotExists = true,
			EnableStreams = true,
			Connection = new DynamoDbOutboxConnectionOptions
			{
				ServiceUrl = _fixture.ServiceUrl,
				AccessKey = "test",
				SecretKey = "test"
			}
		};

		var store = new DynamoDbOutboxStore(Options.Create(opts), NullLogger<DynamoDbOutboxStore>.Instance);
		await store.InitializeAsync(CancellationToken.None).ConfigureAwait(false);

		_created.Add((store, table));

		await WaitForStreamEnabledAsync(table).ConfigureAwait(false);

		return (store, opts);
	}

	/// <summary>
	/// Waits until the table's stream reports <c>ENABLED</c>.
	/// </summary>
	/// <remarks>
	/// The store's <c>InitializeAsync</c> waits for the TABLE to reach <c>ACTIVE</c>, which on real
	/// DynamoDB is when the stream is usable too. LocalStack reaches <c>ACTIVE</c> while the stream is
	/// still <c>ENABLING</c>, and a write during that window never appears on the stream at all -- so
	/// without this wait the feed is legitimately empty and every assertion below would be measuring the
	/// harness rather than the subscription.
	/// </remarks>
	private async Task WaitForStreamEnabledAsync(string tableName)
	{
		var credentials = new BasicAWSCredentials("test", "test");
		using var streamsClient = new AmazonDynamoDBStreamsClient(
			credentials,
			new AmazonDynamoDBStreamsConfig { ServiceURL = _fixture.ServiceUrl });

		using var cts = new CancellationTokenSource(ReadTimeout);

		while (!cts.IsCancellationRequested)
		{
			var table = await _fixture.Client.DescribeTableAsync(tableName, cts.Token).ConfigureAwait(false);
			var arn = table.Table.LatestStreamArn;

			if (!string.IsNullOrEmpty(arn))
			{
				var stream = await streamsClient.DescribeStreamAsync(
					new DescribeStreamRequest { StreamArn = arn }, cts.Token).ConfigureAwait(false);

				if (stream.StreamDescription.StreamStatus == StreamStatus.ENABLED)
				{
					return;
				}
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cts.Token).ConfigureAwait(false);
		}

		throw new InvalidOperationException(
			$"The stream on table '{tableName}' did not reach ENABLED within {ReadTimeout}.");
	}
}
