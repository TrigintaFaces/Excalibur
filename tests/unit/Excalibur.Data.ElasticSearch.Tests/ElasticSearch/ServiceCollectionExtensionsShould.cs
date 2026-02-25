// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IL2026 // RequiresUnreferencedCode
#pragma warning disable IL3050 // RequiresDynamicCode

using Elastic.Clients.Elasticsearch;

using Excalibur.Data.ElasticSearch;
using Excalibur.Data.ElasticSearch.IndexManagement;
using Excalibur.Data.ElasticSearch.Projections;
using Excalibur.Data.ElasticSearch.Resilience;

namespace Excalibur.Data.Tests.ElasticSearch;

[Trait("Category", "Unit")]
[Trait("Component", "Data")]
public sealed class ServiceCollectionExtensionsShould
{
	[Fact]
	public void RegisterServicesWithPreconfiguredClient()
	{
		// Arrange
		var services = new ServiceCollection();
#pragma warning disable CA2000
		var settings = new ElasticsearchClientSettings(new Uri("http://localhost:9200"));
		var client = new ElasticsearchClient(settings);
#pragma warning restore CA2000

		// Act
		services.AddElasticsearchServices(client, null);

		// Assert
		using var sp = services.BuildServiceProvider();
		sp.GetService<ElasticsearchClient>().ShouldNotBeNull();
		sp.GetService<IIndexInitializer>().ShouldNotBeNull();
		sp.GetService<IElasticsearchHealthClient>().ShouldNotBeNull();
	}

	[Fact]
	public void ThrowWhenClientIsNullForPreconfiguredOverload()
	{
		var services = new ServiceCollection();
		Should.Throw<ArgumentNullException>(() => services.AddElasticsearchServices(null!, null));
	}

	[Fact]
	public void InvokeRegistryDelegate()
	{
		// Arrange
		var services = new ServiceCollection();
#pragma warning disable CA2000
		var settings = new ElasticsearchClientSettings(new Uri("http://localhost:9200"));
		var client = new ElasticsearchClient(settings);
#pragma warning restore CA2000
		var invoked = false;

		// Act
		services.AddElasticsearchServices(client, _ => invoked = true);

		// Assert
		invoked.ShouldBeTrue();
	}

	[Fact]
	public void RegisterServicesWithConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ElasticSearch:Url"] = "http://localhost:9200",
			})
			.Build();

		// Act
		services.AddElasticsearchServices(config, null);

		// Assert
		using var sp = services.BuildServiceProvider();
		sp.GetService<IIndexInitializer>().ShouldNotBeNull();
		sp.GetService<IElasticsearchHealthClient>().ShouldNotBeNull();
	}

	[Fact]
	public void ThrowWhenConfigurationIsNull()
	{
		var services = new ServiceCollection();
		Should.Throw<ArgumentNullException>(
			() => services.AddElasticsearchServices((IConfiguration)null!, null));
	}

	[Fact]
	public void RegisterResilientServicesWithConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ElasticSearch:Url"] = "http://localhost:9200",
			})
			.Build();

		// Act
		services.AddResilientElasticsearchServices(config);

		// Assert
		using var sp = services.BuildServiceProvider();
		sp.GetService<IElasticsearchRetryPolicy>().ShouldNotBeNull();
		sp.GetService<IElasticsearchCircuitBreaker>().ShouldNotBeNull();
		sp.GetService<IResilientElasticsearchClient>().ShouldNotBeNull();
	}

	[Fact]
	public void ThrowWhenConfigurationIsNullForResilient()
	{
		var services = new ServiceCollection();
		Should.Throw<ArgumentNullException>(
			() => services.AddResilientElasticsearchServices((IConfiguration)null!));
	}

	[Fact]
	public void RegisterResilientServicesWithPreconfiguredClient()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
#pragma warning disable CA2000
		var settings = new ElasticsearchClientSettings(new Uri("http://localhost:9200"));
		var client = new ElasticsearchClient(settings);
