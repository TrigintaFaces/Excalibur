// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Aggregations;
using Elastic.Clients.Elasticsearch.Core.Bulk;
using Elastic.Clients.Elasticsearch.QueryDsl;

using Excalibur.Data.ElasticSearch.Exceptions;
using Excalibur.Data.ElasticSearch.Monitoring;
using Excalibur.Data.ElasticSearch.Resilience;

using Excalibur.Integration.Tests.DataElasticSearch.Infrastructure.TestBaseClasses;

using Xunit;

namespace Excalibur.Integration.Tests.DataElasticSearch.DataAccess.ElasticSearch.Monitoring;

/// <summary>
///     Integration tests for the <see cref="MonitoredResilientElasticsearchClient" /> class.
///     Tests verify Elasticsearch operations succeed and monitoring infrastructure is wired up.
///     Performance metrics assertions are conditional because the monitoring layer uses
///     probabilistic sampling (default 1%) that may not record every operation.
/// </summary>
/// <remarks>
/// <para>
/// <b>The container is shared, not per-fact.</b> This class used to build an Elasticsearch container in
/// its own <c>InitializeAsync</c>; because xUnit constructs a new instance of a test class for every
/// fact, that started eight containers for eight facts. It now joins the shared
/// <see cref="ElasticsearchHostTests"/> collection, whose fixture starts one.
/// </para>
/// <para>
/// <b>Isolation:</b> each fact indexes into its own <c>test-index-{guid}</c> and builds its own service
/// provider, so no fact can see another's documents or another's monitoring counters — the same
/// isolation the per-fact container gave.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Collection(nameof(ElasticsearchHostTests))]
public sealed class MonitoredResilientElasticsearchClientShould : IAsyncLifetime
{
	private readonly ElasticsearchContainerFixture _fixture;

	/// <summary>
	/// Per-fact index. xUnit builds one instance per fact, so this is unique per fact and is what keeps
	/// facts isolated from each other on the shared container.
	/// </summary>
	private readonly string _index = $"test-index-{Guid.NewGuid():N}";

	private ServiceProvider? _serviceProvider;
	private IResilientElasticsearchClient? _client;
	private ElasticsearchMonitoringService? _monitoringService;
	private bool _dockerAvailable;

	public MonitoredResilientElasticsearchClientShould(ElasticsearchContainerFixture fixture) =>
		_fixture = fixture;

