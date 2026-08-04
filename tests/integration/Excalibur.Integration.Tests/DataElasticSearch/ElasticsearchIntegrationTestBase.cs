// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor — fields are set in InitializeAsync()

using System.Collections.Concurrent;

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;

namespace Excalibur.Integration.Tests.DataElasticSearch;

/// <summary>
///     Base class for Elasticsearch integration tests, bound to a <b>shared</b> Elasticsearch container.
/// </summary>
/// <remarks>
/// <para>
/// <b>This base does not own a container, and must not.</b> xUnit constructs a new instance of a test
/// class for every fact, so the container this base used to start from <c>InitializeAsync</c> was started
/// <i>once per fact</i>. Twelve facts derived from it, so twelve Elasticsearch containers were started
/// where one would do — each paying a multi-minute start, which is what exhausted the CI job's wall clock.
/// Container ownership therefore belongs to a collection fixture
/// (<see cref="global::Tests.Shared.Fixtures.ElasticsearchContainerFixture"/>), which xUnit constructs
/// once per collection.
/// </para>
/// <para>
/// <b>Isolation on the shared container</b> is by index name. <see cref="TestIndexPrefix"/> is generated
/// in this constructor, and because xUnit builds one instance per fact, every fact gets its own unique
/// prefix. Derived classes must name every index they create with that prefix; on teardown every index
/// under the prefix is resolved and deleted, so a fact cannot leave state another fact can see. Tests
/// whose subject-under-test hardcodes a global index pattern cannot use prefixing and must reset that
/// pattern up front instead — see <c>ElasticsearchAuditTestBase</c>.
/// </para>
/// </remarks>
public abstract class ElasticsearchIntegrationTestBase : IAsyncLifetime
{
	private readonly ElasticsearchContainerFixture _fixture;

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
	///     Gets the connection string for the shared Elasticsearch container.
	/// </summary>
	protected string ConnectionString => _fixture.ConnectionString;

	/// <summary>
	///     Gets the test index prefix.
	/// </summary>
	protected string TestIndexPrefix { get; }

	/// <summary>
	///     Gets the list of indices created during the test.
	/// </summary>
	protected ConcurrentBag<string> CreatedIndices { get; }

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
	/// <param name="fixture">
	///     The shared Elasticsearch container fixture, injected by xUnit from the collection this test
	///     class belongs to.
	/// </param>
	/// <remarks>
	///     <see cref="TestIndexPrefix"/> is assigned here, and xUnit constructs one instance per fact, so
	///     each fact receives a prefix no other fact uses. That is the isolation guarantee on the shared
	///     container.
	/// </remarks>
	protected ElasticsearchIntegrationTestBase(ElasticsearchContainerFixture fixture)
	{
		_fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
		TestIndexPrefix = $"test-{Guid.NewGuid():N}-";
		CreatedIndices = [];
	}

	/// <summary>
	///     Initializes the test environment against the shared container.
	/// </summary>
	/// <remarks>
	///     No container is started here. The collection fixture already started exactly one, and starting
	///     another per fact is the defect this base was changed to remove.
	/// </remarks>
	public virtual async ValueTask InitializeAsync()
	{
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
	/// <remarks>
	///     The container is owned by the collection fixture and is deliberately NOT stopped here — it is
	///     shared with every other fact in the collection. Only this fact's own indices and services are
	///     torn down.
	/// </remarks>
	public virtual async ValueTask DisposeAsync()
	{
		try
		{
			// Clean up created indices
			await CleanupIndicesAsync().ConfigureAwait(false);
		}
		finally
		{
			// Dispose services
			(ServiceProvider as IDisposable)?.Dispose();
		}
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
			// The shared container runs with xpack.security disabled, so field encryption is off. This
			// used to be driven by an EnableSecurity toggle that no derived class ever overrode; the
			// toggle is gone rather than left in place, because on a shared container it could no longer
			// reconfigure the node and would have been a setting that silently did nothing.
			["Elasticsearch:Security:EnableFieldEncryption"] = "False",
			["Elasticsearch:Monitoring:EnableMetrics"] = EnableMonitoring.ToString(),
			["Elasticsearch:Monitoring:EnableTracing"] = EnableMonitoring.ToString(),
		};

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

		var response = await Client.SearchAsync<TDocument>(searchRequest).ConfigureAwait(false);
		response.IsValidResponse.ShouldBeTrue("Search failed");

		return response.Documents;
	}

	/// <summary>
	///     Deletes every index this test created.
	/// </summary>
	/// <remarks>
	///     The container is shared, so leaving indices behind is no longer harmless — it leaks state into
	///     the facts that run after this one. Deletion is therefore by <see cref="TestIndexPrefix"/>
	///     wildcard, which covers indices created implicitly by an indexing call as well as those
	///     registered in <see cref="CreatedIndices"/>. Indices outside this fact's prefix are never
	///     matched, so no other fact's state can be destroyed.
	/// </remarks>
	protected virtual async Task CleanupIndicesAsync()
	{
		// Resolve the prefix to CONCRETE index names before deleting. A wildcard index delete is
		// rejected outright by Elasticsearch 8+ (action.destructive_requires_name defaults to true), so
		// Indices.DeleteAsync("prefix*") would report an invalid response and remove nothing — cleanup
		// that looks like it works and does not. Deleting resolved names sidesteps that entirely.
		var names = new HashSet<string>(CreatedIndices, StringComparer.Ordinal);

		try
		{
			var resolved = await Client.Indices
				.GetAsync(new GetIndexRequest($"{TestIndexPrefix}*") { IgnoreUnavailable = true })
				.ConfigureAwait(false);

			if (resolved.IsValidResponse)
			{
				foreach (var name in resolved.Indices.Keys)
				{
					_ = names.Add(name.ToString()!);
				}
			}
		}
		catch (Exception ex)
		{
			LoggerFactory.CreateLogger<ElasticsearchIntegrationTestBase>()
				.LogWarning(ex, "Error resolving indices for {Pattern}", $"{TestIndexPrefix}*");
		}

		foreach (var index in names)
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

}
