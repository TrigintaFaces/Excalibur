// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

using Elastic.Clients.Elasticsearch;

using Excalibur.Data.ElasticSearch.Projections;
using Excalibur.EventSourcing;

namespace Excalibur.Data.ElasticSearch.Tests.ElasticSearch;

/// <summary>
/// The projection store is SCOPED and the inline-projection path creates a DI scope per projection per
/// save. ElasticsearchClient owns an HTTP connection pool and exposes no disposal, so a store that
/// builds its own client leaks a connection pool on every save until the process runs out of ephemeral
/// ports -- surfacing as a request cancelled with no HTTP response, at random, under sustained load.
/// These arms bind the client to the PROCESS rather than to the scope.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "ElasticSearch")]
public sealed class ElasticSearchProjectionClientLifetimeShould : UnitTestBase
{
	private const string NodeUri = "http://localhost:9200";

	// Reads the field rather than a registration descriptor on purpose: a keyed singleton can be
	// registered and the store still build its own client, and that combination is exactly the bug.
	// What matters is the instance the store will actually issue requests through.
	private static ElasticsearchClient? ClientOf(IProjectionStore<LifetimeProjection> store) =>
		(ElasticsearchClient?)typeof(ElasticSearchProjectionStore<LifetimeProjection>)
			.GetField("_client", BindingFlags.NonPublic | BindingFlags.Instance)!
			.GetValue(store);

	private static ServiceProvider BuildProvider()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddElasticSearchProjectionStore<LifetimeProjection>(NodeUri);
		return services.BuildServiceProvider();
	}

	[Fact]
	public async Task GiveEveryScopedStoreTheSameClient()
	{
		// await using throughout: the store is IAsyncDisposable-only, and a synchronous scope dispose
		// throws rather than disposing it.
		await using var provider = BuildProvider();
		await using var first = provider.CreateAsyncScope();
		await using var second = provider.CreateAsyncScope();

		var firstStore = first.ServiceProvider.GetRequiredService<IProjectionStore<LifetimeProjection>>();
		var secondStore = second.ServiceProvider.GetRequiredService<IProjectionStore<LifetimeProjection>>();

		// Scoped: the STORES must differ, or this arm would pass for the wrong reason.
		ReferenceEquals(firstStore, secondStore).ShouldBeFalse();

		var firstClient = ClientOf(firstStore);
		var secondClient = ClientOf(secondStore);

		firstClient.ShouldNotBeNull();
		secondClient.ShouldNotBeNull();
		ReferenceEquals(firstClient, secondClient).ShouldBeTrue();
	}

	[Fact]
	public async Task KeepTheScopeBoundToTheSharedClientWhenOptionsChange()
	{
		await using var provider = BuildProvider();
		await using var first = provider.CreateAsyncScope();
		var firstStore = first.ServiceProvider.GetRequiredService<IProjectionStore<LifetimeProjection>>();
		var before = firstStore.GetService(typeof(IProjectionDestinationScope)).ShouldBeAssignableTo<IProjectionDestinationScope>().Scope;
		provider.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<ElasticSearchProjectionStoreOptions>>()
			.Get(typeof(LifetimeProjection).FullName!).NodeUri = "http://other-cluster:9200";
		await using var second = provider.CreateAsyncScope();
		var secondStore = second.ServiceProvider.GetRequiredService<IProjectionStore<LifetimeProjection>>();
		secondStore.GetService(typeof(IProjectionDestinationScope)).ShouldBeAssignableTo<IProjectionDestinationScope>().Scope.ShouldBe(before);
		ReferenceEquals(ClientOf(firstStore), ClientOf(secondStore)).ShouldBeTrue();
	}

	[Fact]
	public async Task DeclineAutomaticScopeForAnExternallySuppliedClient()
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddElasticSearchProjectionStore<LifetimeProjection>(_ => new ElasticsearchClient(new Uri(NodeUri)),
			o => o.NodeUri = "http://not-the-client:9200");
		await using var provider = services.BuildServiceProvider();
		await using var scope = provider.CreateAsyncScope();
		scope.ServiceProvider.GetRequiredService<IProjectionStore<LifetimeProjection>>()
			.GetService(typeof(IProjectionDestinationScope)).ShouldBeNull();
	}

	[Fact]
	public async Task SupplyTheClientBeforeTheStoreIssuesAnyRequest()
	{
		// A client created lazily on first use is created PER STORE, so "it works" is indistinguishable
		// from the leak. The store must already hold its client the moment it is constructed.
		await using var provider = BuildProvider();
		await using var scope = provider.CreateAsyncScope();

		var store = scope.ServiceProvider.GetRequiredService<IProjectionStore<LifetimeProjection>>();

		ClientOf(store).ShouldNotBeNull();
	}

	private sealed class LifetimeProjection
	{
		public string Id { get; set; } = string.Empty;
	}
}
