// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Excalibur.Data.Tests.DataAnnotations;

/// <summary>
/// Sprint 637 B.5: Validates that Options classes across extracted provider packages
/// have proper DataAnnotations attributes ([Required], [Range]) on their properties.
/// Uses reflection to verify annotation presence without instantiating options.
/// </summary>
[Trait("Category", "Unit")]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Feature", "DataAnnotations")]
public sealed class ProviderDataAnnotationsShould : UnitTestBase
{
	/// <summary>
	/// Verifies that the specified Options class has a [Required] attribute on the given property.
	/// </summary>
	[Theory]
	// --- Inbox providers ---
	[InlineData(typeof(Inbox.CosmosDb.CosmosDbInboxOptions), "DatabaseName")]
	[InlineData(typeof(Inbox.CosmosDb.CosmosDbInboxOptions), "ContainerName")]
	[InlineData(typeof(Inbox.CosmosDb.CosmosDbInboxOptions), "PartitionKeyPath")]
	[InlineData(typeof(Inbox.DynamoDb.DynamoDbInboxOptions), "TableName")]
	[InlineData(typeof(Inbox.DynamoDb.DynamoDbInboxOptions), "PartitionKeyAttribute")]
	[InlineData(typeof(Inbox.DynamoDb.DynamoDbInboxOptions), "SortKeyAttribute")]
	[InlineData(typeof(Inbox.DynamoDb.DynamoDbInboxOptions), "TtlAttributeName")]
	[InlineData(typeof(Inbox.ElasticSearch.ElasticsearchInboxOptions), "IndexName")]
	[InlineData(typeof(Inbox.Firestore.FirestoreInboxOptions), "CollectionName")]
	[InlineData(typeof(Inbox.MongoDB.MongoDbInboxOptions), "ConnectionString")]
	[InlineData(typeof(Inbox.MongoDB.MongoDbInboxOptions), "DatabaseName")]
	[InlineData(typeof(Inbox.MongoDB.MongoDbInboxOptions), "CollectionName")]
	[InlineData(typeof(Inbox.Postgres.PostgresInboxOptions), "ConnectionString")]
	[InlineData(typeof(Inbox.Postgres.PostgresInboxOptions), "SchemaName")]
	[InlineData(typeof(Inbox.Postgres.PostgresInboxOptions), "TableName")]
	[InlineData(typeof(Inbox.Redis.RedisInboxOptions), "ConnectionString")]
	[InlineData(typeof(Inbox.Redis.RedisInboxOptions), "KeyPrefix")]
	// --- Outbox providers ---
	[InlineData(typeof(Outbox.ElasticSearch.ElasticsearchOutboxOptions), "IndexName")]
	[InlineData(typeof(Outbox.Redis.RedisOutboxOptions), "ConnectionString")]
	[InlineData(typeof(Outbox.Redis.RedisOutboxOptions), "KeyPrefix")]
	[InlineData(typeof(Outbox.CosmosDb.CosmosDbOutboxOptions), "ContainerName")]
	[InlineData(typeof(Outbox.DynamoDb.DynamoDbOutboxOptions), "TableName")]
	[InlineData(typeof(Outbox.DynamoDb.DynamoDbOutboxOptions), "PartitionKeyAttribute")]
	[InlineData(typeof(Outbox.DynamoDb.DynamoDbOutboxOptions), "SortKeyAttribute")]
	[InlineData(typeof(Outbox.DynamoDb.DynamoDbOutboxOptions), "TtlAttribute")]
	[InlineData(typeof(Outbox.Firestore.FirestoreOutboxOptions), "CollectionName")]
	[InlineData(typeof(Outbox.MongoDB.MongoDbOutboxOptions), "ConnectionString")]
	[InlineData(typeof(Outbox.MongoDB.MongoDbOutboxOptions), "DatabaseName")]
	[InlineData(typeof(Outbox.MongoDB.MongoDbOutboxOptions), "CollectionName")]
	[InlineData(typeof(Outbox.Postgres.PostgresOutboxStoreOptions), "SchemaName")]
	[InlineData(typeof(Outbox.Postgres.PostgresOutboxStoreOptions), "OutboxTableName")]
	[InlineData(typeof(Outbox.Postgres.PostgresOutboxStoreOptions), "DeadLetterTableName")]
	// --- CDC providers ---
	[InlineData(typeof(Cdc.CosmosDb.CosmosDbCdcOptions), "ConnectionString")]
	[InlineData(typeof(Cdc.CosmosDb.CosmosDbCdcOptions), "DatabaseId")]
	[InlineData(typeof(Cdc.CosmosDb.CosmosDbCdcOptions), "ContainerId")]
	[InlineData(typeof(Cdc.CosmosDb.CosmosDbCdcOptions), "ProcessorName")]
	[InlineData(typeof(Cdc.DynamoDb.DynamoDbCdcOptions), "ProcessorName")]
	[InlineData(typeof(Cdc.MongoDB.MongoDbCdcOptions), "ProcessorId")]
	[InlineData(typeof(Cdc.Firestore.FirestoreCdcOptions), "CollectionPath")]
	[InlineData(typeof(Cdc.Firestore.FirestoreCdcOptions), "ProcessorName")]
	// --- Saga providers ---
	[InlineData(typeof(Saga.CosmosDb.CosmosDbSagaOptions), "DatabaseName")]
	[InlineData(typeof(Saga.CosmosDb.CosmosDbSagaOptions), "ContainerName")]
	[InlineData(typeof(Saga.CosmosDb.CosmosDbSagaOptions), "PartitionKeyPath")]
	[InlineData(typeof(Saga.DynamoDb.DynamoDbSagaOptions), "TableName")]
	[InlineData(typeof(Saga.DynamoDb.DynamoDbSagaOptions), "TtlAttributeName")]
	[InlineData(typeof(Saga.Firestore.FirestoreSagaOptions), "CollectionName")]
	[InlineData(typeof(Saga.MongoDB.MongoDbSagaOptions), "ConnectionString")]
	[InlineData(typeof(Saga.MongoDB.MongoDbSagaOptions), "DatabaseName")]
	[InlineData(typeof(Saga.MongoDB.MongoDbSagaOptions), "CollectionName")]
	[InlineData(typeof(Saga.Postgres.PostgresSagaOptions), "ConnectionString")]
	[InlineData(typeof(Saga.Postgres.PostgresSagaOptions), "Schema")]
	[InlineData(typeof(Saga.Postgres.PostgresSagaOptions), "TableName")]
	// --- EventSourcing providers ---
	[InlineData(typeof(EventSourcing.MongoDB.MongoDbEventStoreOptions), "ConnectionString")]
	[InlineData(typeof(EventSourcing.MongoDB.MongoDbEventStoreOptions), "DatabaseName")]
	[InlineData(typeof(EventSourcing.MongoDB.MongoDbEventStoreOptions), "CollectionName")]
	[InlineData(typeof(EventSourcing.CosmosDb.CosmosDbEventStoreOptions), "EventsContainerName")]
	[InlineData(typeof(EventSourcing.CosmosDb.CosmosDbEventStoreOptions), "PartitionKeyPath")]
	[InlineData(typeof(EventSourcing.DynamoDb.DynamoDbEventStoreOptions), "EventsTableName")]
	[InlineData(typeof(EventSourcing.DynamoDb.DynamoDbEventStoreOptions), "PartitionKeyAttribute")]
	[InlineData(typeof(EventSourcing.DynamoDb.DynamoDbEventStoreOptions), "SortKeyAttribute")]
	[InlineData(typeof(EventSourcing.Firestore.FirestoreEventStoreOptions), "EventsCollectionName")]
	[InlineData(typeof(EventSourcing.Redis.RedisEventStoreOptions), "ConnectionString")]
	// --- LeaderElection providers ---
	[InlineData(typeof(LeaderElection.MongoDB.MongoDbLeaderElectionOptions), "ConnectionString")]
	[InlineData(typeof(LeaderElection.MongoDB.MongoDbLeaderElectionOptions), "DatabaseName")]
	[InlineData(typeof(LeaderElection.MongoDB.MongoDbLeaderElectionOptions), "CollectionName")]
	[InlineData(typeof(LeaderElection.Postgres.PostgresLeaderElectionOptions), "ConnectionString")]
	[InlineData(typeof(LeaderElection.SqlServer.SqlServerHealthBasedLeaderElectionOptions), "SchemaName")]
	[InlineData(typeof(LeaderElection.SqlServer.SqlServerHealthBasedLeaderElectionOptions), "TableName")]
	public void HaveRequiredAttribute_OnExpectedProperty(Type optionsType, string propertyName)
	{
		var property = optionsType.GetProperty(propertyName);
		property.ShouldNotBeNull($"Property '{propertyName}' not found on '{optionsType.Name}'");

		var requiredAttr = property.GetCustomAttribute<RequiredAttribute>();
		requiredAttr.ShouldNotBeNull(
			$"Property '{optionsType.Name}.{propertyName}' should have [Required] attribute");
	}

