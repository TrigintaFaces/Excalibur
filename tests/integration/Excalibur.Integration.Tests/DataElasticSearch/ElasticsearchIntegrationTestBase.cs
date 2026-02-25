// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor — fields are set in InitializeAsync()

using System.Collections.Concurrent;

using DotNet.Testcontainers.Builders;

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Transport;

using Testcontainers.Elasticsearch;

namespace Excalibur.Integration.Tests.DataElasticSearch;

/// <summary>
///     Base class for Elasticsearch integration tests with TestContainers support.
/// </summary>
public abstract class ElasticsearchIntegrationTestBase : IAsyncLifetime, IDisposable
{
	private ElasticsearchContainer? _container;
	private bool _disposed;

	/// <summary>
	///     Gets the service provider for the test.
	/// </summary>
	protected IServiceProvider ServiceProvider { get; private set; }

	/// <summary>
	///     Gets the Elasticsearch client.
	/// </summary>
	protected ElasticsearchClient Client { get; private set; }

	/// <summary>
	///     Gets the test logger factory.
	/// </summary>
	protected ILoggerFactory LoggerFactory { get; private set; }

	/// <summary>
	///     Gets the test configuration.
	/// </summary>
	protected IConfiguration Configuration { get; private set; }

	/// <summary>
	///     Gets the connection string for the Elasticsearch container.
	/// </summary>
	protected string ConnectionString => _container?.GetConnectionString() ?? string.Empty;

	/// <summary>
	///     Gets the test index prefix.
	/// </summary>
	protected string TestIndexPrefix { get; }

	/// <summary>
	///     Gets the list of indices created during the test.
	/// </summary>
	protected ConcurrentBag<string> CreatedIndices { get; }

	/// <summary>
	///     Gets a value indicating whether to enable security features in the test container.
	/// </summary>
	protected virtual bool EnableSecurity => false;

	/// <summary>
	///     Gets a value indicating whether to enable monitoring features in the test.
	/// </summary>
	protected virtual bool EnableMonitoring => true;

	/// <summary>
	///     Gets a value indicating whether to enable performance features in the test.
	/// </summary>
	protected virtual bool EnablePerformanceFeatures => true;

	/// <summary>
	///     Initializes a new instance of the <see cref="ElasticsearchIntegrationTestBase" /> class.
	/// </summary>
	protected ElasticsearchIntegrationTestBase()
	{
		TestIndexPrefix = $"test-{Guid.NewGuid():N}-";
		CreatedIndices = [];
	}

	/// <summary>
	///     Initializes the test environment.
	/// </summary>
	public virtual async Task InitializeAsync()
	{
		// Start Elasticsearch container
		await StartContainerAsync().ConfigureAwait(false);

		// Setup services
		var services = new ServiceCollection();

		// Setup configuration
		var configBuilder = new ConfigurationBuilder();
		ConfigureTestConfiguration(configBuilder);
		Configuration = configBuilder.Build();

		// Configure services
		ConfigureTestServices(services);

		// Add Elasticsearch services
		_ = services.AddElasticsearchServices(Configuration, null);

		if (EnableMonitoring)
		{
			_ = services.AddElasticsearchMonitoring(Configuration);
		}

		if (EnablePerformanceFeatures)
		{
			// TODO: Re-enable when AddElasticsearchPerformanceOptimizations is restored
			// _ = services.AddElasticsearchPerformanceOptimizations(Configuration);
		}

		if (EnableSecurity)
		{
			_ = services.AddElasticsearchSecurity(Configuration);
		}

		// Add logging
		_ = services.AddLogging(static builder =>
		{
			_ = builder.AddConsole();
			_ = builder.SetMinimumLevel(LogLevel.Debug);
		});

		ServiceProvider = services.BuildServiceProvider();
		LoggerFactory = ServiceProvider.GetRequiredService<ILoggerFactory>();
		Client = ServiceProvider.GetRequiredService<ElasticsearchClient>();

		// Initialize test environment
		await InitializeTestEnvironmentAsync().ConfigureAwait(false);
	}

	/// <summary>
	///     Disposes of test resources.
	/// </summary>
	public virtual async Task DisposeAsync()
	{
		try
		{
			// Clean up created indices
			await CleanupIndicesAsync().ConfigureAwait(false);

			// Dispose services
			(ServiceProvider as IDisposable)?.Dispose();
		}
		finally
		{
			// Stop container
			if (_container != null)
			{
				await _container.StopAsync().ConfigureAwait(false);
				await _container.DisposeAsync().ConfigureAwait(false);
			}
		}
	}

