using System.Data;

namespace Excalibur.Tests.Fixtures;

public interface IDatabaseContainerFixture
{
	public string ConnectionString { get; }

	public DatabaseEngine Engine { get; }

	public IDbConnection CreateDbConnection();

	public Task DisposeAsync();

	public Task InitializeAsync();
}

public enum DatabaseEngine
{
	SqlServer,
	PostgreSql,
	Elasticsearch
}
