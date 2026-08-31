// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.DynamoDb;

using Tests.Shared.Conformance.EventStore;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Binds the refusal that stands between an unmigrated DynamoDB table and silent history duplication: an
/// item written under the pre-tenant partition key is unaddressable now, and a load of the aggregate it
/// belongs to would come back EMPTY - which the caller reads as a new aggregate, appends to at version 0,
/// and thereby splits one identity across two disjoint histories while the table still holds the first.
/// </summary>
/// <remarks>
/// Two arms, and the second is what makes the first mean anything: a probe that refused unconditionally
/// would satisfy the safety arm on its own. The liveness arm seeds a correctly-keyed item and requires
/// an absent aggregate to load as an empty stream and then accept a write. That reaches the probe rather
/// than bypassing it: an empty load is exactly what triggers it, so the arm proves the probe comes back
/// clean, not merely that it never ran.
/// </remarks>
[Collection(DynamoDbEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "EventStore")]
[Trait("Database", "DynamoDb")]
public sealed class DynamoDbEventStoreLegacyKeyRefusalShould
	: IClassFixture<DynamoDbEventStoreContainerFixture>, IAsyncLifetime
{
	private const string AggregateType = "LegacyKeyAggregate";

	private readonly DynamoDbEventStoreContainerFixture _fixture;

	// One table per test instance. xUnit builds a fresh instance per arm, so neither arm can observe what
	// the other seeded.
	private readonly string _tableName = $"legacy_key_probe_{Guid.NewGuid():N}";

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamoDbEventStoreLegacyKeyRefusalShould"/> class.
	/// </summary>
	/// <param name="fixture">The LocalStack DynamoDB fixture.</param>
	public DynamoDbEventStoreLegacyKeyRefusalShould(DynamoDbEventStoreContainerFixture fixture) => _fixture = fixture;

	/// <inheritdoc />
	public ValueTask InitializeAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"LocalStack DynamoDB must be available - this arm exists to prove a real table is refused, so it "
			+ $"is never skipped: {_fixture.InitializationError}");

		return ValueTask.CompletedTask;
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync() =>
		await _fixture.DeleteTableAsync(_tableName, CancellationToken.None).ConfigureAwait(false);

	/// <summary>
	/// SAFETY: a table still holding an item written without a tenant segment is refused, by name, before it
	/// can be read back as an empty stream.
	/// </summary>
	[Fact]
	public async Task Refuse_a_table_holding_an_item_written_without_a_tenant_segment()
	{
		// The store provisions the table, so a first pass creates it. That pass is also the empty-table
		// case: a brand-new deployment must not be refused.
		await ProvisionTableAsync().ConfigureAwait(false);

		// The shape an earlier release wrote on this provider.
		const string LegacyPartitionKey = $"{AggregateType}:agg-1";
		await SeedItemAsync(LegacyPartitionKey).ConfigureAwait(false);

		await using var provider = BuildProvider();
		var store = provider.GetRequiredService<IEventStore>();

		var thrown = await Should.ThrowAsync<InvalidOperationException>(
			async () => await store.LoadAsync("agg-1", AggregateType, CancellationToken.None));

		thrown.Message.ShouldContain(
			_tableName,
			Case.Sensitive,
			"the refusal must name the table a consumer has to re-key, or it cannot be acted on");

		thrown.Message.ShouldContain(
			LegacyPartitionKey,
			Case.Sensitive,
			"naming the offending key is what lets a consumer confirm which items are affected");

		// The refusal is a refusal, not a repair: the item is still exactly where it was.
		(await ScanItemCountAsync().ConfigureAwait(false)).ShouldBe(
			1,
			"the probe must modify nothing - re-keying is a decision about the deployment, not about the data");
	}

	/// <summary>
	/// LIVENESS: a table whose items all carry a tenant segment is served normally. Without this arm a
	/// probe that always refused would look correct.
	/// </summary>
	[Fact]
	public async Task Serve_a_table_whose_items_all_carry_a_tenant_segment()
	{
		await ProvisionTableAsync().ConfigureAwait(false);
		await SeedItemAsync($"t:tenant-a:{AggregateType}:agg-1").ConfigureAwait(false);

		await using var provider = BuildProvider();
		var store = provider.GetRequiredService<IEventStore>();

		var loaded = await store.LoadAsync("agg-2", AggregateType, CancellationToken.None);
		loaded.ShouldBeEmpty("an aggregate that was never written must load as an empty stream, not refuse");

		// Resolution and an empty read prove less than a write does: the store must actually be usable.
		var appended = await store.AppendAsync(
			"agg-2",
			AggregateType,
			[new TestDomainEvent { AggregateId = "agg-2", OccurredAt = DateTimeOffset.UtcNow, Data = "first" }],
			expectedVersion: -1,
			CancellationToken.None);

		appended.Success.ShouldBeTrue("a correctly-keyed table must remain fully writable");
	}

	// Each provider gets its own client rather than the fixture's shared one: a container disposes what it
	// resolves, and the fixture's client has to outlive every arm.
	private ServiceProvider BuildProvider()
	{
		var serviceUrl = _fixture.ServiceUrl;
		var services = new ServiceCollection();

		_ = services.AddExcalibur(x => x.AddEventSourcing(es => es.UseDynamoDb(dynamo =>
			_ = dynamo
				.Client(new AmazonDynamoDBClient(
					new BasicAWSCredentials("test", "test"),
					new AmazonDynamoDBConfig { ServiceURL = serviceUrl }))
				.TableName(_tableName))));

		return services.BuildServiceProvider();
	}

	/// <summary>
	/// Creates the table through the store itself, which is the only thing in the system that knows the key
	/// schema. The load it performs is the empty-table arm of the probe: it must not refuse.
	/// </summary>
	private async Task ProvisionTableAsync()
	{
		await using var provider = BuildProvider();
		var store = provider.GetRequiredService<IEventStore>();

		var loaded = await store.LoadAsync("agg-0", AggregateType, CancellationToken.None);
		loaded.ShouldBeEmpty("a newly provisioned, empty table holds nothing to refuse");
	}

	// Seeded through the raw client rather than through the store, because the store can no longer write
	// the shape under test - that is the whole point of the change this locks.
	private async Task SeedItemAsync(string partitionKey) =>
		_ = await _fixture.Client.PutItemAsync(
			_tableName,
			new Dictionary<string, AttributeValue>
			{
				["pk"] = new AttributeValue { S = partitionKey },
				["sk"] = new AttributeValue { N = "0" },
				["eventId"] = new AttributeValue { S = Guid.NewGuid().ToString() },
				["aggregateId"] = new AttributeValue { S = "agg-1" },
				["aggregateType"] = new AttributeValue { S = AggregateType }
			},
			CancellationToken.None).ConfigureAwait(false);

	private async Task<int> ScanItemCountAsync()
	{
		var response = await _fixture.Client.ScanAsync(
			new ScanRequest { TableName = _tableName },
			CancellationToken.None).ConfigureAwait(false);

		return response.Items?.Count ?? 0;
	}
}
