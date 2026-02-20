// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Bulk;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Elastic.Transport;

using Excalibur.Data.ElasticSearch;
using Excalibur.Data.ElasticSearch.Exceptions;
using Excalibur.Data.ElasticSearch.Resilience;
using Excalibur.Integration.Tests.DataElasticSearch.Infrastructure.TestBaseClasses;

using FakeItEasy;

using Microsoft.Extensions.Options;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.DataElasticSearch.DataAccess.ElasticSearch.Resilience;

[Collection(nameof(ElasticsearchHostTests))]
public sealed class ResilientElasticsearchClientShould : IDisposable
{
	private readonly ElasticsearchContainerFixture _fixture;
	private readonly ElasticsearchClient _client;
	private readonly ResilientElasticsearchClient _resilientClient;
	private readonly ILogger<ResilientElasticsearchClient> _logger;

	public ResilientElasticsearchClientShould(ElasticsearchContainerFixture fixture)
	{
		_fixture = fixture;
		_logger = A.Fake<ILogger<ResilientElasticsearchClient>>();

		var clientSettings = new ElasticsearchClientSettings(new Uri(_fixture.ConnectionString));
		_client = new ElasticsearchClient(clientSettings);

		var options = CreateResilienceSettings();
		var retryPolicy = new ElasticsearchRetryPolicy(options);
		var circuitBreaker = new ElasticsearchCircuitBreaker(
			options,
			A.Fake<ILogger<ElasticsearchCircuitBreaker>>());

		_resilientClient = new ResilientElasticsearchClient(
			_client,
			retryPolicy,
			circuitBreaker,
			options,
			_logger);
	}

	[Fact]
	public async Task ExecuteSearchSuccessfullyWhenServiceIsHealthy()
	{
		// Arrange
		const string indexName = "test-resilience-search";
		await CreateTestIndex(indexName).ConfigureAwait(false);

		var searchRequest = new SearchRequest(Indices.Parse(indexName))
		{
			Query = new MatchAllQuery(),
			Size = 10,
		};

		// Act
		var response = await _resilientClient.SearchAsync<TestDocument>(searchRequest, CancellationToken.None).ConfigureAwait(false);

		// Assert
		response.IsValidResponse.ShouldBeTrue();
		_resilientClient.IsCircuitBreakerOpen.ShouldBeFalse();
	}

	[Fact]
	public async Task ExecuteIndexOperationSuccessfully()
	{
		// Arrange
		const string indexName = "test-resilience-index";
		await CreateTestIndex(indexName).ConfigureAwait(false);

		var document = new TestDocument { Id = Guid.NewGuid().ToString(), Name = "Test Document", CreatedAt = DateTime.UtcNow };

		var indexRequest = new IndexRequest<TestDocument>(indexName, document.Id) { Document = document };

		// Act
		var response = await _resilientClient.IndexAsync(indexRequest, CancellationToken.None).ConfigureAwait(false);

		// Assert
		response.IsValidResponse.ShouldBeTrue();
		response.Id.ShouldBe(document.Id);
	}

