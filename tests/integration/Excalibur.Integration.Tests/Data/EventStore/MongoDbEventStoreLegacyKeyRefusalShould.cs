// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.MongoDB;

using MongoDB.Bson;
using MongoDB.Driver;

using Tests.Shared.Conformance.EventStore;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Binds the refusal that stands between an unmigrated MongoDB collection and silent history duplication:
/// a document written under the pre-tenant stream identifier is unaddressable now, and a load of the
/// aggregate it belongs to would come back EMPTY - which the caller reads as a new aggregate, appends to at
/// version 0, and thereby splits one identity across two disjoint histories while the collection still
/// holds the first.
/// </summary>
/// <remarks>
/// Two arms, and the second is what makes the first mean anything: a probe that refused unconditionally
/// would satisfy the safety arm on its own. The liveness arm seeds a correctly-keyed document and requires
/// an absent aggregate to load as an empty stream and then accept a write. That reaches the probe rather
/// than bypassing it: an empty load is exactly what triggers it, so the arm proves the probe comes back
/// clean, not merely that it never ran.
/// </remarks>
[Collection(MongoDbEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "EventStore")]
[Trait("Database", "MongoDb")]
public sealed class MongoDbEventStoreLegacyKeyRefusalShould
	: IClassFixture<MongoDbEventStoreContainerFixture>, IAsyncLifetime
{
	private const string AggregateType = "LegacyKeyAggregate";

	private readonly MongoDbEventStoreContainerFixture _fixture;

	// One collection per test instance. xUnit builds a fresh instance per arm, so neither arm can observe
	// what the other seeded.
	private readonly string _collectionName = $"legacy_key_probe_{Guid.NewGuid():N}";

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbEventStoreLegacyKeyRefusalShould"/> class.
	/// </summary>
	/// <param name="fixture">The MongoDB container fixture.</param>
	public MongoDbEventStoreLegacyKeyRefusalShould(MongoDbEventStoreContainerFixture fixture) => _fixture = fixture;

	/// <inheritdoc />
	public ValueTask InitializeAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"MongoDB must be available - this arm exists to prove a real collection is refused, so it is never skipped.");

		return ValueTask.CompletedTask;
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync() =>
		await new MongoClient(_fixture.ConnectionString)
			.GetDatabase(_fixture.DatabaseName)
			.DropCollectionAsync(_collectionName)
			.ConfigureAwait(false);

	/// <summary>
	/// SAFETY: a collection still holding a document written without a tenant segment is refused, by name,
	/// before it can be read back as an empty stream.
	/// </summary>
	[Fact]
	public async Task Refuse_a_collection_holding_a_document_written_without_a_tenant_segment()
	{
		// The shape an earlier release wrote on this provider: the aggregate identifier alone.
		const string LegacyStreamId = "agg-1";
		await SeedDocumentAsync(LegacyStreamId);

		await using var provider = BuildProvider();
		var store = provider.GetRequiredService<IEventStore>();

		var thrown = await Should.ThrowAsync<InvalidOperationException>(
			async () => await store.LoadAsync("agg-1", AggregateType, CancellationToken.None));

		thrown.Message.ShouldContain(
			_collectionName,
			Case.Sensitive,
			"the refusal must name the collection a consumer has to re-key, or it cannot be acted on");

		thrown.Message.ShouldContain(
			LegacyStreamId,
			Case.Sensitive,
			"naming the offending identifier is what lets a consumer confirm which documents are affected");

		// The refusal is a refusal, not a repair: the document is still exactly where it was.
		(await CountDocumentsAsync().ConfigureAwait(false)).ShouldBe(
			1L,
			"the probe must modify nothing - re-keying is a decision about the deployment, not about the data");
	}

	/// <summary>
	/// LIVENESS: a collection whose documents all carry a tenant segment is served normally. Without
	/// this arm a probe that always refused would look correct.
	/// </summary>
	[Fact]
	public async Task Serve_a_collection_whose_documents_all_carry_a_tenant_segment()
	{
		await SeedDocumentAsync("t:tenant-a:agg-1");

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

		appended.Success.ShouldBeTrue("a correctly-keyed collection must remain fully writable");
	}

	private ServiceProvider BuildProvider()
	{
		var services = new ServiceCollection();

		_ = services.AddExcalibur(x => x.AddEventSourcing(es => es.UseMongoDB(mongo =>
			_ = mongo
				.ConnectionString(_fixture.ConnectionString)
				.DatabaseName(_fixture.DatabaseName)
				.CollectionName(_collectionName))));

		return services.BuildServiceProvider();
	}

	private IMongoCollection<BsonDocument> Collection() =>
		new MongoClient(_fixture.ConnectionString)
			.GetDatabase(_fixture.DatabaseName)
			.GetCollection<BsonDocument>(_collectionName);

	// Seeded through the raw driver rather than through the store, because the store can no longer write
	// the shape under test - that is the whole point of the change this locks.
	private async Task SeedDocumentAsync(string streamId) =>
		await Collection().InsertOneAsync(new BsonDocument
		{
			["eventId"] = Guid.NewGuid().ToString(),
			["streamId"] = streamId,
			["aggregateId"] = "agg-1",
			["aggregateType"] = AggregateType,
			["eventType"] = "SeededEvent",
			["version"] = 0L,
			["globalSequence"] = 1L,
			["timestamp"] = DateTime.UtcNow
		}).ConfigureAwait(false);

	private async Task<long> CountDocumentsAsync() =>
		await Collection().CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty).ConfigureAwait(false);
}
