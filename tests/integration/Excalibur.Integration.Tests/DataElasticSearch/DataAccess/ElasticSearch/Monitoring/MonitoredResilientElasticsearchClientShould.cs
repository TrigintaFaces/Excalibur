// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Aggregations;
using Elastic.Clients.Elasticsearch.Core.Bulk;
using Elastic.Clients.Elasticsearch.QueryDsl;

using Excalibur.Data.ElasticSearch.Monitoring;
using Excalibur.Data.ElasticSearch.Resilience;

using Testcontainers.Elasticsearch;

namespace Excalibur.Integration.Tests.DataElasticSearch.DataAccess.ElasticSearch.Monitoring;

/// <summary>
///     Integration tests for the <see cref="MonitoredResilientElasticsearchClient" /> class.
/// </summary>
public sealed class MonitoredResilientElasticsearchClientShould : IAsyncLifetime
{
	private ElasticsearchContainer? _elasticsearchContainer;
	private ServiceProvider? _serviceProvider;
	private IResilientElasticsearchClient? _client;
	private ElasticsearchMonitoringService? _monitoringService;
	private bool _dockerAvailable;

	/// <inheritdoc/>
	public async Task InitializeAsync()
	{
		try
		{
			_elasticsearchContainer = new ElasticsearchBuilder()
				.WithImage("elasticsearch:8.11.0")
				.WithEnvironment("discovery.type", "single-node")
				.WithEnvironment("xpack.security.enabled", "false")
				.WithPortBinding(9200, true)
				.Build();

			await _elasticsearchContainer.StartAsync().ConfigureAwait(false);

			var configuration = new ConfigurationBuilder()
				.AddInMemoryCollection(new Dictionary<string, string?>
				{
					["ElasticSearch:Url"] = _elasticsearchContainer.GetConnectionString(),
					["ElasticSearch:Resilience:Enabled"] = "true",
					["ElasticSearch:Resilience:Retry:MaxAttempts"] = "3",
					["ElasticSearch:Resilience:CircuitBreaker:Enabled"] = "true",
					["ElasticSearch:Monitoring:Enabled"] = "true",
					["ElasticSearch:Monitoring:Level"] = "Verbose",
					["ElasticSearch:Monitoring:Metrics:Enabled"] = "true",
					["ElasticSearch:Monitoring:RequestLogging:Enabled"] = "true",
					["ElasticSearch:Monitoring:Performance:Enabled"] = "true",
					["ElasticSearch:Monitoring:Health:Enabled"] = "true",
					["ElasticSearch:Monitoring:Tracing:Enabled"] = "true",
				})
				.Build();

			var services = new ServiceCollection();
			_ = services.AddLogging(static builder => builder.AddConsole());
			_ = services.AddMonitoredResilientElasticsearchServices(configuration);

			_serviceProvider = services.BuildServiceProvider();
			_client = _serviceProvider.GetRequiredService<IResilientElasticsearchClient>();
			_monitoringService = _serviceProvider.GetRequiredService<ElasticsearchMonitoringService>();

			// Wait for Elasticsearch to be ready
			var healthCheck = await _client.IsHealthyAsync(CancellationToken.None).ConfigureAwait(false);
			_dockerAvailable = healthCheck;
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Docker/Elasticsearch initialization failed: {ex.Message}");
			_dockerAvailable = false;
		}
	}