	[Fact]
	public async Task HandleTimeoutGracefullyWithRetries()
	{
		// Arrange - use very short timeout to trigger timeout exceptions
		var settings = CreateResilienceSettings(
			retryMaxAttempts: 2,
			searchTimeoutSeconds: 0.001); // Very short timeout

		var retryPolicy = new ElasticsearchRetryPolicy(settings);
		var circuitBreaker = new ElasticsearchCircuitBreaker(
			settings,
			A.Fake<ILogger<ElasticsearchCircuitBreaker>>());

		using var resilientClient = new ResilientElasticsearchClient(
			_client,
			retryPolicy,
			circuitBreaker,
			settings,
			_logger);

		const string indexName = "test-resilience-timeout";
		await CreateTestIndex(indexName).ConfigureAwait(false);

		var searchRequest = new SearchRequest(Indices.Parse(indexName))
		{
			Query = new MatchAllQuery(),
			Size = 1000, // Large result set to increase processing time
		};

		// Act & Assert - operation should eventually fail after retries
		_ = await Should.ThrowAsync<ElasticsearchSearchException>(() => resilientClient.SearchAsync<TestDocument>(searchRequest, CancellationToken.None))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task OpenCircuitBreakerAfterConsecutiveFailures()
	{
		// Arrange - mock a client that always fails
		var failingClient = A.Fake<ElasticsearchClient>();
		_ = A.CallTo(() => failingClient.SearchAsync<TestDocument>(A<SearchRequest>._, A<CancellationToken>._))
				.Throws(new TransportException("Simulated transport failure."));

		var settings = CreateResilienceSettings(
			circuitBreakerFailureThreshold: 2,
			retryMaxAttempts: 1); // Minimal retries to speed up test

		var retryPolicy = new ElasticsearchRetryPolicy(settings);
		var circuitBreaker = new ElasticsearchCircuitBreaker(
			settings,
			A.Fake<ILogger<ElasticsearchCircuitBreaker>>());

		using var resilientClient = new ResilientElasticsearchClient(
			failingClient,
			retryPolicy,
			circuitBreaker,
			settings,
			_logger);

		var searchRequest = new SearchRequest(Indices.Parse("test-circuit-breaker"))
		{
			Query = new MatchAllQuery(),
		};

		// Act - trigger failures to open circuit breaker
		_ = await Should.ThrowAsync<ElasticsearchSearchException>(() => resilientClient.SearchAsync<TestDocument>(searchRequest, CancellationToken.None))
			.ConfigureAwait(false);

		_ = await Should.ThrowAsync<ElasticsearchSearchException>(() => resilientClient.SearchAsync<TestDocument>(searchRequest, CancellationToken.None))
			.ConfigureAwait(false);

		// Assert - circuit breaker should be open
		resilientClient.IsCircuitBreakerOpen.ShouldBeTrue();

		// Further requests should be blocked immediately
		_ = await Should.ThrowAsync<InvalidOperationException>(() => resilientClient.SearchAsync<TestDocument>(searchRequest, CancellationToken.None))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task HandleBulkOperationsWithResilience()
	{
		// Arrange
		const string indexName = "test-resilience-bulk";
		await CreateTestIndex(indexName).ConfigureAwait(false);

		var documents = Enumerable.Range(1, 5)
			.Select(static i => new TestDocument { Id = i.ToString(), Name = $"Test Document {i}", CreatedAt = DateTime.UtcNow })
			.ToList();

		var bulkRequest = new BulkRequest(indexName)
		{
			Operations = documents.Select(static doc =>
				new BulkIndexOperation<TestDocument>(doc) { Id = doc.Id }
			).Cast<IBulkOperation>().ToList(),
		};

		// Act
		var response = await _resilientClient.BulkAsync(bulkRequest, CancellationToken.None).ConfigureAwait(false);

		// Assert
		response.IsValidResponse.ShouldBeTrue();
		response.Errors.ShouldBeFalse();
		response.Items.Count.ShouldBe(5);
	}

	[Fact]
	public async Task PerformHealthCheckCorrectly()
	{
		// Act
		var isHealthy = await _resilientClient.IsHealthyAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		isHealthy.ShouldBeTrue();
	}

	[Fact]
	public async Task HandleGetOperationWithResilience()
	{
		// Arrange
		const string indexName = "test-resilience-get";
		const string documentId = "test-doc-1";

		await CreateTestIndex(indexName).ConfigureAwait(false);

		// First, index a document
		var document = new TestDocument { Id = documentId, Name = "Test Document for Get", CreatedAt = DateTime.UtcNow };

		_ = await _resilientClient.IndexAsync(new IndexRequest<TestDocument>(indexName, documentId) { Document = document }, CancellationToken.None)
			.ConfigureAwait(false);

		// Wait for indexing to complete
		await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);

		var getRequest = new GetRequest(indexName, documentId);

		// Act
		var response = await _resilientClient.GetAsync<TestDocument>(getRequest, CancellationToken.None).ConfigureAwait(false);

		// Assert
		response.IsValidResponse.ShouldBeTrue();
		response.Found.ShouldBeTrue();
		response.Source?.Name.ShouldBe("Test Document for Get");
	}

	[Fact]
	public async Task HandleUpdateOperationWithResilience()
	{
		// Arrange
		const string indexName = "test-resilience-update";
		const string documentId = "test-doc-1";

		await CreateTestIndex(indexName).ConfigureAwait(false);

		// First, index a document
		var document = new TestDocument { Id = documentId, Name = "Original Name", CreatedAt = DateTime.UtcNow };

		_ = await _resilientClient.IndexAsync(new IndexRequest<TestDocument>(indexName, documentId) { Document = document }, CancellationToken.None)
			.ConfigureAwait(false);

		// Wait for indexing to complete
		await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);

		var updateRequest = new UpdateRequest<TestDocument, object>(indexName, documentId) { Doc = new { Name = "Updated Name" } };

		// Act
		var response = await _resilientClient.UpdateAsync(updateRequest, CancellationToken.None).ConfigureAwait(false);

		// Assert
		response.IsValidResponse.ShouldBeTrue();
		response.Result.ShouldBe(Result.Updated);
	}