	/// <summary>
	///     Starts the Elasticsearch container.
	/// </summary>
	protected virtual async Task StartContainerAsync()
	{
		var builder = new ElasticsearchBuilder()
			.WithImage("docker.elastic.co/elasticsearch/elasticsearch:8.15.0")
			.WithName($"es-test-{Guid.NewGuid():N}")
			.WithEnvironment("discovery.type", "single-node")
			.WithEnvironment("xpack.security.enabled", EnableSecurity.ToString().ToUpperInvariant())
			.WithEnvironment("xpack.monitoring.enabled", EnableMonitoring.ToString().ToUpperInvariant())
			.WithEnvironment("indices.query.bool.max_clause_count", "10000")
			.WithEnvironment("ES_JAVA_OPTS", "-Xms512m -Xmx512m")
			.WithPortBinding(9200, true)
			.WithWaitStrategy(Wait.ForUnixContainer()
				.UntilHttpRequestIsSucceeded(static r => r
					.ForPort(9200)
					.ForPath("/")
				.WithMethod(System.Net.Http.HttpMethod.Get)));

		if (EnableSecurity)
		{
			builder = builder
				.WithEnvironment("ELASTIC_PASSWORD", "changeme")
				.WithEnvironment("xpack.security.http.ssl.enabled", "false");
		}

		_container = builder.Build();
		await _container.StartAsync().ConfigureAwait(false);

		// Wait for cluster to be ready
		await WaitForClusterHealthAsync().ConfigureAwait(false);
	}

	/// <summary>
	///     Waits for the Elasticsearch cluster to be healthy.
	/// </summary>
	protected virtual async Task WaitForClusterHealthAsync()
	{
		var maxAttempts = 30;
		var attempt = 0;

		while (attempt < maxAttempts)
		{
			try
			{
				// CA2000: Settings object lifetime is managed by ElasticsearchClient
#pragma warning disable CA2000
				var settings = new ElasticsearchClientSettings(new Uri(ConnectionString))
					.DefaultIndex($"{TestIndexPrefix}default")
					.EnableDebugMode()
					.PrettyJson();
#pragma warning restore CA2000

				if (EnableSecurity)
				{
					settings = settings.Authentication(new BasicAuthentication("elastic", "changeme"));
				}

				var tempClient = new ElasticsearchClient(settings);
				var health = await tempClient.Cluster.HealthAsync().ConfigureAwait(false);

				if (health.IsValidResponse &&
					(health.Status == HealthStatus.Green || health.Status == HealthStatus.Yellow))
				{
					return;
				}
			}
			catch
			{
				// Ignore and retry
			}

			attempt++;
			await Task.Delay(1000).ConfigureAwait(false);
		}

		throw new InvalidOperationException("Elasticsearch cluster failed to become healthy");
	}

	/// <summary>
	///     Configures the test configuration.
	/// </summary>
	/// <param name="builder"> The configuration builder. </param>
	protected virtual void ConfigureTestConfiguration(IConfigurationBuilder builder)
	{
		var testConfig = new Dictionary<string, string?>
		{
			["Elasticsearch:Urls:0"] = ConnectionString,
			["Elasticsearch:DefaultIndex"] = $"{TestIndexPrefix}default",
			["Elasticsearch:EnableDebugMode"] = "true",
			["Elasticsearch:Resilience:MaxRetryAttempts"] = "3",
			["Elasticsearch:Resilience:RetryDelayMilliseconds"] = "100",
			["Elasticsearch:Resilience:CircuitBreakerThreshold"] = "5",
			["Elasticsearch:Performance:EnableCaching"] = EnablePerformanceFeatures.ToString(),
			["Elasticsearch:Performance:CacheExpirationMinutes"] = "5",
			["Elasticsearch:Performance:EnableQueryOptimization"] = EnablePerformanceFeatures.ToString(),
			["Elasticsearch:Security:EnableFieldEncryption"] = EnableSecurity.ToString(),
			["Elasticsearch:Monitoring:EnableMetrics"] = EnableMonitoring.ToString(),
			["Elasticsearch:Monitoring:EnableTracing"] = EnableMonitoring.ToString(),
		};

		if (EnableSecurity)
		{
			testConfig["Elasticsearch:Username"] = "elastic";
			testConfig["Elasticsearch:Password"] = "test-password";
		}

		_ = builder.AddInMemoryCollection(testConfig);
	}

	/// <summary>
	///     Configures test services for dependency injection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	protected virtual void ConfigureTestServices(IServiceCollection services)
	{
		// Add test-specific service configurations here
	}

	/// <summary>
	///     Initializes the test environment after services are configured.
	/// </summary>
	protected virtual async Task InitializeTestEnvironmentAsync() =>
		// Override in derived classes to perform additional initialization
		await Task.CompletedTask.ConfigureAwait(false);

	/// <summary>
	///     Creates a test index with the specified name.
	/// </summary>
	/// <param name="indexName"> The index name. </param>
	/// <param name="configure"> Optional index configuration. </param>
	protected async Task<string> CreateTestIndexAsync(
		string indexName,
		Action<CreateIndexRequestDescriptor>? configure = null)
	{
		var fullIndexName = $"{TestIndexPrefix}{indexName}";
		CreatedIndices.Add(fullIndexName);

		var createRequest = new CreateIndexRequestDescriptor(fullIndexName);
		configure?.Invoke(createRequest);

		var response = await Client.Indices.CreateAsync(createRequest).ConfigureAwait(false);
		response.IsValidResponse.ShouldBeTrue($"Failed to create index {fullIndexName}");

		// Wait for index to be ready
		await WaitForIndexAsync(fullIndexName).ConfigureAwait(false);

		return fullIndexName;
	}

