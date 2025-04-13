using Excalibur.Tests.Fixtures;

namespace Excalibur.Tests.Infrastructure.TestBaseClasses;

[CollectionDefinition(nameof(SqlServerPersistenceOnlyContainerCollection))]
public class SqlServerPersistenceOnlyContainerCollection : ICollectionFixture<SqlServerContainerFixture>
{
	// No code inside, just for xUnit to recognize the shared collection.
}

[CollectionDefinition(nameof(PostgreSqlPersistenceOnlyContainerCollection))]
public class PostgreSqlPersistenceOnlyContainerCollection : ICollectionFixture<PostgreSqlContainerFixture>
{
	// No code inside, just for xUnit to recognize the shared collection.
}

[CollectionDefinition(nameof(SqlServerHostContainerCollection))]
public class SqlServerHostContainerCollection : ICollectionFixture<SqlServerContainerFixture>
{
	// No code inside, just for xUnit to recognize the shared collection.
}

[CollectionDefinition(nameof(PostgreSqlHostContainerCollection))]
public class PostgreSqlHostContainerCollection : ICollectionFixture<PostgreSqlContainerFixture>
{
	// No code inside, just for xUnit to recognize the shared collection.
}

[CollectionDefinition(nameof(ElasticsearchHostContainerCollection))]
public class ElasticsearchHostContainerCollection : ICollectionFixture<ElasticsearchContainerFixture>
{
	// No code inside, just for xUnit to recognize the shared collection.
}

[CollectionDefinition(nameof(ElasticsearchPersistenceOnlyContainerCollection))]
public class ElasticsearchPersistenceOnlyContainerCollection : ICollectionFixture<ElasticsearchContainerFixture>
{
}
