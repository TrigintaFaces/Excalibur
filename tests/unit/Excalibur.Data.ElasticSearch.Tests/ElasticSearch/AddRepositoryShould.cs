// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Elastic.Clients.Elasticsearch;

using Excalibur.Data.ElasticSearch;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Data.Tests.ElasticSearch;

internal sealed class RegistrationDocA
{
	public string Id { get; set; } = string.Empty;
}

internal sealed class RegistrationDocB
{
	public string Id { get; set; } = string.Empty;
}

internal interface IRegistrationRepoA;

internal interface IRegistrationRepoB;

internal sealed class RegistrationRepoA(ElasticsearchClient client)
	: ElasticRepositoryBase<RegistrationDocA>(client, "registration-a"), IRegistrationRepoA
{
	public override Task InitializeIndexAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class RegistrationRepoB(ElasticsearchClient client)
	: ElasticRepositoryBase<RegistrationDocB>(client, "registration-b"), IRegistrationRepoB
{
	public override Task InitializeIndexAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// Registration tests for the Elasticsearch repository helper.
/// </summary>
/// <remarks>
/// The index-initializer set is consumed as <c> IEnumerable&lt;IInitializeElasticIndex&gt; </c>, so what matters is how
/// many descriptors the container supplies -- not that the consumer iterates a hand-built list.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Data)]
public sealed class AddRepositoryShould
{
	private static ServiceProvider Build(Action<IServiceCollection> configure)
	{
		var services = new ServiceCollection();
		_ = services.AddSingleton(new ElasticsearchClient(new ElasticsearchClientSettings(new Uri("http://localhost:9200"))));
		configure(services);

		// Scope validation is what makes the scoped arms below falsifiable: without it the root provider
		// hands out a scoped instance instead of rejecting the request.
		return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
	}

	[Fact]
	public void RegisterTheInitializerForOneRepository()
	{
		// Control. A zero here would make the arm below unfalsifiable.
		using var provider = Build(static s => s.AddRepository<IRegistrationRepoA, RegistrationRepoA>());

		provider.GetServices<IInitializeElasticIndex>().Count().ShouldBe(1);
	}

	[Fact]
	public void RegisterTheInitializerForEveryRepository()
	{
		using var provider = Build(static s =>
		{
			_ = s.AddRepository<IRegistrationRepoA, RegistrationRepoA>();
			_ = s.AddRepository<IRegistrationRepoB, RegistrationRepoB>();
		});

		var initializers = provider.GetServices<IInitializeElasticIndex>().ToList();

		initializers.Count.ShouldBe(2);
		initializers.ShouldContain(static i => i is RegistrationRepoA);
		initializers.ShouldContain(static i => i is RegistrationRepoB);
	}

	[Fact]
	public void ShareOneInstanceAcrossEveryFrameworkInterface()
	{
		using var provider = Build(static s => s.AddElasticRepository<RegistrationDocA, RegistrationRepoA>());

		var repository = provider.GetRequiredService<RegistrationRepoA>();

		provider.GetRequiredService<IElasticRepositoryBase<RegistrationDocA>>().ShouldBeSameAs(repository);
		provider.GetRequiredService<IElasticRepositoryBaseQuery<RegistrationDocA>>().ShouldBeSameAs(repository);
		provider.GetServices<IInitializeElasticIndex>().ShouldHaveSingleItem().ShouldBeSameAs(repository);
	}

	[Fact]
	public void RegisterTheInitializerForEveryRepositoryRegisteredByDocument()
	{
		using var provider = Build(static s =>
		{
			_ = s.AddElasticRepository<RegistrationDocA, RegistrationRepoA>();
			_ = s.AddElasticRepository<RegistrationDocB, RegistrationRepoB>();
		});

		provider.GetServices<IInitializeElasticIndex>().Count().ShouldBe(2);
	}

	[Fact]
	public void ShareOneScopedInstanceAcrossTheFrameworkInterfacesWithinAScope()
	{
		using var provider = Build(static s =>
			s.AddElasticRepository<RegistrationDocA, RegistrationRepoA>(ServiceLifetime.Scoped));

		using var scope = provider.CreateScope();
		var repository = scope.ServiceProvider.GetRequiredService<RegistrationRepoA>();

		scope.ServiceProvider.GetRequiredService<IElasticRepositoryBase<RegistrationDocA>>().ShouldBeSameAs(repository);
		scope.ServiceProvider.GetRequiredService<IElasticRepositoryBaseQuery<RegistrationDocA>>().ShouldBeSameAs(repository);
	}

	[Fact]
	public void InitializeAScopedRepositoryThroughItsOwnSingletonInstance()
	{
		// The initializer set is enumerated from the root, so a scoped repository cannot forward to the
		// instance that serves requests. What must hold is that the index still gets initialized.
		using var provider = Build(static s =>
			s.AddElasticRepository<RegistrationDocA, RegistrationRepoA>(ServiceLifetime.Scoped));

		var initializer = provider.GetServices<IInitializeElasticIndex>().ShouldHaveSingleItem();

		_ = initializer.ShouldBeOfType<RegistrationRepoA>();

		using var scope = provider.CreateScope();
		initializer.ShouldNotBeSameAs(scope.ServiceProvider.GetRequiredService<RegistrationRepoA>());
	}

	[Fact]
	public void RegisterTheInitializerForEveryScopedRepository()
	{
		using var provider = Build(static s =>
		{
			_ = s.AddElasticRepository<RegistrationDocA, RegistrationRepoA>(ServiceLifetime.Scoped);
			_ = s.AddElasticRepository<RegistrationDocB, RegistrationRepoB>(ServiceLifetime.Scoped);
		});

		provider.GetServices<IInitializeElasticIndex>().Count().ShouldBe(2);
	}
}
