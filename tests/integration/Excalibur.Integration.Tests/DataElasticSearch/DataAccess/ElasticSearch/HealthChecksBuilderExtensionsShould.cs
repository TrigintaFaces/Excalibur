// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
using System.Text.Json;

using Excalibur.Data.ElasticSearch;
using Excalibur.Integration.Tests.DataElasticSearch.Helpers;
using Excalibur.Integration.Tests.DataElasticSearch.Infrastructure.TestBaseClasses;
using Excalibur.Integration.Tests.DataElasticSearch.Infrastructure.TestBaseClasses.Host;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;

using Tests.Shared.Fixtures;

using Xunit.Abstractions;

namespace Excalibur.Integration.Tests.DataElasticSearch.DataAccess.ElasticSearch;

[Collection(nameof(ElasticsearchHostTests))]
public class HealthChecksBuilderExtensionsShould(ElasticsearchContainerFixture fixture, ITestOutputHelper output)
	: HostTestBase<ElasticsearchContainerFixture>(fixture, output)
{
	[Fact]
	public async Task HealthCheckShouldReturnHealthyStatus()
	{
		// Arrange & Act
		var result = await TestHost.Scenario(static scenario => _ = scenario.Get.Url("/health")).ConfigureAwait(true);

		// Assert
		result.Context.Response.StatusCode.ShouldBe(200);

		var content = await result.ReadAsTextAsync().ConfigureAwait(true);
		content.ShouldContain("""
			"status":"Healthy"
			""");
	}

	/// <inheritdoc/>
	protected override void ConfigureHostApplication(WebApplication app)
	{
		base.ConfigureHostApplication(app);

		// Expose /health endpoint
		_ = app.MapHealthChecks("/health", new HealthCheckOptions
		{
			ResponseWriter = static async (context, report) =>
			{
				context.Response.ContentType = "application/json";
				var json = JsonSerializer.Serialize(new
				{
					status = report.Status.ToString(),
					results = report.Entries.Select(static e => new
					{
						name = e.Key,
						status = e.Value.Status.ToString(),
						description = e.Value.Description,
					}),
				});
				await context.Response.WriteAsync(json).ConfigureAwait(true);
			},
		});
	}

	/// <inheritdoc/>
	protected override void ConfigureHostServices(WebApplicationBuilder builder, IDatabaseContainerFixture fixture)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(fixture);

		var uri = new UriBuilder(fixture.ConnectionString) { Scheme = "http", Port = new Uri(fixture.ConnectionString).Port }.Uri;

		var elasticConfig = new ConfigurationBuilder()
				.AddInMemoryCollection(new Dictionary<string, string?> { ["ElasticSearch:Url"] = uri.ToString() })
				.Build();

		base.ConfigureHostServices(builder, fixture);

		_ = builder.Services
			.AddHealthChecks()
			.AddElasticHealthCheck(
				name: "Elastic",
				timeout: TimeSpan.FromSeconds(5));

		_ = builder.Services.AddElasticsearchServices(
			elasticConfig,
			static registry => registry.AddRepository<ITestElasticRepository, TestElasticRepository>(),
			static settings => settings.DisableDirectStreaming());
	}

	/// <inheritdoc/>
	protected override Task InitializePersistenceAsync() => Task.CompletedTask;
}
