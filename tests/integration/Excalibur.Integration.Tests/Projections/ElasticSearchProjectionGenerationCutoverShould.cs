// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Elastic.Clients.Elasticsearch;

using Excalibur.Data.ElasticSearch.IndexManagement;
using Excalibur.EventSourcing;

using Microsoft.Extensions.Logging.Abstractions;

using Excalibur.Data.ElasticSearch.Projections;

using Microsoft.Extensions.Options;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Integration.Tests.Projections;

/// <summary>
/// The lag-gated generation cutover over a real Elasticsearch alias.
/// </summary>
/// <remarks>
/// The alias swap is the provider's half of the seam and is decided by the engine: Elasticsearch applies
/// a multi-action alias update atomically, and only the engine can say which index an alias resolves to
/// afterwards. A faked alias manager would answer whatever it was told and would satisfy every arm here.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Database", "Elasticsearch")]
[Trait("Component", "Projections")]
[Collection(nameof(DataElasticSearch.Infrastructure.TestBaseClasses.ElasticsearchHostTests))]
public sealed class ElasticSearchProjectionGenerationCutoverShould : ProjectionGenerationCutoverConformance
{
	private readonly ElasticsearchContainerFixture _fixture;

	public ElasticSearchProjectionGenerationCutoverShould(ElasticsearchContainerFixture fixture) =>
		_fixture = fixture;

    protected override string DestinationPrefix => "elasticsearch/";

	protected override string CheckpointConnectionString { get; } =
		$"Data Source={Path.Combine(Path.GetTempPath(), $"es-cutover-{Guid.NewGuid():N}.db")}";

	protected override void RequireEngine() =>
		_fixture.DockerAvailable.ShouldBeTrue(
			"The alias swap and its read-back are decided by Elasticsearch, so this suite is verified "
			+ "against a real engine and must never be skipped.");

	protected override async Task CreateIndexAsync(string indexName)
	{
		var response = await Client.Indices.CreateAsync(indexName).ConfigureAwait(false);
		response.IsValidResponse.ShouldBeTrue($"failed to create index {indexName}: {response.DebugInformation}");
	}

	protected override async Task BindAliasAsync(string aliasName, string indexName)
	{
		var created = await Aliases
			.CreateAliasAsync(aliasName, [indexName], null, CancellationToken.None)
			.ConfigureAwait(false);

		created.ShouldBeTrue($"failed to bind alias {aliasName} to {indexName}");
	}

	protected override async Task<IReadOnlyCollection<string>> ReadAliasIndicesAsync(string aliasName)
	{
		// Straight from the engine's client, not through the repoint under test.
		var response = await Client.Indices
			.GetAliasAsync(new Elastic.Clients.Elasticsearch.IndexManagement.GetAliasRequest((Names)aliasName))
			.ConfigureAwait(false);

		if (!response.IsValidResponse || response.Aliases is null)
		{
			return [];
		}

		return response.Aliases
			.Where(entry => entry.Value.Aliases.ContainsKey(aliasName))
			.Select(entry => entry.Key.ToString()!)
			.ToList();
	}

	protected override IProjectionGenerationRepoint CreateRepoint(string aliasName) =>
		new AliasGenerationRepoint(Aliases, aliasName);

	private ElasticsearchClient Client =>
		new(new ElasticsearchClientSettings(new Uri(_fixture.ConnectionString)));

	private IIndexAliasManager Aliases =>
		new IndexAliasManager(Client, NullLogger<IndexAliasManager>.Instance);

	[Fact]
	public Task NotRepointTheAliasWhenTheNewGenerationHasNoCursor() =>
		RefuseTheCutoverWhenTheNewGenerationHasNoCursor();

	[Fact]
	public Task NotRepointTheAliasWhenTheNewGenerationsLagExceedsTheThreshold() =>
		RefuseTheCutoverWhenTheNewGenerationsLagExceedsTheThreshold();

	[Fact]
	public Task NotReleaseThePreviousGenerationWhenTheSwapIsNotVerified() =>
		RefuseStaleReaderRevision();

	[Fact]
	public Task RepointTheAliasAndReleaseThePreviousGenerationWhenTheNewOneIsWithinTheThreshold() =>
		CutOverAndRetainThePreviousGenerationWhenTheNewOneIsWithinTheThreshold();

    [Fact]
    public async Task RejectAnAliasAsAGenerationDestinationEvenWhenItResolvesToOneIndex()
    {
        RequireEngine();
        var index = "generation-concrete-" + Guid.NewGuid().ToString("N");
        var alias = "generation-alias-" + Guid.NewGuid().ToString("N");
        await CreateIndexAsync(index);
        await BindAliasAsync(alias, index);
        var services = new ServiceCollection();
        services.AddOptions<ElasticSearchProjectionStoreOptions>().Configure(options => options.NodeUri = _fixture.ConnectionString);
        services.ConfigureAll<ElasticSearchProjectionStoreOptions>(options => options.NodeUri = _fixture.ConnectionString);
        using var container = services.BuildServiceProvider();
        var store = new ElasticSearchProjectionStore<GenerationRow>(
            container.GetRequiredService<IOptionsMonitor<ElasticSearchProjectionStoreOptions>>(),
            NullLogger<ElasticSearchProjectionStore<GenerationRow>>.Instance);
        var factory = (IProjectionGenerationReaderFactory<GenerationRow>)store;
        await using var concreteReader = factory.OpenReader(DestinationPrefix + index);
        (await concreteReader.GetDestinationScopeAsync(CancellationToken.None)).ShouldNotBeNull();
        await using var aliasReader = factory.OpenReader(DestinationPrefix + alias);
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await aliasReader.GetDestinationScopeAsync(CancellationToken.None));
        await Should.ThrowAsync<InvalidOperationException>(() => aliasReader.GetByIdAsync("missing", CancellationToken.None));
    }

    internal sealed class GenerationRow { public string Id { get; set; } = string.Empty; }
}