	/// <summary>
	/// Verifies that the specified Options class has a [Range] attribute on the given property.
	/// </summary>
	[Theory]
	// --- Inbox providers ---
	[InlineData(typeof(Inbox.DynamoDb.DynamoDbInboxOptions), "DefaultTtlSeconds")]
	[InlineData(typeof(Inbox.ElasticSearch.ElasticsearchInboxOptions), "RetentionDays")]
	[InlineData(typeof(Inbox.Firestore.FirestoreInboxOptions), "DefaultTtlSeconds")]
	[InlineData(typeof(Inbox.Firestore.FirestoreInboxOptions), "TimeoutInSeconds")]
	[InlineData(typeof(Inbox.InMemory.InMemoryInboxOptions), "MaxEntries")]
	[InlineData(typeof(Inbox.MongoDB.MongoDbInboxOptions), "DefaultTtlSeconds")]
	[InlineData(typeof(Inbox.MongoDB.MongoDbInboxOptions), "ServerSelectionTimeoutSeconds")]
	[InlineData(typeof(Inbox.MongoDB.MongoDbInboxOptions), "ConnectTimeoutSeconds")]
	[InlineData(typeof(Inbox.MongoDB.MongoDbInboxOptions), "MaxPoolSize")]
	[InlineData(typeof(Inbox.Postgres.PostgresInboxOptions), "CommandTimeoutSeconds")]
	[InlineData(typeof(Inbox.Postgres.PostgresInboxOptions), "MaxRetryCount")]
	[InlineData(typeof(Inbox.Redis.RedisInboxOptions), "DatabaseId")]
	[InlineData(typeof(Inbox.Redis.RedisInboxOptions), "DefaultTtlSeconds")]
	[InlineData(typeof(Inbox.Redis.RedisInboxOptions), "ConnectTimeoutMs")]
	[InlineData(typeof(Inbox.Redis.RedisInboxOptions), "SyncTimeoutMs")]
	// --- Outbox providers ---
	[InlineData(typeof(Outbox.ElasticSearch.ElasticsearchOutboxOptions), "DefaultBatchSize")]
	[InlineData(typeof(Outbox.Redis.RedisOutboxOptions), "DatabaseId")]
	[InlineData(typeof(Outbox.Redis.RedisOutboxOptions), "SentMessageTtlSeconds")]
	[InlineData(typeof(Outbox.Redis.RedisOutboxOptions), "ConnectTimeoutMs")]
	[InlineData(typeof(Outbox.Redis.RedisOutboxOptions), "SyncTimeoutMs")]
	[InlineData(typeof(Outbox.CosmosDb.CosmosDbOutboxOptions), "MaxRetryAttempts")]
	[InlineData(typeof(Outbox.CosmosDb.CosmosDbOutboxOptions), "MaxRetryWaitTimeInSeconds")]
	[InlineData(typeof(Outbox.CosmosDb.CosmosDbOutboxOptions), "ContainerThroughput")]
	[InlineData(typeof(Outbox.DynamoDb.DynamoDbOutboxOptions), "DefaultTimeToLiveSeconds")]
	[InlineData(typeof(Outbox.DynamoDb.DynamoDbOutboxOptions), "MaxRetryAttempts")]
	[InlineData(typeof(Outbox.Firestore.FirestoreOutboxOptions), "DefaultTimeToLiveSeconds")]
	[InlineData(typeof(Outbox.Firestore.FirestoreOutboxOptions), "MaxBatchSize")]
	[InlineData(typeof(Outbox.Firestore.FirestoreOutboxOptions), "MaxRetryAttempts")]
	[InlineData(typeof(Outbox.InMemory.InMemoryOutboxOptions), "MaxMessages")]
	[InlineData(typeof(Outbox.MongoDB.MongoDbOutboxOptions), "SentMessageTtlSeconds")]
	[InlineData(typeof(Outbox.MongoDB.MongoDbOutboxOptions), "ServerSelectionTimeoutSeconds")]
	[InlineData(typeof(Outbox.MongoDB.MongoDbOutboxOptions), "ConnectTimeoutSeconds")]
	[InlineData(typeof(Outbox.MongoDB.MongoDbOutboxOptions), "MaxPoolSize")]
	[InlineData(typeof(Outbox.Postgres.PostgresOutboxStoreOptions), "ReservationTimeout")]
	// --- CDC providers ---
	[InlineData(typeof(Cdc.CosmosDb.CosmosDbChangeFeedOptions), "MaxBatchSize")]
	[InlineData(typeof(Cdc.DynamoDb.DynamoDbCdcOptions), "MaxBatchSize")]
	[InlineData(typeof(Cdc.MongoDB.MongoDbCdcOptions), "BatchSize")]
	[InlineData(typeof(Cdc.Firestore.FirestoreCdcOptions), "MaxBatchSize")]
	[InlineData(typeof(Cdc.Firestore.FirestoreCdcOptions), "ChannelCapacity")]
	// --- Saga providers ---
	[InlineData(typeof(Saga.CosmosDb.CosmosDbSagaOptions), "ContainerThroughput")]
	[InlineData(typeof(Saga.DynamoDb.DynamoDbSagaOptions), "MaxRetryAttempts")]
	[InlineData(typeof(Saga.DynamoDb.DynamoDbSagaOptions), "TimeoutInSeconds")]
	[InlineData(typeof(Saga.DynamoDb.DynamoDbSagaOptions), "DefaultTtlSeconds")]
	[InlineData(typeof(Saga.Firestore.FirestoreSagaOptions), "TimeoutInSeconds")]
	[InlineData(typeof(Saga.MongoDB.MongoDbSagaOptions), "ServerSelectionTimeoutSeconds")]
	[InlineData(typeof(Saga.MongoDB.MongoDbSagaOptions), "ConnectTimeoutSeconds")]
	[InlineData(typeof(Saga.MongoDB.MongoDbSagaOptions), "MaxPoolSize")]
	[InlineData(typeof(Saga.Postgres.PostgresSagaOptions), "CommandTimeoutSeconds")]
	// --- EventSourcing providers ---
	[InlineData(typeof(EventSourcing.MongoDB.MongoDbEventStoreOptions), "ServerSelectionTimeoutSeconds")]
	[InlineData(typeof(EventSourcing.MongoDB.MongoDbEventStoreOptions), "ConnectTimeoutSeconds")]
	[InlineData(typeof(EventSourcing.MongoDB.MongoDbEventStoreOptions), "MaxPoolSize")]
	[InlineData(typeof(EventSourcing.CosmosDb.CosmosDbEventStoreOptions), "MaxBatchSize")]
	[InlineData(typeof(EventSourcing.CosmosDb.CosmosDbEventStoreOptions), "ContainerThroughput")]
	[InlineData(typeof(EventSourcing.DynamoDb.DynamoDbEventStoreOptions), "MaxBatchSize")]
	[InlineData(typeof(EventSourcing.Firestore.FirestoreEventStoreOptions), "MaxBatchSize")]
	[InlineData(typeof(EventSourcing.Redis.RedisEventStoreOptions), "DefaultBatchSize")]
	[InlineData(typeof(EventSourcing.Redis.RedisEventStoreOptions), "DatabaseIndex")]
	// --- LeaderElection providers ---
	[InlineData(typeof(LeaderElection.MongoDB.MongoDbLeaderElectionOptions), "LeaseDurationSeconds")]
	[InlineData(typeof(LeaderElection.MongoDB.MongoDbLeaderElectionOptions), "RenewIntervalSeconds")]
	[InlineData(typeof(LeaderElection.MongoDB.MongoDbLeaderElectionOptions), "TimeoutInSeconds")]
	[InlineData(typeof(LeaderElection.Postgres.PostgresLeaderElectionOptions), "LockKey")]
	[InlineData(typeof(LeaderElection.Postgres.PostgresLeaderElectionOptions), "CommandTimeoutSeconds")]
	[InlineData(typeof(LeaderElection.SqlServer.SqlServerHealthBasedLeaderElectionOptions), "HealthExpirationSeconds")]
	[InlineData(typeof(LeaderElection.SqlServer.SqlServerHealthBasedLeaderElectionOptions), "CommandTimeoutSeconds")]
	public void HaveRangeAttribute_OnExpectedProperty(Type optionsType, string propertyName)
	{
		var property = optionsType.GetProperty(propertyName);
		property.ShouldNotBeNull($"Property '{propertyName}' not found on '{optionsType.Name}'");

		var rangeAttr = property.GetCustomAttribute<RangeAttribute>();
		rangeAttr.ShouldNotBeNull(
			$"Property '{optionsType.Name}.{propertyName}' should have [Range] attribute");
	}

	/// <summary>
	/// Verifies that each Options class passes validation when properly configured with required values.
	/// </summary>
	[Theory]
	[InlineData(typeof(Inbox.InMemory.InMemoryInboxOptions))]
	[InlineData(typeof(Outbox.InMemory.InMemoryOutboxOptions))]
	[InlineData(typeof(Outbox.ElasticSearch.ElasticsearchOutboxOptions))]
	[InlineData(typeof(Inbox.ElasticSearch.ElasticsearchInboxOptions))]
	public void HaveAtLeastOneDataAnnotation(Type optionsType)
	{
		var properties = optionsType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

		var annotatedProps = properties.Where(p =>
			p.GetCustomAttribute<RequiredAttribute>() != null ||
			p.GetCustomAttribute<RangeAttribute>() != null).ToList();

		annotatedProps.Count.ShouldBeGreaterThan(0,
			$"Options class '{optionsType.Name}' should have at least one DataAnnotation attribute");
	}
}
