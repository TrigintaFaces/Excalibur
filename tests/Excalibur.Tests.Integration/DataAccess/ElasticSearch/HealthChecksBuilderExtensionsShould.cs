using System.Text.Json;

using Elastic.Clients.Elasticsearch;

using Excalibur.DataAccess.ElasticSearch;
using Excalibur.Tests.Fixtures;
using Excalibur.Tests.Infrastructure.TestBaseClasses.Host;
using Excalibur.Tests.Shared;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit.Abstractions;

namespace Excalibur.Tests.Integration.DataAccess.ElasticSearch;

public class HealthChecksBuilderExtensionsShould(ElasticsearchContainerFixture fixture, ITestOutputHelper output)
	: ElasticSearchHostTestBase(fixture, output)
{
	[Fact]
	public async Task HealthCheckShouldReturnHealthyStatus()
	{
		// Arrange & Act
		var result = await TestHost!.Scenario(async scenario =>
		{
			_ = scenario.Get.Url("/health");
		}).ConfigureAwait(true);

		// Assert
		result.Context.Response.StatusCode.ShouldBe(200);

		var content = await result.ReadAsTextAsync().ConfigureAwait(true);
		content.ShouldContain("\"status\":\"Healthy\"");
	}

	protected override void ConfigureHostApplication(WebApplication app)
	{
		base.ConfigureHostApplication(app);

		// Expose /health endpoint
		_ = app.MapHealthChecks("/health", new HealthCheckOptions
		{
			ResponseWriter = async (context, report) =>
			{
				context.Response.ContentType = "application/json";
				var json = JsonSerializer.Serialize(new
				{
					status = report.Status.ToString(),
					results = report.Entries.Select(e => new
					{
						name = e.Key,
						status = e.Value.Status.ToString(),
						description = e.Value.Description
					})
				});
				await context.Response.WriteAsync(json).ConfigureAwait(true);
			}
		});
	}

	protected override void ConfigureHostServices(WebApplicationBuilder builder, IDatabaseContainerFixture fixture)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(fixture);

		var uri = new UriBuilder(fixture.ConnectionString) { Scheme = "http", Port = new Uri(fixture.ConnectionString).Port }.Uri;
#pragma warning disable CA2000 // Dispose objects before losing scope
		var client = new ElasticsearchClient(new ElasticsearchClientSettings(uri)
			.DisableDirectStreaming());
#pragma warning restore CA2000 // Dispose objects before losing scope

		base.ConfigureHostServices(builder, fixture);

		_ = builder.Services
			.AddHealthChecks()
			.AddElasticHealthCheck(
				name: "Elastic",
				timeout: TimeSpan.FromSeconds(5));

		_ = builder.Services.AddElasticsearchServices(client,
			registry => registry.AddRepository<ITestElasticRepository, TestElasticRepository>());
	}
}