	[Fact]
	public async Task HandleDeleteOperationWithResilience()
	{
		// Arrange
		const string indexName = "test-resilience-delete";
		const string documentId = "test-doc-1";

		await CreateTestIndex(indexName).ConfigureAwait(false);

		// First, index a document
		var document = new TestDocument { Id = documentId, Name = "Document to Delete", CreatedAt = DateTime.UtcNow };

		_ = await _resilientClient.IndexAsync(new IndexRequest<TestDocument>(indexName, documentId) { Document = document }, CancellationToken.None)
			.ConfigureAwait(false);

		// Wait for indexing to complete
		await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);

		var deleteRequest = new DeleteRequest(indexName, documentId);

		// Act
		var response = await _resilientClient.DeleteAsync(deleteRequest, CancellationToken.None).ConfigureAwait(false);

		// Assert
		response.IsValidResponse.ShouldBeTrue();
		response.Result.ShouldBe(Result.Deleted);
	}

	[Fact]
	public void ThrowWhenDisposed()
	{
		// Arrange
		_resilientClient.Dispose();

		// Act & Assert
		_ = Should.Throw<ObjectDisposedException>(() =>
				_resilientClient.SearchAsync<TestDocument>(new SearchRequest(Indices.Parse("test-disposed")), CancellationToken.None));
	}

	/// <inheritdoc/>
	public void Dispose() => _resilientClient?.Dispose();

	private static IOptions<ElasticsearchConfigurationOptions> CreateResilienceSettings(
		int retryMaxAttempts = 3,
		double searchTimeoutSeconds = 30,
		int circuitBreakerFailureThreshold = 5)
	{
		var config = new ElasticsearchConfigurationOptions
		{
			Resilience = new ElasticsearchResilienceOptions
			{
				Enabled = true,
				Retry =
					new RetryPolicyOptions
					{
						Enabled = true,
						MaxAttempts = retryMaxAttempts,
						BaseDelay = TimeSpan.FromMilliseconds(100),
						MaxDelay = TimeSpan.FromSeconds(5),
						UseExponentialBackoff = true,
						JitterFactor = 0.1,
					},
				CircuitBreaker =
					new CircuitBreakerOptions
					{
						Enabled = true,
						FailureThreshold = circuitBreakerFailureThreshold,
						MinimumThroughput = 3,
						BreakDuration = TimeSpan.FromSeconds(5),
						SamplingDuration = TimeSpan.FromSeconds(30),
						FailureRateThreshold = 0.5,
					},
				Timeouts = new TimeoutOptions
				{
					SearchTimeout = TimeSpan.FromSeconds(searchTimeoutSeconds),
					IndexTimeout = TimeSpan.FromSeconds(60),
					BulkTimeout = TimeSpan.FromSeconds(120),
					DeleteTimeout = TimeSpan.FromSeconds(30),
				},
			},
		};

		return Microsoft.Extensions.Options.Options.Create(config);
	}

	private async Task CreateTestIndex(string indexName)
	{
		// Delete index if it exists
		_ = await _client.Indices.DeleteAsync(indexName).ConfigureAwait(false);

		// Create index with mapping
		_ = await _client.Indices.CreateAsync(indexName, static c => c
				.Mappings(static m => m
						.Properties<TestDocument>(static p => p
								.Keyword(static k => k.Id)
					.Text(static t => t.Name)
					.Date(static d => d.CreatedAt))));
	}

	private sealed class TestDocument
	{
		public required string Id { get; init; }

		public required string Name { get; init; }

		public DateTime CreatedAt { get; init; }
	}
}
