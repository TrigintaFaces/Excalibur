// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Elastic.Clients.Elasticsearch;

using Excalibur.Data.ElasticSearch;

namespace Excalibur.Data.Tests.ElasticSearch;

internal sealed class InitializationProbeDocument
{
	public string Id { get; set; } = string.Empty;
}

/// <summary>
/// Counts how many times the framework asked it to provision its index.
/// </summary>
internal sealed class InitializationProbeRepository(ElasticsearchClient client)
	: ElasticRepositoryBase<InitializationProbeDocument>(client, "initialization-probe")
{
	public int IndexInitializations { get; private set; }

	public override Task InitializeIndexAsync(CancellationToken cancellationToken)
	{
		IndexInitializations++;
		return Task.CompletedTask;
	}
}

/// <summary>
/// The repository whose index is owned by someone else -- a projection store, or a deployment-time script.
/// It overrides nothing, which only compiles while index provisioning is optional.
/// </summary>
internal sealed class ProvisionedElsewhereRepository(ElasticsearchClient client)
	: ElasticRepositoryBase<InitializationProbeDocument>(client, "provisioned-elsewhere");

/// <summary>
/// What the framework does with a repository's index-provisioning hook.
/// </summary>
/// <remarks>
/// The index initializer set is enumerated once at start-up and each member's <c> InitializeAsync </c> is
/// awaited. A repository that declares how to provision its index must therefore have that declaration
/// invoked by <c> InitializeAsync </c> -- otherwise the consumer writes two overrides, one of which does
/// nothing unless the other forwards to it, and forgetting the forward silently skips provisioning.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class RepositoryIndexInitializationShould
{
	[Fact]
	public async Task InvokeTheIndexProvisioningHook()
	{
		var repository = new InitializationProbeRepository(Client());

		await repository.InitializeAsync(CancellationToken.None).ConfigureAwait(true);

		repository.IndexInitializations.ShouldBe(
			1,
			"the initializer set awaits InitializeAsync, so that is the member which must reach the "
			+ "repository's own index declaration -- exactly once, not zero times and not twice");
	}

	/// <summary>
	/// CONTROL, and the reason the hook is not abstract: a repository over an index someone else owns must
	/// be able to say nothing at all, and start.
	/// </summary>
	[Fact]
	public async Task StartARepositoryThatProvisionsNothing()
	{
		var repository = new ProvisionedElsewhereRepository(Client());

		await repository.InitializeAsync(CancellationToken.None).ConfigureAwait(true);
	}

	private static ElasticsearchClient Client() =>
		new(new ElasticsearchClientSettings(new Uri("http://localhost:9200")));
}
