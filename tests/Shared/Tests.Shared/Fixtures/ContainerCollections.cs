namespace Tests.Shared.Fixtures;

/// <summary>
/// Collection definitions for sharing TestContainers across tests.
/// Use [Collection(ContainerCollections.Postgres)] on test classes to share the container.
/// This dramatically improves test execution time by reusing containers.
/// </summary>
public static class ContainerCollections
{
	/// <summary>
	/// Postgres container shared across all tests in this collection.
	/// </summary>
	public const string Postgres = "Postgres";

	/// <summary>
	/// SQL Server container shared across all tests in this collection.
	/// </summary>
	public const string SqlServer = "SQL Server";

	/// <summary>
	/// Redis container shared across all tests in this collection.
	/// </summary>
	public const string Redis = "Redis";

	/// <summary>
	/// MongoDB container shared across all tests in this collection.
	/// </summary>
	public const string MongoDB = "MongoDB";

	/// <summary>
	/// Kafka container shared across all tests in this collection.
	/// </summary>
	public const string Kafka = "Kafka";

	/// <summary>
	/// RabbitMQ container shared across all tests in this collection.
	/// </summary>
	public const string RabbitMQ = "RabbitMQ";

	/// <summary>
	/// Elasticsearch container shared across all tests in this collection.
	/// </summary>
	public const string Elasticsearch = "Elasticsearch";
}

// Collection definitions - these register fixtures for sharing

[CollectionDefinition(ContainerCollections.Postgres)]
public class PostgresCollection : ICollectionFixture<PostgresContainerFixture> { }

[CollectionDefinition(ContainerCollections.SqlServer)]
public class SqlServerCollection : ICollectionFixture<SqlServerContainerFixture> { }

[CollectionDefinition(ContainerCollections.Redis)]
public class RedisCollection : ICollectionFixture<RedisContainerFixture> { }

[CollectionDefinition(ContainerCollections.MongoDB)]
public class MongoDbCollection : ICollectionFixture<MongoDbContainerFixture> { }

[CollectionDefinition(ContainerCollections.Kafka)]
public class KafkaCollection : ICollectionFixture<KafkaContainerFixture> { }

[CollectionDefinition(ContainerCollections.RabbitMQ)]
public class RabbitMqCollection : ICollectionFixture<RabbitMqContainerFixture> { }

[CollectionDefinition(ContainerCollections.Elasticsearch)]
public class ElasticsearchCollection : ICollectionFixture<ElasticsearchContainerFixture> { }
