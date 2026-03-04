// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;

using Excalibur.Data.ElasticSearch.IndexManagement;
using Excalibur.Integration.Tests.DataElasticSearch.Infrastructure.TestBaseClasses;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.DataElasticSearch.IndexManagement;

/// <summary>
///     Integration tests for <see cref="IndexTemplateManager" /> using Tests.Shared.Handlers.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("IDisposable", "CA1001:Types that own disposable fields should be disposable", Justification = "Disposed via IDisposable.Dispose")]
[Collection(nameof(ElasticsearchHostTests))]
public sealed class IndexTemplateManagerIntegrationShould : IDisposable
{
	private readonly ElasticsearchClient _client;
	private readonly IndexTemplateManager _manager;
	private readonly ILoggerFactory _loggerFactory;

	public IndexTemplateManagerIntegrationShould(ElasticsearchContainerFixture fixture)
	{
		var settings = new ElasticsearchClientSettings(new Uri(fixture.ConnectionString));
		_client = new ElasticsearchClient(settings);

		_loggerFactory = new LoggerFactory();
		var logger = new Logger<IndexTemplateManager>(_loggerFactory);
		_manager = new IndexTemplateManager(_client, logger);
	}

	public void Dispose()
	{
		_loggerFactory.Dispose();
	}

	[Fact]
	public async Task CreateOrUpdateTemplateAsyncCreateTemplateWhenValidConfiguration()
	{
		// Arrange
		var templateName = "test-template";
		var template = new IndexTemplateConfiguration
		{
			IndexPatterns = ["test-*"],
			Priority = 100,
			Template = new IndexSettings { NumberOfShards = 1, NumberOfReplicas = 0 },
		};

		// Act
		var result = await _manager.CreateOrUpdateTemplateAsync(templateName, template, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeTrue();

		// Verify template exists
		var exists = await _manager.TemplateExistsAsync(templateName, CancellationToken.None).ConfigureAwait(false);
		exists.ShouldBeTrue();
	}

	[Fact]
	public async Task DeleteTemplateAsyncRemoveTemplateWhenTemplateExists()
	{
		// Arrange
		var templateName = "delete-test-template";
		var template = new IndexTemplateConfiguration { IndexPatterns = ["delete-test-*"], Priority = 100 };

		_ = await _manager.CreateOrUpdateTemplateAsync(templateName, template, CancellationToken.None).ConfigureAwait(false);

		// Act
		var result = await _manager.DeleteTemplateAsync(templateName, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBeTrue();

		// Verify template no longer exists
		var exists = await _manager.TemplateExistsAsync(templateName, CancellationToken.None).ConfigureAwait(false);
		exists.ShouldBeFalse();
	}
}
