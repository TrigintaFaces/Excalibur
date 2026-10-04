// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using DotNet.Testcontainers.Builders;

using Elastic.Clients.Elasticsearch;

using Testcontainers.Elasticsearch;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.Data.MaterializedViews;

/// <summary>
/// Elasticsearch container fixture for the materialized-view position checkpoint.
/// </summary>
/// <remarks>
/// <para>
/// Mirrors the Elasticsearch inbox fixture. Extends <see cref="ContainerFixtureBase"/>, so Docker is
/// REQUIRED and a missing container surfaces as a failure rather than a silent pass — which matters
/// especially here, because the defects this suite binds were invisible to every non-container test.
/// </para>
/// <para>
/// The client is built with the SDK's DEFAULT serializer settings. That is load-bearing rather than
/// convenient: the position advance is performed by a Painless script addressing fields by their
/// SERIALIZED names, and the default serializer camelCases them. A fixture that configured a different
/// naming policy would exercise a shape no consumer runs.
/// </para>
/// </remarks>
public sealed class ElasticSearchViewPositionContainerFixture : ContainerFixtureBase
{
	private ElasticsearchContainer? _container;

	/// <summary>
	/// Gets the Elasticsearch client, built with default serializer settings.
	/// </summary>
	public ElasticsearchClient Client { get; private set; } = null!;

	/// <summary>
	/// Gets the unique views index name for this fixture.
	/// </summary>
	public string ViewsIndexName { get; } = $"views-test-{Guid.NewGuid():N}";

	/// <summary>
	/// Gets the unique positions index name for this fixture.
	/// </summary>
	public string PositionsIndexName { get; } = $"positions-test-{Guid.NewGuid():N}";

	/// <inheritdoc/>
	protected override TimeSpan ContainerStartTimeout => TimeSpan.FromMinutes(4);

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new ElasticsearchBuilder()
			.WithImage("docker.elastic.co/elasticsearch/elasticsearch:9.0.0")
			.WithName($"es-viewpos-test-{Guid.NewGuid():N}")
			.WithEnvironment("discovery.type", "single-node")
			.WithEnvironment("xpack.security.enabled", "false")
			.WithEnvironment("xpack.security.http.ssl.enabled", "false")
			.WithEnvironment("ES_JAVA_OPTS", "-Xms512m -Xmx512m")
			.WithPortBinding(9200, true)
			.WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(static r => r.ForPort(9200)))
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);

		// Security disabled means plain HTTP; the Testcontainers connection string defaults to https://.
		var url = _container.GetConnectionString()
			.Replace("https://", "http://", StringComparison.OrdinalIgnoreCase);

		Client = new ElasticsearchClient(new ElasticsearchClientSettings(new Uri(url)));
	}

	/// <summary>
	/// Drops the positions index so each arm starts with no checkpoint at all.
	/// </summary>
	/// <remarks>
	/// Drops the INDEX rather than a single document, deliberately. Deleting one document would require
	/// this fixture to reconstruct the store's tenant-qualified identifier, which couples the setup to a
	/// private naming scheme; and calling the store's own reset would make a defect in the code under test
	/// silently become this suite's setup. Dropping the index depends on neither, and the store recreates
	/// it on next use.
	/// </remarks>
	public async Task DropPositionsIndexAsync()
	{
		_ = await Client.Indices.DeleteAsync(PositionsIndexName).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	protected override async Task DisposeContainerAsync(CancellationToken cancellationToken)
	{
		if (_container is not null)
		{
			await _container.DisposeAsync().ConfigureAwait(false);
		}
	}
}
