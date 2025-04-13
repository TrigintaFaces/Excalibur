using System.Data;

using DotNet.Testcontainers.Builders;

using Testcontainers.Elasticsearch;

namespace Excalibur.Tests.Fixtures;

public class ElasticsearchContainerFixture : IDatabaseContainerFixture, IAsyncLifetime
{
	private readonly ElasticsearchContainer _container = new ElasticsearchBuilder()
		.WithImage("docker.elastic.co/elasticsearch/elasticsearch:8.13.0")
		.WithName($"es-test-{Guid.NewGuid():N}")
		.WithEnvironment("discovery.type", "single-node")
		.WithEnvironment("xpack.security.enabled", "false")
		.WithPortBinding(9200, true)
		.WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(9200)))
		.Build();

	public string ConnectionString => _container.GetConnectionString();

	public DatabaseEngine Engine => DatabaseEngine.Elasticsearch;

	public IDbConnection CreateDbConnection() =>
		throw new NotSupportedException("Elasticsearch does not use traditional relational DB connections.");

	public async Task InitializeAsync() => await _container.StartAsync().ConfigureAwait(false);

	public async Task DisposeAsync() => await _container.DisposeAsync().ConfigureAwait(false);
}
