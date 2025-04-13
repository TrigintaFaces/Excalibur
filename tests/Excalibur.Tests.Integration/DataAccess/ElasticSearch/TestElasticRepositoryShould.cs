using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Elastic.Transport;

using Excalibur.DataAccess.ElasticSearch;
using Excalibur.Tests.Fixtures;
using Excalibur.Tests.Infrastructure.TestBaseClasses.PersistenceOnly;
using Excalibur.Tests.Mothers;
using Excalibur.Tests.Shared;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit.Abstractions;

namespace Excalibur.Tests.Integration.DataAccess.ElasticSearch;

public class TestElasticRepositoryShould(ElasticsearchContainerFixture fixture, ITestOutputHelper output)
	: ElasticsearchPersistenceOnlyTestBase(fixture, output)
{
	[Fact]
	public async Task AddOrUpdateShouldIndexDocument()
	{
		var _repository = GetRequiredService<ITestElasticRepository>();
		var doc = ElasticSearchMother.CreateTestDocument();
		_ = await _repository.AddOrUpdateAsync(doc.Id, doc).ConfigureAwait(true);
		var result = await _repository.GetByIdAsync(doc.Id).ConfigureAwait(true);
		_ = result.ShouldNotBeNull();
		result!.Id.ShouldBe(doc.Id);
	}

	[Fact]
	public async Task GetByIdShouldReturnNullIfNotFound()
	{
		var _repository = GetRequiredService<ITestElasticRepository>();
		var result = await _repository.GetByIdAsync("nonexistent-id").ConfigureAwait(true);
		result.ShouldBeNull();
	}

	[Fact]
	public async Task RemoveShouldDeleteDocument()
	{
		var _repository = GetRequiredService<ITestElasticRepository>();
		var doc = ElasticSearchMother.CreateTestDocument();
		_ = await _repository.AddOrUpdateAsync(doc.Id, doc).ConfigureAwait(true);
		var deleted = await _repository.RemoveAsync(doc.Id).ConfigureAwait(true);
		deleted.ShouldBeTrue();
		var result = await _repository.GetByIdAsync(doc.Id).ConfigureAwait(true);
		result.ShouldBeNull();
	}

	[Fact]
	public async Task BulkAddOrUpdateShouldIndexMultipleDocuments()
	{
		var _repository = GetRequiredService<ITestElasticRepository>();
		var docs = ElasticSearchMother.CreateManyTestDocuments(5).ToList();
		_ = await _repository.BulkAddOrUpdateAsync(docs, x => x.Id).ConfigureAwait(true);
		foreach (var doc in docs)
		{
			var fetched = await _repository.GetByIdAsync(doc.Id).ConfigureAwait(true);
			_ = fetched.ShouldNotBeNull();
			fetched!.Name.ShouldBe(doc.Name);
		}
	}

	[Fact]
	public async Task UpdateShouldModifyFields()
	{
		var _repository = GetRequiredService<ITestElasticRepository>();
		var doc = ElasticSearchMother.CreateTestDocument();
		_ = await _repository.AddOrUpdateAsync(doc.Id, doc).ConfigureAwait(true);
		var updated = new Dictionary<string, object> { ["name"] = "Updated Name" };
		_ = await _repository.UpdateAsync(doc.Id, updated).ConfigureAwait(true);
		var fetched = await _repository.GetByIdAsync(doc.Id).ConfigureAwait(true);
		fetched!.Name.ShouldBe("Updated Name");
	}

	[Fact]
	public async Task SearchShouldReturnExpectedResults()
	{
		var _repository = GetRequiredService<ITestElasticRepository>();
		var doc = ElasticSearchMother.CreateTestDocument(name: "UniqueName");
		_ = await _repository.AddOrUpdateAsync(doc.Id, doc).ConfigureAwait(true);

		var request = new SearchRequest<TestElasticDocument>("test-elastic-index")
		{
			Query = new MatchQuery("name") { Query = "UniqueName" }
		};

		var result = await _repository.SearchAsync(request).ConfigureAwait(true);
		result.Documents.ShouldContain(x => x.Id == doc.Id);
	}

	[Fact]
	public async Task GetByIdShouldReturnNullIfDocumentNotFound()
	{
		var _repository = GetRequiredService<ITestElasticRepository>();
		var result = await _repository.GetByIdAsync("nonexistent-id").ConfigureAwait(true);
		result.ShouldBeNull();
	}

	protected override void AddServices(IServiceCollection services, IConfiguration configuration)
	{
		var uri = new UriBuilder(Fixture.ConnectionString) { Scheme = "http", Port = new Uri(Fixture.ConnectionString).Port }.Uri;
#pragma warning disable CA2000 // Dispose objects before losing scope
		var client = new ElasticsearchClient(new ElasticsearchClientSettings(uri)
			.ServerCertificateValidationCallback(CertificateValidations.AllowAll)
			.DisableDirectStreaming());
#pragma warning restore CA2000 // Dispose objects before losing scope

		_ = services.AddElasticsearchServices(client,
			registry => registry.AddRepository<ITestElasticRepository, TestElasticRepository>());
	}
}
