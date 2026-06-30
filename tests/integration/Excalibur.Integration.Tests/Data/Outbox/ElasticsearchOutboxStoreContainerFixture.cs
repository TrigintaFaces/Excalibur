// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using DotNet.Testcontainers.Builders;

using Elastic.Clients.Elasticsearch;

using Testcontainers.Elasticsearch;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Elasticsearch container fixture for the Elasticsearch <c>IOutboxStore</c> real-infrastructure conformance.
/// </summary>
/// <remarks>
/// Extends <see cref="ContainerFixtureBase"/>: Docker is required, so a missing container surfaces as a
/// failure rather than a silent pass. Security/TLS are disabled on the container, and the exposed
/// <see cref="Client"/> is built with the SDK's DEFAULT serializer settings — no custom converter — so the
/// store talks to real infrastructure exactly as a consumer's default client would. A unique index name per
/// run isolates the suite, and <see cref="DeleteIndexAsync"/> deletes the index between tests.
/// </remarks>
public sealed class ElasticsearchOutboxStoreContainerFixture : ContainerFixtureBase
{
	private ElasticsearchContainer? _container;

	/// <summary>
	/// Gets the Elasticsearch client (default serializer settings) injected into the store.
	/// </summary>
	public ElasticsearchClient Client { get; private set; } = null!;

	/// <summary>
	/// Gets the unique index name for this fixture's outbox documents.
	/// </summary>
	public string IndexName { get; } = $"outbox-test-{Guid.NewGuid():N}";

	/// <inheritdoc/>
	protected override TimeSpan ContainerStartTimeout => TimeSpan.FromMinutes(4);

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new ElasticsearchBuilder()
			.WithImage("docker.elastic.co/elasticsearch/elasticsearch:9.0.0")
			.WithName($"es-outbox-test-{Guid.NewGuid():N}")
			.WithEnvironment("discovery.type", "single-node")
			.WithEnvironment("xpack.security.enabled", "false")
			.WithEnvironment("xpack.security.http.ssl.enabled", "false")
			.WithEnvironment("ES_JAVA_OPTS", "-Xms512m -Xmx512m")
			.WithPortBinding(9200, true)
			.WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(static r => r.ForPort(9200)))
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);

		// xpack.security.enabled=false means the container listens on plain HTTP; the Testcontainers
		// connection string defaults to https://, so convert the scheme to avoid SSL handshake failures.
		var url = _container.GetConnectionString()
			.Replace("https://", "http://", StringComparison.OrdinalIgnoreCase);

		// DEFAULT serializer settings — no custom converter — so the store round-trips through real
		// infrastructure exactly as a consumer's default client would.
		var settings = new ElasticsearchClientSettings(new Uri(url));
		Client = new ElasticsearchClient(settings);
	}

	/// <summary>
	/// Deletes the fixture's outbox index between tests (best effort).
	/// </summary>
	public async Task DeleteIndexAsync()
	{
		_ = await Client.Indices.DeleteAsync(IndexName).ConfigureAwait(false);
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
