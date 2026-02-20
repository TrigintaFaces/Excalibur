// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch;
using Elastic.Transport;

using Excalibur.Integration.Tests.DataElasticSearch.Helpers;
using Excalibur.Integration.Tests.DataElasticSearch.Infrastructure.TestBaseClasses;
using Excalibur.Integration.Tests.DataElasticSearch.Infrastructure.TestBaseClasses.PersistenceOnly;

using Tests.Shared.Fixtures;

using Xunit.Abstractions;

namespace Excalibur.Integration.Tests.DataElasticSearch.DataAccess.ElasticSearch;

[Collection(nameof(ElasticsearchPersistenceOnlyTests))]
public class TestElasticRepositoryShould(ElasticsearchContainerFixture fixture, ITestOutputHelper output)
	: PersistenceOnlyTestBase<ElasticsearchContainerFixture>(fixture, output)
{
	[Fact]
	public async Task AddOrUpdateShouldIndexDocument()
	{
		var _repository = GetRequiredService<ITestElasticRepository>();
		var doc = ElasticSearchMother.CreateTestDocument();
		_ = await _repository.AddOrUpdateAsync(doc.Id, doc, CancellationToken.None).ConfigureAwait(true);
		var result = await _repository.GetByIdAsync(doc.Id, CancellationToken.None).ConfigureAwait(true);
		_ = result.ShouldNotBeNull();
		result.Id.ShouldBe(doc.Id);
	}

	[Fact]
	public async Task GetByIdShouldReturnNullIfNotFound()
	{
		var _repository = GetRequiredService<ITestElasticRepository>();
		var result = await _repository.GetByIdAsync("nonexistent-id", CancellationToken.None).ConfigureAwait(true);
		result.ShouldBeNull();
	}

	[Fact]
	public async Task RemoveShouldDeleteDocument()
	{
		var _repository = GetRequiredService<ITestElasticRepository>();
		var doc = ElasticSearchMother.CreateTestDocument();
		_ = await _repository.AddOrUpdateAsync(doc.Id, doc, CancellationToken.None).ConfigureAwait(true);
		var deleted = await _repository.RemoveAsync(doc.Id, CancellationToken.None).ConfigureAwait(true);
		deleted.ShouldBeTrue();
		var result = await _repository.GetByIdAsync(doc.Id, CancellationToken.None).ConfigureAwait(true);
		result.ShouldBeNull();
	}

	[Fact]
	public async Task BulkAddOrUpdateShouldIndexMultipleDocuments()
	{
		var _repository = GetRequiredService<ITestElasticRepository>();
		var docs = ElasticSearchMother.CreateManyTestDocuments(5).ToList();
		_ = await _repository.BulkAddOrUpdateAsync(docs, static x => x.Id, CancellationToken.None).ConfigureAwait(true);
		foreach (var doc in docs)
		{
			var fetched = await _repository.GetByIdAsync(doc.Id, CancellationToken.None).ConfigureAwait(true);
			_ = fetched.ShouldNotBeNull();
			fetched.Name.ShouldBe(doc.Name);
		}
	}

	[Fact]
	public async Task UpdateShouldModifyFields()
	{
		var _repository = GetRequiredService<ITestElasticRepository>();
		var doc = ElasticSearchMother.CreateTestDocument();
		_ = await _repository.AddOrUpdateAsync(doc.Id, doc, CancellationToken.None).ConfigureAwait(true);
		var updated = new Dictionary<string, object> { ["name"] = "Updated Name" };
		_ = await _repository.UpdateAsync(doc.Id, updated, CancellationToken.None).ConfigureAwait(true);
		var fetched = await _repository.GetByIdAsync(doc.Id, CancellationToken.None).ConfigureAwait(true);
		fetched.Name.ShouldBe("Updated Name");
	}

	[Fact]
	public async Task SearchShouldReturnExpectedResults()
	{
		var _repository = GetRequiredService<ITestElasticRepository>();
		var doc = ElasticSearchMother.CreateTestDocument(name: "UniqueName");
		_ = await _repository.AddOrUpdateAsync(doc.Id, doc, CancellationToken.None).ConfigureAwait(true);

		// Elasticsearch near-real-time: force a refresh so the indexed document becomes searchable
		var client = GetRequiredService<ElasticsearchClient>();
		_ = await client.Indices.RefreshAsync("test-elastic-index").ConfigureAwait(true);

		var request = new SearchRequestDescriptor<TestElasticDocument>()
				.Indices("test-elastic-index")
				.Query(q => q.Match(m => m.Field("name").Query("UniqueName")));

		var result = await _repository.SearchAsync(request, CancellationToken.None).ConfigureAwait(true);
		result.Documents.ShouldContain(x => x.Id == doc.Id);
	}

	[Fact]
	public async Task GetByIdShouldReturnNullIfDocumentNotFound()
	{
		var _repository = GetRequiredService<ITestElasticRepository>();
		var result = await _repository.GetByIdAsync("nonexistent-id", CancellationToken.None).ConfigureAwait(true);
		result.ShouldBeNull();
	}

	/// <inheritdoc/>
	protected override Task InitializePersistenceAsync() => Task.CompletedTask;

	/// <inheritdoc/>
	protected override void AddServices(IServiceCollection services, IConfiguration configuration)
	{
		var uri = new UriBuilder(Fixture.ConnectionString) { Scheme = "http", Port = new Uri(Fixture.ConnectionString).Port }.Uri;

		var elasticConfig = new ConfigurationBuilder()
				.AddInMemoryCollection(new Dictionary<string, string?> { ["ElasticSearch:Url"] = uri.ToString() })
				.Build();

		_ = services.AddElasticsearchServices(
			elasticConfig,
			static registry => registry.AddRepository<ITestElasticRepository, TestElasticRepository>(),
			static settings => settings
				.ServerCertificateValidationCallback(CertificateValidations.AllowAll)
				.DisableDirectStreaming());
	}
}