	/// <summary>
	///     Waits for an index to be ready.
	/// </summary>
	/// <param name="indexName"> The index name. </param>
	protected async Task WaitForIndexAsync(string indexName)
	{
		var maxAttempts = 10;
		var attempt = 0;

		while (attempt < maxAttempts)
		{
			var exists = await Client.Indices.ExistsAsync(indexName).ConfigureAwait(false);
			if (exists is { IsValidResponse: true, Exists: true })
			{
				// Refresh the index to make sure it's ready for searches
				_ = await Client.Indices.RefreshAsync(indexName).ConfigureAwait(false);
				return;
			}

			attempt++;
			await Task.Delay(500).ConfigureAwait(false);
		}

		throw new InvalidOperationException($"Index {indexName} failed to become ready");
	}

	/// <summary>
	///     Indexes test documents.
	/// </summary>
	/// <typeparam name="TDocument"> The document type. </typeparam>
	/// <param name="indexName"> The index name. </param>
	/// <param name="documents"> The documents to index. </param>
	protected async Task IndexDocumentsAsync<TDocument>(string indexName, params TDocument[] documents)
		where TDocument : class
	{
		ArgumentNullException.ThrowIfNull(documents);
		if (documents.Length == 0)
		{
			return;
		}

		if (documents.Length == 1)
		{
			var response = await Client.IndexAsync(documents[0], i => i.Index(indexName)).ConfigureAwait(false);
			response.IsValidResponse.ShouldBeTrue("Failed to index document");
		}
		else
		{
			var bulkRequest = new BulkRequest(indexName) { Operations = [] };

			foreach (var doc in documents)
			{
				bulkRequest.Operations.Add(new Elastic.Clients.Elasticsearch.Core.Bulk.BulkIndexOperation<TDocument>(doc));
			}

			var response = await Client.BulkAsync(bulkRequest).ConfigureAwait(false);
			response.IsValidResponse.ShouldBeTrue("Failed to bulk index documents");
			response.Errors.ShouldBeFalse("Bulk indexing had errors");
		}

		// Refresh index to make documents searchable
		_ = await Client.Indices.RefreshAsync(indexName).ConfigureAwait(false);
	}

	/// <summary>
	///     Searches for documents in the specified index.
	/// </summary>
	/// <typeparam name="TDocument"> The document type. </typeparam>
	/// <param name="indexName"> The index name. </param>
	/// <param name="configure"> Optional search configuration. </param>
	/// <returns> The search results. </returns>
	protected async Task<IReadOnlyCollection<TDocument>> SearchDocumentsAsync<TDocument>(
		string indexName,
		Action<SearchRequestDescriptor<TDocument>>? configure = null)
		where TDocument : class
	{
		var searchRequest = new SearchRequestDescriptor<TDocument>()
			.Index(indexName)
			.Size(100);

		configure?.Invoke(searchRequest);

		var response = await Client.SearchAsync(searchRequest).ConfigureAwait(false);
		response.IsValidResponse.ShouldBeTrue("Search failed");

		return response.Documents;
	}

	/// <summary>
	///     Cleans up all created indices.
	/// </summary>
	protected virtual async Task CleanupIndicesAsync()
	{
		foreach (var index in CreatedIndices)
		{
			try
			{
				var response = await Client.Indices.DeleteAsync(index).ConfigureAwait(false);
				if (!response.IsValidResponse)
				{
					LoggerFactory.CreateLogger<ElasticsearchIntegrationTestBase>()
						.LogWarning("Failed to delete index {Index}: {Error}", index, response.DebugInformation);
				}
			}
			catch (Exception ex)
			{
				LoggerFactory.CreateLogger<ElasticsearchIntegrationTestBase>()
					.LogWarning(ex, "Error deleting index {Index}", index);
			}
		}
	}

	/// <summary>
	///     Gets a service from the service provider.
	/// </summary>
	/// <typeparam name="TService"> The service type. </typeparam>
	/// <returns> The service instance. </returns>
	protected TService GetService<TService>() where TService : notnull => ServiceProvider.GetRequiredService<TService>();

	/// <summary>
	///     Disposes of test resources.
	/// </summary>
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	///     Disposes of test resources.
	/// </summary>
	/// <param name="disposing"> Whether to dispose managed resources. </param>
	protected virtual void Dispose(bool disposing)
	{
		if (!_disposed)
		{
			if (disposing)
			{
				DisposeAsync().GetAwaiter().GetResult();
			}

			_disposed = true;
		}
	}
}
