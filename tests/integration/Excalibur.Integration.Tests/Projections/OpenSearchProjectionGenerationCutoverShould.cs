// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.OpenSearch.IndexManagement;
using Excalibur.EventSourcing;
using Excalibur.Integration.Tests.OpenSearch;

using Microsoft.Extensions.DependencyInjection;

using OpenSearch.Client;

using Excalibur.Data.OpenSearch.Projections;

using Microsoft.Extensions.Options;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Integration.Tests.Projections;

/// <summary>
/// The lag-gated generation cutover over a real OpenSearch alias.
/// </summary>
/// <remarks>
/// Proved separately from Elasticsearch rather than inferred from it. The two providers reach the same
/// contract through different clients — a bulk alias action here against a multi-action alias update
/// there — and a result carried across would cover neither the read-back shape nor the atomicity of the
/// one that was not run.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Database", "OpenSearch")]
[Trait("Component", "Projections")]
[Collection(OpenSearchTestCollection.CollectionName)]
public sealed class OpenSearchProjectionGenerationCutoverShould : ProjectionGenerationCutoverConformance
{
	private readonly OpenSearchContainerFixture _fixture;

	public OpenSearchProjectionGenerationCutoverShould(OpenSearchContainerFixture fixture) =>
		_fixture = fixture;

    protected override string DestinationPrefix => "opensearch/";

	protected override string CheckpointConnectionString { get; } =
		$"Data Source={Path.Combine(Path.GetTempPath(), $"os-cutover-{Guid.NewGuid():N}.db")}";

	protected override void RequireEngine() =>
		_fixture.Available.ShouldBeTrue(
			"The alias swap and its read-back are decided by OpenSearch, so this suite is verified "
			+ "against a real engine and must never be skipped.");

	protected override async Task CreateIndexAsync(string indexName)
	{
		var response = await Client.Indices.CreateAsync(indexName).ConfigureAwait(false);
		response.IsValid.ShouldBeTrue($"failed to create index {indexName}: {response.DebugInformation}");
	}

	protected override async Task BindAliasAsync(string aliasName, string indexName)
	{
		var created = await Aliases
			.CreateAliasAsync(aliasName, [indexName], CancellationToken.None)
			.ConfigureAwait(false);

		created.ShouldBeTrue($"failed to bind alias {aliasName} to {indexName}");
	}

	protected override async Task<IReadOnlyCollection<string>> ReadAliasIndicesAsync(string aliasName)
	{
		// Straight from the engine's client, not through the repoint under test.
		var response = await Client.Indices
			.GetAliasAsync(Indices.All, a => a.Name(aliasName))
			.ConfigureAwait(false);

		if (!response.IsValid || response.Indices is null)
		{
			return [];
		}

		return response.Indices
			.Where(entry => entry.Value.Aliases.ContainsKey(aliasName))
			.Select(entry => entry.Key.ToString()!)
			.ToList();
	}

	protected override IProjectionGenerationRepoint CreateRepoint(string aliasName) =>
		new AliasGenerationRepoint(Aliases, aliasName);

	private IOpenSearchClient Client =>
		new OpenSearchClient(new ConnectionSettings(_fixture.Endpoint));

	/// <summary>
	/// Resolved through the package's own registration, because the implementation is internal and a
	/// consumer can only reach it this way.
	/// </summary>
	private IIndexAliasManager Aliases
	{
		get
		{
			var services = new ServiceCollection();
			_ = services.AddLogging();
			_ = services.AddOpenSearchServices(_fixture.Endpoint.ToString());
			_ = services.AddOpenSearchIndexManagement();

			return services.BuildServiceProvider().GetRequiredService<IIndexAliasManager>();
		}
	}

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
        services.AddOptions<OpenSearchProjectionStoreOptions>().Configure(options => options.NodeUri = _fixture.Endpoint.ToString());
        services.ConfigureAll<OpenSearchProjectionStoreOptions>(options => options.NodeUri = _fixture.Endpoint.ToString());
        using var container = services.BuildServiceProvider();
        var store = new OpenSearchProjectionStore<GenerationRow>(Client,
            container.GetRequiredService<IOptionsMonitor<OpenSearchProjectionStoreOptions>>(),
            NullLogger<OpenSearchProjectionStore<GenerationRow>>.Instance);
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
