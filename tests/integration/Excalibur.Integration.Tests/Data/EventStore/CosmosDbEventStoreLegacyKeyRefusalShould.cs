// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.CosmosDb;

using Tests.Shared.Conformance.EventStore;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Binds the refusal that stands between an unmigrated Cosmos DB container and silent history duplication:
/// a document written under the pre-tenant stream identifier is unaddressable now, and a load of the
/// aggregate it belongs to would come back EMPTY - which the caller reads as a new aggregate, appends to at
/// version 0, and thereby splits one identity across two disjoint histories while the container still holds
/// the first.
/// </summary>
/// <remarks>
/// Two arms, and the second is what makes the first mean anything: a probe that refused unconditionally
/// would satisfy the safety arm on its own. The liveness arm seeds a correctly-keyed document and requires
/// an absent aggregate to load as an empty stream and then accept a write. That reaches the probe rather
/// than bypassing it: an empty load is exactly what triggers it, so the arm proves the probe comes back
/// clean, not merely that it never ran.
/// </remarks>
[Collection(CosmosDbEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "EventStore")]
[Trait("Infrastructure", "CosmosEmulator")]
[Trait("Database", "CosmosDb")]
public sealed class CosmosDbEventStoreLegacyKeyRefusalShould
	: IClassFixture<CosmosDbEventStoreContainerFixture>, IAsyncLifetime
{
	private const string AggregateType = "LegacyKeyAggregate";

	private readonly CosmosDbEventStoreContainerFixture _fixture;

	// One container per test instance. xUnit builds a fresh instance per arm, so neither arm can observe
	// what the other seeded.
	private readonly string _containerName = $"legacy_key_probe_{Guid.NewGuid():N}";

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbEventStoreLegacyKeyRefusalShould"/> class.
	/// </summary>
	/// <param name="fixture">The Cosmos DB emulator fixture.</param>
	public CosmosDbEventStoreLegacyKeyRefusalShould(CosmosDbEventStoreContainerFixture fixture) => _fixture = fixture;

	/// <inheritdoc />
	public ValueTask InitializeAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"The Cosmos DB emulator must be available - this arm exists to prove a real container is refused, "
			+ "so it is never skipped.");

		return ValueTask.CompletedTask;
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync() =>
		await _fixture.DeleteContainerAsync(_containerName).ConfigureAwait(false);

	/// <summary>
	/// SAFETY: a container still holding a document written without a tenant segment is refused, by name,
	/// before it can be read back as an empty stream.
	/// </summary>
	[Fact]
	public async Task Refuse_a_container_holding_a_document_written_without_a_tenant_segment()
	{
		// The store provisions the container, so a first pass creates it. That pass is also the empty-
		// container case: a brand-new deployment must not be refused.
		await ProvisionContainerAsync().ConfigureAwait(false);

		// The shape an earlier release wrote on this provider.
		const string LegacyStreamId = $"{AggregateType}:agg-1";
		await SeedDocumentAsync(LegacyStreamId).ConfigureAwait(false);

		await using var provider = BuildProvider();
		var store = provider.GetRequiredService<IEventStore>();

		var thrown = await Should.ThrowAsync<InvalidOperationException>(
			async () => await store.LoadAsync("agg-1", AggregateType, CancellationToken.None));

		thrown.Message.ShouldContain(
			_containerName,
			Case.Sensitive,
			"the refusal must name the container a consumer has to re-key, or it cannot be acted on");

		thrown.Message.ShouldContain(
			LegacyStreamId,
			Case.Sensitive,
			"naming the offending identifier is what lets a consumer confirm which documents are affected");

		// The refusal is a refusal, not a repair: the document is still exactly where it was.
		(await CountDocumentsAsync().ConfigureAwait(false)).ShouldBe(
			1,
			"the probe must modify nothing - re-keying is a decision about the deployment, not about the data");
	}

	/// <summary>
	/// LIVENESS: a container whose documents all carry a tenant segment is served normally. Without this arm a
	/// probe that always refused would look correct.
	/// </summary>
	[Fact]
	public async Task Serve_a_container_whose_documents_all_carry_a_tenant_segment()
	{
		await ProvisionContainerAsync().ConfigureAwait(false);
		await SeedDocumentAsync($"t:tenant-a:{AggregateType}:agg-1").ConfigureAwait(false);

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

		appended.Success.ShouldBeTrue("a correctly-keyed container must remain fully writable");
	}

	private ServiceProvider BuildProvider()
	{
		var services = new ServiceCollection();

		_ = services.AddExcalibur(x => x.AddEventSourcing(es => es.UseCosmosDb(cosmos =>
			_ = cosmos
				.Client(_fixture.Client)
				.DatabaseName(_fixture.DatabaseName)
				.ContainerName(_containerName))));

		return services.BuildServiceProvider();
	}

	/// <summary>
	/// Creates the container through the store itself, which is the only thing that knows the partition-key
	/// path it expects. The load it performs is the empty-container arm of the probe: it must not refuse.
	/// </summary>
	private async Task ProvisionContainerAsync()
	{
		await using var provider = BuildProvider();
		var store = provider.GetRequiredService<IEventStore>();

		var loaded = await store.LoadAsync("agg-0", AggregateType, CancellationToken.None);
		loaded.ShouldBeEmpty("a newly provisioned, empty container holds nothing to refuse");
	}

	// Seeded through the raw client rather than through the store, because the store can no longer write
	// the shape under test - that is the whole point of the change this locks.
	private async Task SeedDocumentAsync(string streamId) =>
		_ = await _fixture.Client
			.GetContainer(_fixture.DatabaseName, _containerName)
			.CreateItemAsync(
				new
				{
					id = $"{streamId}:0",
					streamId,
					eventId = Guid.NewGuid().ToString(),
					aggregateId = "agg-1",
					aggregateType = AggregateType,
					version = 0
				},
				new Microsoft.Azure.Cosmos.PartitionKey(streamId),
				cancellationToken: CancellationToken.None).ConfigureAwait(false);

	private async Task<int> CountDocumentsAsync()
	{
		var container = _fixture.Client.GetContainer(_fixture.DatabaseName, _containerName);

		using var iterator = container.GetItemQueryIterator<int>(
			new Microsoft.Azure.Cosmos.QueryDefinition("SELECT VALUE COUNT(1) FROM c"));

		var total = 0;

		while (iterator.HasMoreResults)
		{
			foreach (var count in await iterator.ReadNextAsync(CancellationToken.None).ConfigureAwait(false))
			{
				total += count;
			}
		}

		return total;
	}
}
