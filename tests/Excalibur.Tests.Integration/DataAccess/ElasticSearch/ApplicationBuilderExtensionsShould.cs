using Elastic.Clients.Elasticsearch;
using Elastic.Transport;

using Excalibur.DataAccess.ElasticSearch;
using Excalibur.Tests.Fixtures;
using Excalibur.Tests.Infrastructure.TestBaseClasses.Host;
using Excalibur.Tests.Mothers;
using Excalibur.Tests.Shared;

using Microsoft.AspNetCore.Builder;

using Shouldly;

using Xunit.Abstractions;

namespace Excalibur.Tests.Integration.DataAccess.ElasticSearch;

public class ApplicationBuilderExtensionsShould(ElasticsearchContainerFixture fixture, ITestOutputHelper output)
	: ElasticSearchHostTestBase(fixture, output)
{
	[Fact]
	public async Task ShouldInitializeIndexDuringStartup()
	{
		var repo = GetRequiredService<ITestElasticRepository>();

		var doc = ElasticSearchMother.CreateTestDocument();

		// If the index is not initialized during startup, this will throw
		_ = await repo.AddOrUpdateAsync(doc.Id, doc).ConfigureAwait(true);
		var result = await repo.GetByIdAsync(doc.Id).ConfigureAwait(true);

		_ = result.ShouldNotBeNull();
		result!.Id.ShouldBe(doc.Id);
	}

	protected override void ConfigureHostServices(WebApplicationBuilder builder, IDatabaseContainerFixture fixture)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(fixture);

		var uri = new UriBuilder(fixture.ConnectionString) { Scheme = "http", Port = new Uri(fixture.ConnectionString).Port }.Uri;
#pragma warning disable CA2000 // Dispose objects before losing scope
		var client = new ElasticsearchClient(new ElasticsearchClientSettings(uri)
			.ServerCertificateValidationCallback(CertificateValidations.AllowAll)
			.DisableDirectStreaming());
#pragma warning restore CA2000 // Dispose objects before losing scope

		_ = builder.Services.AddElasticsearchServices(client,
			registry => registry.AddRepository<ITestElasticRepository, TestElasticRepository>());
	}

	protected override void ConfigureHostApplication(WebApplication app)
	{
		app.UseElasticsearchIndexInitialization();
	}
}