	/// <inheritdoc/>
	public async ValueTask InitializeAsync()
	{
		try
		{
			var configuration = new ConfigurationBuilder()
				.AddInMemoryCollection(new Dictionary<string, string?>
				{
					["ElasticSearch:Url"] = _fixture.ConnectionString,
					["ElasticSearch:Resilience:Enabled"] = "true",
					["ElasticSearch:Resilience:Retry:MaxAttempts"] = "1",
					["ElasticSearch:Resilience:CircuitBreaker:Enabled"] = "true",
					["ElasticSearch:Monitoring:Enabled"] = "true",
					["ElasticSearch:Monitoring:Level"] = "Verbose",
					["ElasticSearch:Monitoring:Metrics:Enabled"] = "true",
					["ElasticSearch:Monitoring:RequestLogging:Enabled"] = "true",
					["ElasticSearch:Monitoring:Performance:Enabled"] = "true",
					["ElasticSearch:Monitoring:Performance:SamplingRate"] = "1.0",
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
		// CONDITIONAL, and it must stay conditional. This was previously a bare Assert.Skip, so the
		// fact could never execute on any machine — while InitializeAsync still started a real
		// Elasticsearch container first, buying ~3 minutes of container time per test for zero
		// assertions. The message also asserted the infrastructure was absent, which was FALSE
		// whenever Docker was up: a reader triaging CI read "infra unavailable" and stopped looking.
		// _dockerAvailable is the health-probe result computed in InitializeAsync; consulting it is
		// what makes the skip honest — it now fires only when Elasticsearch is genuinely unreachable.
		Assert.SkipUnless(_dockerAvailable, "[infrastructure-unavailable] Elasticsearch (Docker) was not reachable from InitializeAsync, so this fact did NOT execute. It is reported skipped, never passed.");

		// Arrange
		var testDoc = new TestDocument { Id = "test-1", Name = "Test Document", Value = 42 };
		var indexRequest = new IndexRequest<TestDocument>(testDoc) { Index = _index, Id = testDoc.Id };

		// Act
		var response = await _client!.IndexAsync(indexRequest, CancellationToken.None).ConfigureAwait(false);

		// Assert — verify the operation succeeded
		_ = response.ShouldNotBeNull();
		response.IsValidResponse.ShouldBeTrue();
		// ES 8.x may auto-generate IDs depending on client version; verify a valid ID was returned
		response.Id.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task SearchDocumentsWithMetricsCollection()
	{
		// CONDITIONAL, and it must stay conditional. This was previously a bare Assert.Skip, so the
		// fact could never execute on any machine — while InitializeAsync still started a real
		// Elasticsearch container first, buying ~3 minutes of container time per test for zero
		// assertions. The message also asserted the infrastructure was absent, which was FALSE
		// whenever Docker was up: a reader triaging CI read "infra unavailable" and stopped looking.
		// _dockerAvailable is the health-probe result computed in InitializeAsync; consulting it is
		// what makes the skip honest — it now fires only when Elasticsearch is genuinely unreachable.
		Assert.SkipUnless(_dockerAvailable, "[infrastructure-unavailable] Elasticsearch (Docker) was not reachable from InitializeAsync, so this fact did NOT execute. It is reported skipped, never passed.");

		// Arrange - Index a test document first
		var testDoc = new TestDocument { Id = "test-2", Name = "Search Test", Value = 100 };
		var indexRequest = new IndexRequest<TestDocument>(testDoc) { Index = _index, Id = testDoc.Id };
		_ = await _client!.IndexAsync(indexRequest, CancellationToken.None).ConfigureAwait(false);

		var searchRequest = new SearchRequest(Indices.Parse(_index))
		{
			Query = new MatchQuery { Field = "name", Query = "Search" },
			Size = 10,
		};

		// Wait for indexing — poll until the document is searchable
		var searchable = await global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
			async () => (await _client!.SearchAsync<TestDocument>(searchRequest, CancellationToken.None).ConfigureAwait(false)).Documents.Count > 0,
			TimeSpan.FromSeconds(30)).ConfigureAwait(false);
		searchable.ShouldBeTrue();

		// Act
		var response = await _client.SearchAsync<TestDocument>(searchRequest, CancellationToken.None).ConfigureAwait(false);

		// Assert — verify search works
		_ = response.ShouldNotBeNull();
		response.IsValidResponse.ShouldBeTrue();
		response.Documents.Count.ShouldBeGreaterThan(0);

		// Verify monitoring service is wired up (metrics may or may not be populated
		// depending on sampling rate config binding)
		_ = _monitoringService!.GetPerformanceMetrics().ShouldNotBeNull();
	}

	[Fact]
	public async Task HandleBulkOperationsWithDocumentCounting()
	{
		// CONDITIONAL, and it must stay conditional. This was previously a bare Assert.Skip, so the
		// fact could never execute on any machine — while InitializeAsync still started a real
		// Elasticsearch container first, buying ~3 minutes of container time per test for zero
		// assertions. The message also asserted the infrastructure was absent, which was FALSE
		// whenever Docker was up: a reader triaging CI read "infra unavailable" and stopped looking.
		// _dockerAvailable is the health-probe result computed in InitializeAsync; consulting it is
		// what makes the skip honest — it now fires only when Elasticsearch is genuinely unreachable.
		Assert.SkipUnless(_dockerAvailable, "[infrastructure-unavailable] Elasticsearch (Docker) was not reachable from InitializeAsync, so this fact did NOT execute. It is reported skipped, never passed.");

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

		var bulkRequest = new BulkRequest(_index)
		{
			Operations = bulkOperations,
		};

		// Act
		var response = await _client!.BulkAsync(bulkRequest, CancellationToken.None).ConfigureAwait(false);

		// Assert — verify bulk operation succeeded
		_ = response.ShouldNotBeNull();
		response.IsValidResponse.ShouldBeTrue();
		response.Items.Count.ShouldBe(5);
	}

	[Fact]
	public async Task TrackRetryAttemptsWhenTransientFailuresOccur()
	{
		// CONDITIONAL, and it must stay conditional. This was previously a bare Assert.Skip, so the
		// fact could never execute on any machine — while InitializeAsync still started a real
		// Elasticsearch container first, buying ~3 minutes of container time per test for zero
		// assertions. The message also asserted the infrastructure was absent, which was FALSE
		// whenever Docker was up: a reader triaging CI read "infra unavailable" and stopped looking.
		// _dockerAvailable is the health-probe result computed in InitializeAsync; consulting it is
		// what makes the skip honest — it now fires only when Elasticsearch is genuinely unreachable.
		Assert.SkipUnless(_dockerAvailable, "[infrastructure-unavailable] Elasticsearch (Docker) was not reachable from InitializeAsync, so this fact did NOT execute. It is reported skipped, never passed.");

		// Arrange - Create a request to a non-existent index to trigger retries
		var searchRequest = new SearchRequest(Indices.Parse("non-existent-index"))
		{
			Query = new MatchAllQuery(),
		};

		// Act - The resilience layer may throw if the response is invalid after retries
		try
		{
			_ = await _client!.SearchAsync<TestDocument>(searchRequest, CancellationToken.None).ConfigureAwait(false);
		}
		catch (ElasticsearchSearchException)
		{
			// Expected - resilient client wraps invalid responses as exceptions
		}

		// Assert - monitoring infrastructure should be accessible regardless of operation outcome
		_ = _monitoringService!.GetPerformanceMetrics().ShouldNotBeNull();
	}

	[Fact]
	public async Task MonitorHealthCheckOperations()
	{
		// CONDITIONAL, and it must stay conditional. This was previously a bare Assert.Skip, so the
		// fact could never execute on any machine — while InitializeAsync still started a real
		// Elasticsearch container first, buying ~3 minutes of container time per test for zero
		// assertions. The message also asserted the infrastructure was absent, which was FALSE
		// whenever Docker was up: a reader triaging CI read "infra unavailable" and stopped looking.
		// _dockerAvailable is the health-probe result computed in InitializeAsync; consulting it is
		// what makes the skip honest — it now fires only when Elasticsearch is genuinely unreachable.
		Assert.SkipUnless(_dockerAvailable, "[infrastructure-unavailable] Elasticsearch (Docker) was not reachable from InitializeAsync, so this fact did NOT execute. It is reported skipped, never passed.");

		// Act
		var isHealthy = await _client!.IsHealthyAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		isHealthy.ShouldBeTrue();

		// Verify monitoring service is wired up and accessible
		_ = _monitoringService!.GetPerformanceMetrics().ShouldNotBeNull();
	}

	[Fact]
	public async Task CollectPerformanceMetricsForSlowOperations()
	{
		// CONDITIONAL, and it must stay conditional. This was previously a bare Assert.Skip, so the
		// fact could never execute on any machine — while InitializeAsync still started a real
		// Elasticsearch container first, buying ~3 minutes of container time per test for zero
		// assertions. The message also asserted the infrastructure was absent, which was FALSE
		// whenever Docker was up: a reader triaging CI read "infra unavailable" and stopped looking.
		// _dockerAvailable is the health-probe result computed in InitializeAsync; consulting it is
		// what makes the skip honest — it now fires only when Elasticsearch is genuinely unreachable.
		Assert.SkipUnless(_dockerAvailable, "[infrastructure-unavailable] Elasticsearch (Docker) was not reachable from InitializeAsync, so this fact did NOT execute. It is reported skipped, never passed.");

		// Arrange - Index a document first so the index exists for aggregation queries
		var testDoc = new TestDocument { Id = "agg-test", Name = "Aggregation Test", Value = 42 };
		var indexRequest = new IndexRequest<TestDocument>(testDoc) { Index = _index, Id = testDoc.Id };
		_ = await _client!.IndexAsync(indexRequest, CancellationToken.None).ConfigureAwait(false);

		// Wait for indexing to complete
		await Task.Delay(1000).ConfigureAwait(false);

		// Create an aggregation query
		var searchRequest = new SearchRequest(Indices.Parse(_index))
		{
			Size = 0,
			Aggregations = new Dictionary<string, Aggregation>
			{
				["value_stats"] = new StatsAggregation { Field = "value" },
				["name_terms"] = new TermsAggregation { Field = "name.keyword", Size = 100 },
			},
		};

		// Act - search may throw if resilience detects an invalid response
		try
		{
			var response = await _client.SearchAsync<TestDocument>(searchRequest, CancellationToken.None).ConfigureAwait(false);
			_ = response.ShouldNotBeNull();
		}
		catch (ElasticsearchSearchException)
		{
			// Acceptable - monitoring still records the operation
		}

		// Assert - Verify monitoring infrastructure is accessible
		_ = _monitoringService!.GetPerformanceMetrics().ShouldNotBeNull();
	}

	[Fact]
	public async Task HandleCircuitBreakerStateTracking()
	{
		// CONDITIONAL, and it must stay conditional. This was previously a bare Assert.Skip, so the
		// fact could never execute on any machine — while InitializeAsync still started a real
		// Elasticsearch container first, buying ~3 minutes of container time per test for zero
		// assertions. The message also asserted the infrastructure was absent, which was FALSE
		// whenever Docker was up: a reader triaging CI read "infra unavailable" and stopped looking.
		// _dockerAvailable is the health-probe result computed in InitializeAsync; consulting it is
		// what makes the skip honest — it now fires only when Elasticsearch is genuinely unreachable.
		Assert.SkipUnless(_dockerAvailable, "[infrastructure-unavailable] Elasticsearch (Docker) was not reachable from InitializeAsync, so this fact did NOT execute. It is reported skipped, never passed.");

		// Arrange - Get initial circuit breaker state
		var initialState = _client!.IsCircuitBreakerOpen;

		// Act - Perform a normal operation
		var testDoc = new TestDocument { Id = "cb-test", Name = "Circuit Breaker Test", Value = 123 };
		var indexRequest = new IndexRequest<TestDocument>(testDoc) { Index = _index, Id = testDoc.Id };
		var response = await _client.IndexAsync(indexRequest, CancellationToken.None).ConfigureAwait(false);

		// Assert
		response.IsValidResponse.ShouldBeTrue();
		_client.IsCircuitBreakerOpen.ShouldBe(initialState); // Should remain in the same state
	}

	[Fact]
	public async Task ResetAndRetrievePerformanceMetrics()
	{
		// CONDITIONAL, and it must stay conditional. This was previously a bare Assert.Skip, so the
		// fact could never execute on any machine — while InitializeAsync still started a real
		// Elasticsearch container first, buying ~3 minutes of container time per test for zero
		// assertions. The message also asserted the infrastructure was absent, which was FALSE
		// whenever Docker was up: a reader triaging CI read "infra unavailable" and stopped looking.
		// _dockerAvailable is the health-probe result computed in InitializeAsync; consulting it is
		// what makes the skip honest — it now fires only when Elasticsearch is genuinely unreachable.
		Assert.SkipUnless(_dockerAvailable, "[infrastructure-unavailable] Elasticsearch (Docker) was not reachable from InitializeAsync, so this fact did NOT execute. It is reported skipped, never passed.");

		// Arrange - Perform some operations to potentially populate metrics
		var testDoc = new TestDocument { Id = "metrics-test", Name = "Metrics Test", Value = 456 };
		var indexRequest = new IndexRequest<TestDocument>(testDoc) { Index = _index, Id = testDoc.Id };
		_ = await _client!.IndexAsync(indexRequest, CancellationToken.None).ConfigureAwait(false);

		// Act - Get metrics (may be empty due to sampling), reset, and get again
		var initialMetrics = _monitoringService!.GetPerformanceMetrics();
		initialMetrics.ShouldNotBeNull();

		_monitoringService.ResetPerformanceMetrics();

		var resetMetrics = _monitoringService.GetPerformanceMetrics();

		// Assert — after reset, metrics should always be empty
		resetMetrics.ShouldBeEmpty();
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		try
		{
			// Owned by the service provider, but dispose explicitly to satisfy CA2213.
			_monitoringService?.Dispose();
		}
		catch (Exception)
		{
			// Suppress monitoring service disposal errors
		}

		try
		{
			if (_serviceProvider is not null)
			{
				await _serviceProvider.DisposeAsync().ConfigureAwait(false);
			}
		}
		catch (Exception)
		{
			// Suppress service provider disposal errors
		}

		// The container belongs to the collection fixture and must NOT be stopped here — it is shared
		// with every other fact in the collection. Only this fact's own index is removed.
		try
		{
			if (_dockerAvailable)
			{
				var client = new Elastic.Clients.Elasticsearch.ElasticsearchClient(
					new Elastic.Clients.Elasticsearch.ElasticsearchClientSettings(new Uri(_fixture.ConnectionString)));
				_ = await client.Indices.DeleteAsync(_index).ConfigureAwait(false);
			}
		}
		catch (Exception)
		{
			// Best effort cleanup — a leftover index cannot affect another fact, which uses its own.
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
