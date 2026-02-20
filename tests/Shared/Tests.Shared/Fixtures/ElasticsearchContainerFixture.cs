// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using DotNet.Testcontainers.Builders;

using Testcontainers.Elasticsearch;

namespace Tests.Shared.Fixtures;

/// <summary>
/// TestContainer fixture for Elasticsearch integration tests.
/// </summary>
/// <remarks>
/// Implements <see cref="IDatabaseContainerFixture"/> for compatibility with generic test bases,
/// but <see cref="CreateDbConnection"/> throws because Elasticsearch doesn't use relational connections.
/// </remarks>
public sealed class ElasticsearchContainerFixture : ContainerFixtureBase, IDatabaseContainerFixture
{
	private ElasticsearchContainer? _container;

	protected override TimeSpan ContainerStartTimeout => TimeSpan.FromMinutes(4);

	/// <inheritdoc/>
	/// <remarks>
	/// Note: The underlying Testcontainers library returns an https:// URL by default,
	/// but since xpack.security.enabled=false, the container listens on plain HTTP.
	/// This property converts the scheme to http:// to avoid SSL handshake failures.
	/// </remarks>
	public string ConnectionString => _container is not null
		? _container.GetConnectionString().Replace("https://", "http://", StringComparison.OrdinalIgnoreCase)
		: throw new InvalidOperationException("Container not initialized");

	/// <inheritdoc/>
	public DatabaseEngine Engine => DatabaseEngine.Elasticsearch;

	/// <inheritdoc/>
	public IDbConnection CreateDbConnection() =>
		throw new NotSupportedException("Elasticsearch does not use traditional relational DB connections.");

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new ElasticsearchBuilder()
			.WithImage("docker.elastic.co/elasticsearch/elasticsearch:8.13.0")
			.WithName($"es-test-{Guid.NewGuid():N}")
			.WithEnvironment("discovery.type", "single-node")
			.WithEnvironment("xpack.security.enabled", "false")
			.WithPortBinding(9200, true)
			.WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(static r => r.ForPort(9200)))
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);
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