#pragma warning restore CA2000
		var configOptions = new ElasticsearchConfigurationOptions();

		// Register IOptions<ElasticsearchConfigurationOptions> which the retry policy requires
		services.Configure<ElasticsearchConfigurationOptions>(_ => { });

		// Act
		services.AddResilientElasticsearchServices(client, configOptions);

		// Assert
		using var sp = services.BuildServiceProvider();
		sp.GetService<ElasticsearchClient>().ShouldNotBeNull();
		sp.GetService<IElasticsearchRetryPolicy>().ShouldNotBeNull();
		sp.GetService<IElasticsearchCircuitBreaker>().ShouldNotBeNull();
		sp.GetService<IResilientElasticsearchClient>().ShouldNotBeNull();
	}

	[Fact]
	public void ThrowWhenClientIsNullForResilientPreconfigured()
	{
		var services = new ServiceCollection();
		Should.Throw<ArgumentNullException>(
			() => services.AddResilientElasticsearchServices(null!, new ElasticsearchConfigurationOptions()));
	}

	[Fact]
	public void ThrowWhenOptionsIsNullForResilientPreconfigured()
	{
		var services = new ServiceCollection();
#pragma warning disable CA2000
		var client = new ElasticsearchClient(new ElasticsearchClientSettings(new Uri("http://localhost:9200")));
#pragma warning restore CA2000

		Should.Throw<ArgumentNullException>(
			() => services.AddResilientElasticsearchServices(client, null!));
	}

	[Fact]
	public void RegisterMonitoringServices()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ElasticSearch:Monitoring:Tracing:ActivitySourceName"] = "test-source",
			})
			.Build();

		// Act
		services.AddElasticsearchMonitoring(config);

		// Assert
		using var sp = services.BuildServiceProvider();
		sp.GetService<IOptions<Excalibur.Data.ElasticSearch.Monitoring.ElasticsearchMonitoringOptions>>().ShouldNotBeNull();
	}

	[Fact]
	public void ThrowWhenConfigurationIsNullForMonitoring()
	{
		var services = new ServiceCollection();
		Should.Throw<ArgumentNullException>(() => services.AddElasticsearchMonitoring(null!));
	}

	[Fact]
	public void RegisterMonitoredResilientServices()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ElasticSearch:Url"] = "http://localhost:9200",
			})
			.Build();

		// Act
		services.AddMonitoredResilientElasticsearchServices(config);

		// Assert
		using var sp = services.BuildServiceProvider();
		sp.GetService<IResilientElasticsearchClient>().ShouldNotBeNull();
	}

	[Fact]
	public void ThrowWhenConfigurationIsNullForMonitoredResilient()
	{
		var services = new ServiceCollection();
		Should.Throw<ArgumentNullException>(
			() => services.AddMonitoredResilientElasticsearchServices(null!));
	}

	[Fact]
	public void RegisterIndexManagementServices()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();
#pragma warning disable CA2000
		var client = new ElasticsearchClient(new ElasticsearchClientSettings(new Uri("http://localhost:9200")));
#pragma warning restore CA2000
		services.AddSingleton(client);

		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ElasticSearch:IndexManagement:Environment:Name"] = "test",
			})
			.Build();

		// Act
		services.AddElasticsearchIndexManagement(config);

		// Assert
		using var sp = services.BuildServiceProvider();
		sp.GetService<IIndexTemplateManager>().ShouldNotBeNull();
		sp.GetService<IIndexLifecycleManager>().ShouldNotBeNull();
		sp.GetService<IIndexOperationsManager>().ShouldNotBeNull();
		sp.GetService<IIndexAliasManager>().ShouldNotBeNull();
	}

	[Fact]
	public void ThrowWhenConfigurationIsNullForIndexManagement()
	{
		var services = new ServiceCollection();
		Should.Throw<ArgumentNullException>(() => services.AddElasticsearchIndexManagement(null!));
	}

	[Fact]
	public void RegisterProjectionServices()
	{
		// Arrange
		var services = new ServiceCollection();
#pragma warning disable CA2000
		var client = new ElasticsearchClient(new ElasticsearchClientSettings(new Uri("http://localhost:9200")));
#pragma warning restore CA2000
		services.AddSingleton(client);
		services.AddLogging();
		services.AddMetrics();

		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ElasticSearch:Projections:IndexPrefix"] = "test-proj",
			})
			.Build();

		// ProjectionRebuildManager depends on IIndexAliasManager from index management
		services.AddElasticsearchIndexManagement(config);

		// Act
		services.AddElasticsearchProjections(config);

		// Assert
		using var sp = services.BuildServiceProvider();
		sp.GetService<IProjectionErrorHandler>().ShouldNotBeNull();
		sp.GetService<IProjectionRebuildManager>().ShouldNotBeNull();
		sp.GetService<IEventualConsistencyTracker>().ShouldNotBeNull();
		sp.GetService<ISchemaEvolutionHandler>().ShouldNotBeNull();
	}

	[Fact]
	public void ThrowWhenConfigurationIsNullForProjections()
	{
		var services = new ServiceCollection();
		Should.Throw<ArgumentNullException>(() => services.AddElasticsearchProjections(null!));
	}

	[Fact]
	public void RegisterProjectionOptionsValidator()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ElasticSearch:Projections:IndexPrefix"] = "test",
			})
			.Build();

#pragma warning disable CA2000
		var client = new ElasticsearchClient(new ElasticsearchClientSettings(new Uri("http://localhost:9200")));
#pragma warning restore CA2000
		services.AddSingleton(client);
		services.AddLogging();
		services.AddMetrics();

		// Act
		services.AddElasticsearchProjections(config);

		// Assert — validator resolves without needing full dependency tree
		using var sp = services.BuildServiceProvider();
		var validators = sp.GetServices<IValidateOptions<ProjectionOptions>>();
		validators.ShouldContain(v => v is ProjectionOptionsValidator);
	}
}
