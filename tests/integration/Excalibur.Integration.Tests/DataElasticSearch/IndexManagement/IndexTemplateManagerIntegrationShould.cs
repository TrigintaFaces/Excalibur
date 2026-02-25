// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;

using Excalibur.Data.ElasticSearch.IndexManagement;

using Testcontainers.Elasticsearch;

namespace Excalibur.Integration.Tests.DataElasticSearch.IndexManagement;

/// <summary>
///     Integration tests for <see cref="IndexTemplateManager" /> using Tests.Shared.Handlers.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("IDisposable", "CA1001:Types that own disposable fields should be disposable", Justification = "Disposed via IAsyncLifetime.DisposeAsync")]
public sealed class IndexTemplateManagerIntegrationShould : IAsyncLifetime
{
	private readonly ElasticsearchContainer _elasticsearchContainer = new ElasticsearchBuilder()
		.WithImage("docker.elastic.co/elasticsearch/elasticsearch:8.11.0")
		.WithEnvironment("discovery.type", "single-node")
		.WithEnvironment("xpack.security.enabled", "false")
		.Build();

	private ElasticsearchClient _client = null!;
	private IndexTemplateManager _manager = null!;
	private ILoggerFactory _loggerFactory = null!;

	/// <inheritdoc/>
	public async Task InitializeAsync()
	{
		await _elasticsearchContainer.StartAsync().ConfigureAwait(true);

		// GetConnectionString() returns https:// by default, but security is disabled so ES
		// listens on plain HTTP. Replace the scheme to avoid SSL handshake failures.
		var connectionString = _elasticsearchContainer.GetConnectionString()
			.Replace("https://", "http://", StringComparison.OrdinalIgnoreCase);

		var settings = new ElasticsearchClientSettings(new Uri(connectionString));
		_client = new ElasticsearchClient(settings);

		_loggerFactory = new LoggerFactory();
		var logger = new Logger<IndexTemplateManager>(_loggerFactory);
		_manager = new IndexTemplateManager(_client, logger);
	}

	/// <inheritdoc/>
	public async Task DisposeAsync()
	{
		_loggerFactory?.Dispose();
		await _elasticsearchContainer.DisposeAsync().ConfigureAwait(true);
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
