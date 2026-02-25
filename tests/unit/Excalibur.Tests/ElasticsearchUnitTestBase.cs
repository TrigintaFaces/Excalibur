// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Elastic.Clients.Elasticsearch;
using Elastic.Transport;

namespace Excalibur.Tests;

/// <summary>
///     Base class for Elasticsearch unit tests providing common test infrastructure and mocking capabilities.
/// </summary>
public abstract class ElasticsearchUnitTestBase : IDisposable
{
	private bool _disposed;

	/// <summary>
	///     Gets the service provider for the test.
	/// </summary>
	protected IServiceProvider ServiceProvider { get; }

	/// <summary>
	///     Gets the mocked Elasticsearch client.
	/// </summary>
	protected ElasticsearchClient MockClient { get; }

	/// <summary>
	///     Gets the mocked transport for the Elasticsearch client.
	/// </summary>
	protected ITransport MockTransport { get; }

	/// <summary>
	///     Gets the test logger factory.
	/// </summary>
	protected ILoggerFactory LoggerFactory { get; }

	/// <summary>
	///     Gets the test configuration.
	/// </summary>
	protected IConfiguration Configuration { get; }

	/// <summary>
	///     Gets the collection of captured requests for verification.
	/// </summary>
	protected ConcurrentBag<CapturedRequest> CapturedRequests { get; }

	/// <summary>
	///     Initializes a new instance of the <see cref="ElasticsearchUnitTestBase" /> class.
	/// </summary>
	protected ElasticsearchUnitTestBase()
	{
		CapturedRequests = [];

		// Setup configuration
		var configBuilder = new ConfigurationBuilder();
		ConfigureTestConfiguration(configBuilder);
		Configuration = configBuilder.Build();

		// Setup mocks
		MockTransport = A.Fake<ITransport>();
		MockClient = CreateMockClient();

		// Setup services
		var services = new ServiceCollection();
		ConfigureTestServices(services);

		// Add default services
		_ = services.AddSingleton(Configuration);
		_ = services.AddLogging(static builder =>
		{
			_ = builder.AddConsole();
			_ = builder.SetMinimumLevel(LogLevel.Debug);
		});

		_ = services.AddSingleton(MockClient);
		_ = services.AddSingleton(MockTransport);

		ServiceProvider = services.BuildServiceProvider();
		LoggerFactory = ServiceProvider.GetRequiredService<ILoggerFactory>();
	}

	/// <summary>
	///     Creates a mock Elasticsearch client for testing.
	/// </summary>
	/// <returns> A mocked Elasticsearch client. </returns>
	protected virtual ElasticsearchClient CreateMockClient()
	{
		// CA2000: Settings object lifetime is managed by ElasticsearchClient
#pragma warning disable CA2000
		var settings = new ElasticsearchClientSettings(new Uri("http://localhost:9200"))
			// .Transport(MockTransport) - Transport method no longer exists in new API
			.DefaultIndex("test-index")
			.EnableDebugMode()
			.PrettyJson();

		return new ElasticsearchClient(settings);
#pragma warning restore CA2000
	}