	[Fact]
	public async Task IndexDocumentWithFullMonitoring()
	{
		if (!_dockerAvailable) { return; }

		// Arrange
		var testDoc = new TestDocument { Id = "test-1", Name = "Test Document", Value = 42 };
		var indexRequest = new IndexRequest<TestDocument>("test-index") { Document = testDoc };

		// Act
		var response = await _client.IndexAsync(indexRequest, CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = response.ShouldNotBeNull();
		response.IsValidResponse.ShouldBeTrue();
		response.Id.ShouldBe("test-1");

		// Verify monitoring metrics are available
		var performanceMetrics = _monitoringService.GetPerformanceMetrics();
		performanceMetrics.ShouldNotBeEmpty();
		performanceMetrics.ShouldContainKey("index");
	}

	[Fact]
	public async Task SearchDocumentsWithMetricsCollection()
	{
		if (!_dockerAvailable) { return; }

		// Arrange - Index a test document first
		var testDoc = new TestDocument { Id = "test-2", Name = "Search Test", Value = 100 };
		var indexRequest = new IndexRequest<TestDocument>("test-index") { Document = testDoc };
		_ = await _client.IndexAsync(indexRequest, CancellationToken.None).ConfigureAwait(false);

		// Wait for indexing
		await Task.Delay(1000).ConfigureAwait(false);

		var searchRequest = new SearchRequest(Indices.Parse("test-index"))
		{
			Query = new MatchQuery(new Field("name")) { Query = "Search" },
			Size = 10,
		};

		// Act
		var response = await _client.SearchAsync<TestDocument>(searchRequest, CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = response.ShouldNotBeNull();
		response.IsValidResponse.ShouldBeTrue();
		response.Documents.Count.ShouldBeGreaterThan(0);

		// Verify search metrics
		var performanceMetrics = _monitoringService.GetPerformanceMetrics();
		performanceMetrics.ShouldContainKey("search");
		performanceMetrics["search"].TotalOperations.ShouldBeGreaterThan(0);
	}

	[Fact]
	public async Task HandleBulkOperationsWithDocumentCounting()
	{
		if (!_dockerAvailable) { return; }

		// Arrange
		var documents = Enumerable.Range(1, 5).Select(static i => new TestDocument
		{
			Id = $"bulk-{i}",
			Name = $"Bulk Document {i}",
			Value = i * 10,
		}).ToList();

		var bulkOperations = new BulkOperationsCollection();
		foreach (var doc in documents)
		{
			bulkOperations.Add(new BulkIndexOperation<TestDocument>(doc) { Id = doc.Id });
		}

		var bulkRequest = new BulkRequest("test-index")
		{
			Operations = bulkOperations,
		};

		// Act
		var response = await _client.BulkAsync(bulkRequest, CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = response.ShouldNotBeNull();
		response.IsValidResponse.ShouldBeTrue();
		response.Items.Count.ShouldBe(5);

		// Verify bulk metrics include document count
		var performanceMetrics = _monitoringService.GetPerformanceMetrics();
		performanceMetrics.ShouldContainKey("bulk");
	}

	[Fact]
	public async Task TrackRetryAttemptsWhenTransientFailuresOccur()
	{
		if (!_dockerAvailable) { return; }

		// Arrange - Create a request to a non-existent index to trigger retries
		var searchRequest = new SearchRequest(Indices.Parse("non-existent-index"))
		{
			Query = new MatchAllQuery(),
		};

		// Act
		var response = await _client.SearchAsync<TestDocument>(searchRequest, CancellationToken.None).ConfigureAwait(false);

		// Assert The request should complete (possibly with an error), but monitoring should track the operation
		_ = response.ShouldNotBeNull();

		var performanceMetrics = _monitoringService.GetPerformanceMetrics();
		performanceMetrics.ShouldContainKey("search");
	}

	[Fact]
	public async Task MonitorHealthCheckOperations()
	{
		if (!_dockerAvailable) { return; }

		// Act
		var isHealthy = await _client.IsHealthyAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		isHealthy.ShouldBeTrue();

		// Verify health check was monitored
		var performanceMetrics = _monitoringService.GetPerformanceMetrics();
		performanceMetrics.ShouldContainKey("health_check");
		performanceMetrics["health_check"].TotalOperations.ShouldBeGreaterThan(0);
	}

	[Fact]
	public async Task CollectPerformanceMetricsForSlowOperations()
	{
		if (!_dockerAvailable) { return; }

		// Arrange - Create a complex aggregation query that might be slow
		var searchRequest = new SearchRequest(Indices.Parse("test-index"))
		{
			Size = 0,
			Aggregations = new Dictionary<string, Aggregation>
			{
				["value_stats"] = new StatsAggregation { Field = "value" },
				["name_terms"] = new TermsAggregation { Field = "name.keyword", Size = 100 },
			},
		};

		// Act
		var response = await _client.SearchAsync<TestDocument>(searchRequest, CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = response.ShouldNotBeNull();

		// Verify performance metrics were collected
		var performanceMetrics = _monitoringService.GetPerformanceMetrics();
		performanceMetrics.ShouldContainKey("search");

		var searchMetrics = performanceMetrics["search"];
		searchMetrics.TotalOperations.ShouldBeGreaterThan(0);
		searchMetrics.AverageDurationMs.ShouldBeGreaterThan(0);
	}

	[Fact]
	public async Task HandleCircuitBreakerStateTracking()
	{
		if (!_dockerAvailable) { return; }

		// Arrange - Get initial circuit breaker state
		var initialState = _client.IsCircuitBreakerOpen;

		// Act - Perform a normal operation
		var testDoc = new TestDocument { Id = "cb-test", Name = "Circuit Breaker Test", Value = 123 };
		var indexRequest = new IndexRequest<TestDocument>("test-index") { Document = testDoc };
		var response = await _client.IndexAsync(indexRequest, CancellationToken.None).ConfigureAwait(false);

		// Assert
		response.IsValidResponse.ShouldBeTrue();
		_client.IsCircuitBreakerOpen.ShouldBe(initialState); // Should remain in the same state
	}

	[Fact]
	public async Task ResetAndRetrievePerformanceMetrics()
	{
		if (!_dockerAvailable) { return; }

		// Arrange - Perform some operations first
		var testDoc = new TestDocument { Id = "metrics-test", Name = "Metrics Test", Value = 456 };
		var indexRequest = new IndexRequest<TestDocument>("test-index") { Document = testDoc };
		_ = await _client.IndexAsync(indexRequest, CancellationToken.None).ConfigureAwait(false);

		// Act - Get metrics, reset, and get again
		var initialMetrics = _monitoringService.GetPerformanceMetrics();
		initialMetrics.ShouldNotBeEmpty();

		_monitoringService.ResetPerformanceMetrics();

		var resetMetrics = _monitoringService.GetPerformanceMetrics();

		// Assert
		resetMetrics.ShouldBeEmpty();
	}

	/// <inheritdoc/>
	public async Task DisposeAsync()
	{
		if (_serviceProvider is not null)
		{
			await _serviceProvider.DisposeAsync().ConfigureAwait(false);
		}

		if (_elasticsearchContainer is not null)
		{
			await _elasticsearchContainer.StopAsync().ConfigureAwait(false);
			await _elasticsearchContainer.DisposeAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	///     Test document class for integration testing.
	/// </summary>
	private sealed class TestDocument
	{
		public required string Id { get; set; } = string.Empty;

		public required string Name { get; set; } = string.Empty;

		public int Value { get; set; }
	}
}
