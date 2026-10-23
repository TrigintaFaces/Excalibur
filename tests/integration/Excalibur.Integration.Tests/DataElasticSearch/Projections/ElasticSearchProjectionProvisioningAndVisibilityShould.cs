// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;

using Excalibur.Data.ElasticSearch.Projections;
using Excalibur.EventSourcing;
using Excalibur.Integration.Tests.DataElasticSearch.Infrastructure.TestBaseClasses;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Integration.Tests.DataElasticSearch.Projections;

[Trait("Category", "Integration")]
[Trait("Database", "Elasticsearch")]
[Trait("Component", "Projections")]
[Collection(nameof(ElasticsearchHostTests))]
public sealed class ElasticSearchProjectionProvisioningAndVisibilityShould(ElasticsearchContainerFixture fixture)
	: ElasticsearchIntegrationTestBase(fixture)
{
	[Fact]
	public async Task RefuseAnAbsentManuallyProvisionedIndexBeforeAnyDocumentWrite()
	{
		var options = Options(create: false);
		var indexName = IndexName(options);
		await using var store = Store(options);
		await Should.ThrowAsync<InvalidOperationException>(
			() => store.UpsertAsync("one", new Projection { Id = "one", Status = "Awaiting Payment" }, CancellationToken.None));
		(await Client.Indices.ExistsAsync(indexName).ConfigureAwait(false)).Exists.ShouldBeFalse();
	}

	[Fact]
	public async Task RefuseAnExistingDynamicallyInferredStringMapping()
	{
		var options = Options(create: false);
		var indexName = IndexName(options);
		var indexed = await Client.IndexAsync(new Projection { Id = "seed", Status = "Awaiting Payment" },
			r => r.Index(indexName).Id("seed")).ConfigureAwait(false);
		indexed.IsValidResponse.ShouldBeTrue();
		await using var store = Store(options);
		await Should.ThrowAsync<InvalidOperationException>(
			() => store.UpsertAtPositionAsync("one", new Projection { Id = "one" }, null, 1, CancellationToken.None));
		(await Client.GetAsync<Projection>(new GetRequest(indexName, "one")).ConfigureAwait(false)).Found.ShouldBeFalse();
	}

	[Fact]
	public async Task AcceptACompatibleManuallyProvisionedMappingWithExtraFields()
	{
		var options = Options(create: false);
		var indexName = IndexName(options);
		await CreateIndexAsync(indexName, new LongNumberProperty()).ConfigureAwait(false);
		await using var store = Store(options);
		var result = await store.UpsertAtPositionAsync("one", new Projection { Id = "one", Status = "Ready" },
			null, 1, CancellationToken.None).ConfigureAwait(false);
		result.Outcome.ShouldBe(ProjectionAdvanceOutcome.Applied);
		(await store.GetByIdAsync("one", CancellationToken.None).ConfigureAwait(false))!.Status.ShouldBe("Ready");
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task RefuseAnIncorrectReservedPositionMappingRegardlessOfProvisioningMode(bool create)
	{
		var options = Options(create);
		await CreateIndexAsync(IndexName(options), new KeywordProperty()).ConfigureAwait(false);
		await using var store = Store(options);
		await Should.ThrowAsync<InvalidOperationException>(
			() => store.UpsertAtPositionAsync("one", new Projection { Id = "one" }, null, 1, CancellationToken.None));
	}

	[Fact]
	public async Task MakeOptedInCreateAndConditionalUpdateSearchableWithoutAnExplicitRefresh()
	{
		var options = Options(create: true);
		options.Index.WaitForSearchVisibilityOnWrite = true;
		options.Index.RefreshInterval = "100ms";
		IndexName(options);
		await using var store = Store(options);
		using var bound = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		(await store.UpsertAtPositionAsync("one", new Projection { Id = "one", Status = "Pending" },
			null, 1, bound.Token).ConfigureAwait(false)).Outcome.ShouldBe(ProjectionAdvanceOutcome.Applied);
		var created = await store.QueryAsync(new Dictionary<string, object> { ["status"] = "Pending" },
			null, bound.Token).ConfigureAwait(false);
		created.Count.ShouldBe(1);
		(await store.UpsertAtPositionAsync("one", new Projection { Id = "one", Status = "Paid" },
			1, 2, bound.Token).ConfigureAwait(false)).Outcome.ShouldBe(ProjectionAdvanceOutcome.Applied);
		var updated = await store.QueryAsync(new Dictionary<string, object> { ["status"] = "Paid" },
			null, bound.Token).ConfigureAwait(false);
		updated.Count.ShouldBe(1);
		updated[0].Id.ShouldBe("one");
	}

	[Fact]
	public async Task KeepRealtimeGetIndependentOfSearchRefreshByDefault()
	{
		var options = Options(create: true);
		options.Index.RefreshInterval = "-1";
		IndexName(options);
		await using var store = Store(options);
		await store.UpsertAtPositionAsync("one", new Projection { Id = "one", Status = "Pending" },
			null, 1, CancellationToken.None).ConfigureAwait(false);
		(await store.GetByIdAsync("one", CancellationToken.None).ConfigureAwait(false)).ShouldNotBeNull();
		(await store.QueryAsync(null, null, CancellationToken.None).ConfigureAwait(false)).Count.ShouldBe(0);
	}

	[Fact]
	public async Task RefreshObservedCompetingWritesEvenWhenThisStoreDoesNotWrite()
	{
		var writerOptions = Options(create: true);
		writerOptions.Index.RefreshInterval = "-1";
		IndexName(writerOptions);
		await using var writer = Store(writerOptions);
		await writer.UpsertAtPositionAsync("one", new Projection { Id = "one", Status = "Pending" },
			null, 1, CancellationToken.None).ConfigureAwait(false);
		// An independent cooperative driver observes the indexed state via realtime GET. It has
		// no write to wait_for: its fold would skip this event as already applied.
		var observerOptions = Options(create: false);
		observerOptions.Index.IndexPrefix = writerOptions.Index.IndexPrefix;
		observerOptions.Index.WaitForSearchVisibilityOnWrite = true;
		await using var observer = Store(observerOptions);
		(await observer.GetWithPositionAsync("one", CancellationToken.None).ConfigureAwait(false)).Position.Value.ShouldBe(1);
		(await observer.QueryAsync(null, null, CancellationToken.None).ConfigureAwait(false)).Count.ShouldBe(0);
		var barrier = ((IServiceProvider)observer).GetService(typeof(IProjectionVisibilityBarrier))
			.ShouldBeAssignableTo<IProjectionVisibilityBarrier>();
		await barrier.EnsureVisibleAsync(CancellationToken.None).ConfigureAwait(false);
		var visible = await observer.QueryAsync(new Dictionary<string, object> { ["status"] = "Pending" },
			null, CancellationToken.None).ConfigureAwait(false);
		visible.Count.ShouldBe(1);
		visible[0].Id.ShouldBe("one");
	}

	[Fact]
	public async Task CheckpointVisibilityDoesNotWaitForEveryIndividualWrite()
	{
		var options = Options(create: true);
		options.Index.RefreshInterval = "-1";
		options.Index.EnsureSearchVisibilityOnProjectionCheckpoint = true;
		IndexName(options);
		await using var store = Store(options);
		using var bound = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		// With auto-refresh disabled, accidental per-document wait_for never completes. These
		// writes must finish without a refresh; only the explicit page barrier makes them searchable.
		for (var position = 1; position <= 3; position++)
		{
			var id = $"checkpoint-{position}";
			(await store.UpsertAtPositionAsync(id, new Projection { Id = id, Status = "Pending" },
				null, position, bound.Token).ConfigureAwait(false)).Outcome.ShouldBe(ProjectionAdvanceOutcome.Applied);
		}
		(await store.QueryAsync(null, null, bound.Token).ConfigureAwait(false)).Count.ShouldBe(0);
		var barrier = (IProjectionVisibilityBarrier)((IServiceProvider)store)
			.GetService(typeof(IProjectionVisibilityBarrier))!;
		await barrier.EnsureVisibleAsync(bound.Token).ConfigureAwait(false);
		(await store.QueryAsync(null, null, bound.Token).ConfigureAwait(false)).Count.ShouldBe(3);
	}

	private ElasticSearchProjectionStoreOptions Options(bool create) => new()
	{
		NodeUri = ConnectionString,
		Index = { IndexPrefix = $"provision-visibility-{Guid.NewGuid():N}", CreateIndexOnInitialize = create },
	};

	private string IndexName(ElasticSearchProjectionStoreOptions options)
	{
		var name = ElasticSearchProjectionIndexConvention.GetIndexName<Projection>(options);
		CreatedIndices.Add(name);
		return name;
	}

	private ElasticSearchProjectionStore<Projection> Store(ElasticSearchProjectionStoreOptions options)
	{
		var monitor = A.Fake<IOptionsMonitor<ElasticSearchProjectionStoreOptions>>();
		A.CallTo(() => monitor.Get(A<string>._)).Returns(options);
		return new ElasticSearchProjectionStore<Projection>(Client, monitor,
			NullLogger<ElasticSearchProjectionStore<Projection>>.Instance);
	}

	private async Task CreateIndexAsync(string name, IProperty position)
	{
		var result = await Client.Indices.CreateAsync(new CreateIndexRequest(name)
		{
			Mappings = new TypeMapping
			{
				Properties = new Properties
				{
					{ "id", new KeywordProperty() },
					{ "status", new KeywordProperty() },
					{ "extra", new TextProperty() },
					{ "excaliburProjectionPosition", position },
				},
			},
		}).ConfigureAwait(false);
		result.IsValidResponse.ShouldBeTrue();
	}

	private sealed class Projection
	{
		public string Id { get; init; } = string.Empty;
		public string Status { get; init; } = string.Empty;
	}
}