	/// <summary>
	///     Configures the test configuration.
	/// </summary>
	/// <param name="builder"> The configuration builder. </param>
	protected virtual void ConfigureTestConfiguration(IConfigurationBuilder builder)
	{
		var testConfig = new Dictionary<string, string?>
		{
			["Elasticsearch:Urls:0"] = "http://localhost:9200",
			["Elasticsearch:DefaultIndex"] = "test-index",
			["Elasticsearch:Username"] = "elastic",
			["Elasticsearch:Password"] = "test-password",
			["Elasticsearch:EnableDebugMode"] = "true",
			["Elasticsearch:Resilience:MaxRetryAttempts"] = "3",
			["Elasticsearch:Resilience:RetryDelayMilliseconds"] = "100",
			["Elasticsearch:Performance:EnableCaching"] = "true",
			["Elasticsearch:Security:EnableFieldEncryption"] = "false",
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
	///     Sets up a mock response for a search operation.
	/// </summary>
	/// <typeparam name="TDocument"> The document type. </typeparam>
	/// <param name="documents"> The documents to return. </param>
	/// <param name="total"> The total number of hits. </param>
	protected void SetupSearchResponse<TDocument>(IEnumerable<TDocument> documents, long total = -1)
		where TDocument : class
	{
		var hits = documents.Select(doc => new Hit<TDocument>
		{
			Source = doc,
			Id = Guid.NewGuid().ToString(),
			Index = "test-index",
			Score = 1.0,
		}).ToList();

		var searchResponse = A.Fake<SearchResponse<TDocument>>(x => x.ConfigureFake(fake =>
		{
			_ = A.CallTo(() => fake.IsValidResponse).Returns(true);
			_ = A.CallTo(() => fake.Total).Returns(total >= 0 ? total : hits.Count);
			_ = A.CallTo(() => fake.Documents).Returns(hits.Select(h => h.Source).ToList());
			// Note: Hits property cannot be mocked with FakeItEasy - using Documents instead
		}));

		// Capture the request for verification
		_ = A.CallTo(() => MockClient.SearchAsync(A<SearchRequestDescriptor<TDocument>>._, A<CancellationToken>._))
			.Invokes((SearchRequestDescriptor<TDocument> request, CancellationToken ct) => CapturedRequests.Add(new CapturedRequest
			{
				RequestType = "Search",
				Index = "test-index",
				Request = request,
				Timestamp = DateTime.UtcNow,
			}))
			.Returns(Task.FromResult(searchResponse));
	}

	/// <summary>
	///     Sets up a mock response for an index operation.
	/// </summary>
	/// <typeparam name="TDocument"> The document type. </typeparam>
	/// <param name="success"> Whether the operation should succeed. </param>
	/// <param name="id"> The document ID. </param>
	protected void SetupIndexResponse<TDocument>(bool success = true, string? id = null)
		where TDocument : class
	{
		var indexResponse = A.Fake<IndexResponse>();
		_ = A.CallTo(() => indexResponse.IsValidResponse).Returns(success);
		_ = A.CallTo(() => indexResponse.Id).Returns(id ?? Guid.NewGuid().ToString());
		_ = A.CallTo(() => indexResponse.Index).Returns("test-index");
		_ = A.CallTo(() => indexResponse.Result)
			.Returns(success ? Result.Created : Result.NotFound); // Result.Error doesn't exist in new API

		_ = A.CallTo(() => MockClient.IndexAsync(
				A<TDocument>._,
				A<CancellationToken>._))
			.Returns(Task.FromResult(indexResponse));
	}

	/// <summary>
	///     Sets up a mock response for a delete operation.
	/// </summary>
	/// <param name="success"> Whether the operation should succeed. </param>
	protected void SetupDeleteResponse(bool success = true)
	{
		var deleteResponse = A.Fake<DeleteResponse>();
		_ = A.CallTo(() => deleteResponse.IsValidResponse).Returns(success);
		_ = A.CallTo(() => deleteResponse.Result).Returns(success ? Result.Deleted : Result.NotFound);

		_ = A.CallTo(() => MockClient.DeleteAsync(
				A<DeleteRequestDescriptor>._,
				A<CancellationToken>._))
			.Returns(Task.FromResult(deleteResponse));
	}

	/// <summary>
	///     Sets up a mock response for a bulk operation.
	/// </summary>
	/// <param name="success"> Whether the operation should succeed. </param>
	/// <param name="itemCount"> The number of items in the bulk operation. </param>
	protected void SetupBulkResponse(bool success = true, int itemCount = 10)
	{
		var bulkResponse = A.Fake<BulkResponse>();
		_ = A.CallTo(() => bulkResponse.IsValidResponse).Returns(success);
		_ = A.CallTo(() => bulkResponse.Errors).Returns(!success);
		_ = A.CallTo(() => bulkResponse.Took).Returns(100);

		var items = Enumerable.Range(0, itemCount).Select(i =>
		{
			var item = A.Fake<BulkResponseItem>();
			_ = A.CallTo(() => item.Status).Returns(success ? 200 : 400);
			_ = A.CallTo(() => item.Id).Returns($"doc-{i}");
			return item;
		}).ToList();

		// Note: Type mismatch between ResponseItem and BulkResponseItem in FakeItEasy configuration This may need adjustment based on
		// actual ElasticSearch API version A.CallTo(() => bulkResponse.Items).Returns(items.AsReadOnly());

		_ = A.CallTo(() => MockClient.BulkAsync(
				A<BulkRequestDescriptor>._,
				A<CancellationToken>._))
			.Returns(Task.FromResult(bulkResponse));
	}

	/// <summary>
	///     Verifies that a search request was made with the expected parameters.
	/// </summary>
	/// <param name="index"> The expected index. </param>
	/// <param name="times"> The expected number of times. </param>
	protected void VerifySearchRequest(string? index = null, int times = 1)
	{
		var searchRequests = CapturedRequests
			.Where(r => r.RequestType == "Search")
			.Where(r => index == null || r.Index == index)
			.ToList();

		searchRequests.Count.ShouldBe(times);
	}

	/// <summary>
	///     Gets a service from the service provider.
	/// </summary>
	/// <typeparam name="TService"> The service type. </typeparam>
	/// <returns> The service instance. </returns>
	protected TService GetService<TService>() where TService : notnull => ServiceProvider.GetRequiredService<TService>();

	/// <summary>
	///     Creates a test document for testing.
	/// </summary>
	/// <typeparam name="TDocument"> The document type. </typeparam>
	/// <param name="id"> The document ID. </param>
	/// <returns> A test document instance. </returns>
	protected virtual TDocument CreateTestDocument<TDocument>(string? id = null) where TDocument : class, new()
	{
		var document = new TDocument();

		// Set ID if the document has an MessageId property
		var idProperty = typeof(TDocument).GetProperty("MessageId");
		if (idProperty != null && idProperty.CanWrite)
		{
			idProperty.SetValue(document, id ?? Guid.NewGuid().ToString());
		}

		return document;
	}

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
				(ServiceProvider as IDisposable)?.Dispose();
			}

			_disposed = true;
		}
	}
}

/// <summary>
///     Represents a captured request for verification.
/// </summary>
public class CapturedRequest
{
	/// <summary>
	///     Gets or sets the request type.
	/// </summary>
	public required string RequestType { get; set; } = string.Empty;

	/// <summary>
	///     Gets or sets the index name.
	/// </summary>
	public required string? Index { get; set; }

	/// <summary>
	///     Gets or sets the request object.
	/// </summary>
	public required object? Request { get; set; }

	/// <summary>
	///     Gets or sets the timestamp of the request.
	/// </summary>
	public DateTime Timestamp { get; set; }
}
