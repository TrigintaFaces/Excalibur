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
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
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

		var clientSettings = new ElasticsearchClientSettings(new Uri(_fixture.ConnectionString))
			.ServerCertificateValidationCallback((_, _, _, _) => true);
		_client = new ElasticsearchClient(clientSettings);

		var options = CreateResilienceSettings();
		var pipeline = new ElasticsearchResiliencePipeline(
			Microsoft.Extensions.Options.Options.Create(options.Value.Resilience));
		var circuitBreaker = new PollyElasticsearchCircuitBreaker(pipeline);

		_resilientClient = new ResilientElasticsearchClient(
			_client,
			pipeline,
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

		var indexRequest = new IndexRequest<TestDocument>(document) { Index = indexName, Id = document.Id };

		// Act
		var response = await _resilientClient.IndexAsync(indexRequest, CancellationToken.None).ConfigureAwait(false);

		// Assert
		response.IsValidResponse.ShouldBeTrue();
		response.Id.ShouldBe(document.Id);
	}

	[Fact]
	public async Task SurfaceTimeoutAsSearchExceptionRatherThanRawCancellation()
	{
		// Arrange - an already-elapsed timeout budget. Using zero (rather than racing a ~1ms timer against a
		// real search) makes this deterministic: the timeout token is cancelled before the operation starts,
		// so the outcome does not depend on machine speed or OS timer resolution.
		var settings = CreateResilienceSettings(
			retryMaxAttempts: 2,
			searchTimeoutSeconds: 0);

		var pipeline = new ElasticsearchResiliencePipeline(
			Microsoft.Extensions.Options.Options.Create(settings.Value.Resilience));
		var circuitBreaker = new PollyElasticsearchCircuitBreaker(pipeline);

		using var resilientClient = new ResilientElasticsearchClient(
			_client,
			pipeline,
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

		// Act & Assert - a timeout the caller did not request must surface as the operation's domain exception,
		// carrying a TimeoutException, so callers can tell "it timed out" from "I cancelled it".
		var thrown = await Should
			.ThrowAsync<ElasticsearchSearchException>(() => resilientClient.SearchAsync<TestDocument>(searchRequest, CancellationToken.None))
			.ConfigureAwait(false);

		_ = thrown.InnerException.ShouldBeOfType<TimeoutException>();
	}

	/// <summary>
	/// Liveness counterpart to <see cref="SurfaceTimeoutAsSearchExceptionRatherThanRawCancellation" />. Without this
	/// arm, a client that wrapped *every* cancellation - including the caller's own - would still pass. Caller
	/// cancellation must propagate unchanged rather than being reported as a timeout.
	/// </summary>
	[Fact]
	public async Task PropagateCallerCancellationInsteadOfReportingItAsATimeout()
	{
		// Arrange - a generous timeout, so the only cancellation in play is the caller's own.
		var settings = CreateResilienceSettings(retryMaxAttempts: 2, searchTimeoutSeconds: 30);

		var pipeline = new ElasticsearchResiliencePipeline(
			Microsoft.Extensions.Options.Options.Create(settings.Value.Resilience));
		var circuitBreaker = new PollyElasticsearchCircuitBreaker(pipeline);

		using var resilientClient = new ResilientElasticsearchClient(
			_client,
			pipeline,
			circuitBreaker,
			settings,
			_logger);

		const string indexName = "test-resilience-caller-cancel";
		await CreateTestIndex(indexName).ConfigureAwait(false);

		var searchRequest = new SearchRequest(Indices.Parse(indexName)) { Query = new MatchAllQuery() };

		using var callerCancellation = new CancellationTokenSource();
		await callerCancellation.CancelAsync().ConfigureAwait(false);

		// Act & Assert - the caller's cancellation is honored verbatim, NOT translated into a search failure.
		_ = await Should
			.ThrowAsync<OperationCanceledException>(
				() => resilientClient.SearchAsync<TestDocument>(searchRequest, callerCancellation.Token))
			.ConfigureAwait(false);
	}

	[Fact]
	public async Task OpenCircuitBreakerAfterConsecutiveFailures()
	{
		// Arrange - point a real ElasticsearchClient at an unreachable endpoint
		// so every SearchAsync fails with TransportException (connection
		// refused). Replaces the former FakeItEasy-based simulated failure
		// per ADR-142 §D7 / S799 SdkFake debt-drain — the circuit breaker
		// contract is behaviorally identical (repeated transport failures
		// trip the breaker), and using a real client exercises the actual
		// Elastic.Transport failure-propagation path.
		var failingClientSettings = new ElasticsearchClientSettings(new Uri("http://127.0.0.1:1"))
			.RequestTimeout(TimeSpan.FromMilliseconds(250))
			.MaximumRetries(0);
		var failingClient = new ElasticsearchClient(failingClientSettings);

		var settings = CreateResilienceSettings(
			circuitBreakerMinimumThroughput: 2,
			retryMaxAttempts: 1); // Minimal retries to speed up test

		var pipeline = new ElasticsearchResiliencePipeline(
			Microsoft.Extensions.Options.Options.Create(settings.Value.Resilience));
		var circuitBreaker = new PollyElasticsearchCircuitBreaker(pipeline);

		using var resilientClient = new ResilientElasticsearchClient(
			failingClient,
			pipeline,
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

		_ = await _resilientClient.IndexAsync(new IndexRequest<TestDocument>(document) { Index = indexName, Id = documentId }, CancellationToken.None)
			.ConfigureAwait(false);

		var getRequest = new GetRequest(indexName, documentId);

		// Wait for indexing to complete — poll until the document is retrievable
		var indexed = await global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
			async () => (await _resilientClient.GetAsync<TestDocument>(getRequest, CancellationToken.None).ConfigureAwait(false)).Found,
			TimeSpan.FromSeconds(30)).ConfigureAwait(false);
		indexed.ShouldBeTrue();

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

		_ = await _resilientClient.IndexAsync(new IndexRequest<TestDocument>(document) { Index = indexName, Id = documentId }, CancellationToken.None)
			.ConfigureAwait(false);

		// Wait for indexing to complete — poll until the document is retrievable
		var indexed = await global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
			async () => (await _resilientClient.GetAsync<TestDocument>(new GetRequest(indexName, documentId), CancellationToken.None).ConfigureAwait(false)).Found,
			TimeSpan.FromSeconds(30)).ConfigureAwait(false);
		indexed.ShouldBeTrue();

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

		_ = await _resilientClient.IndexAsync(new IndexRequest<TestDocument>(document) { Index = indexName, Id = documentId }, CancellationToken.None)
			.ConfigureAwait(false);

		// Wait for indexing to complete — poll until the document is retrievable
		var indexed = await global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
			async () => (await _resilientClient.GetAsync<TestDocument>(new GetRequest(indexName, documentId), CancellationToken.None).ConfigureAwait(false)).Found,
			TimeSpan.FromSeconds(30)).ConfigureAwait(false);
		indexed.ShouldBeTrue();

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
		int circuitBreakerMinimumThroughput = 5)
	{
		var config = new ElasticsearchConfigurationOptions
		{
			Resilience = new ElasticsearchResilienceOptions
			{
				Enabled = true,
				Retry =
					new ElasticSearchRetryPolicyOptions
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
						MinimumThroughput = circuitBreakerMinimumThroughput,
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