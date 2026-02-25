// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Transport;

using Tests.Shared.Fixtures;

using Excalibur.Data.ElasticSearch;
using Excalibur.Integration.Tests.DataElasticSearch.Helpers;
using Excalibur.Integration.Tests.DataElasticSearch.Infrastructure.TestBaseClasses;
using Excalibur.Integration.Tests.DataElasticSearch.Infrastructure.TestBaseClasses.Host;

using Microsoft.AspNetCore.Builder;

using Xunit.Abstractions;

namespace Excalibur.Integration.Tests.DataElasticSearch.DataAccess.ElasticSearch;

[Collection(nameof(ElasticsearchHostTests))]
public class ApplicationBuilderExtensionsShould(ElasticsearchContainerFixture fixture, ITestOutputHelper output)
	: HostTestBase<ElasticsearchContainerFixture>(fixture, output)
{
	[Fact]
	public async Task ShouldInitializeIndexDuringStartup()
	{
		var repo = GetRequiredService<ITestElasticRepository>();

		var doc = ElasticSearchMother.CreateTestDocument();

		// If the index is not initialized during startup, this will throw
		_ = await repo.AddOrUpdateAsync(doc.Id, doc, CancellationToken.None).ConfigureAwait(true);
		var result = await repo.GetByIdAsync(doc.Id, CancellationToken.None).ConfigureAwait(true);

		_ = result.ShouldNotBeNull();
		result.Id.ShouldBe(doc.Id);
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

		_ = builder.Services.AddElasticsearchServices(
			elasticConfig,
			static registry => registry.AddRepository<ITestElasticRepository, TestElasticRepository>(),
			static settings => settings
				.ServerCertificateValidationCallback(CertificateValidations.AllowAll)
				.DisableDirectStreaming());
	}

	/// <inheritdoc/>
	protected override void ConfigureHostApplication(WebApplication app) =>
			app.InitializeElasticsearchIndexesAsync().GetAwaiter().GetResult();

	/// <inheritdoc/>
	protected override Task InitializePersistenceAsync() => Task.CompletedTask;
}
