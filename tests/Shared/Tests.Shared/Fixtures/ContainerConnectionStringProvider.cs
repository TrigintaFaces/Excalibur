// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

namespace Tests.Shared.Fixtures;

/// <summary>
/// Provides connection string utilities for TestContainers and CI/CD environments.
/// </summary>
/// <remarks>
/// <para>
/// This utility class provides methods to obtain connection strings from:
/// <list type="bullet">
/// <item><description>TestContainer fixtures (primary, for local development)</description></item>
/// <item><description>Environment variables (fallback, for CI/CD without Docker)</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Environment Variable Naming Convention:</b>
/// <list type="bullet">
/// <item><description><c>RABBITMQ_CONNECTION_STRING</c> - RabbitMQ AMQP connection string</description></item>
/// <item><description><c>POSTGRES_CONNECTION_STRING</c> - Postgres connection string</description></item>
/// <item><description><c>SQLSERVER_CONNECTION_STRING</c> - SQL Server connection string</description></item>
/// <item><description><c>REDIS_CONNECTION_STRING</c> - Redis connection string</description></item>
/// <item><description><c>ELASTICSEARCH_URL</c> - Elasticsearch HTTP URL</description></item>
/// <item><description><c>KAFKA_BOOTSTRAP_SERVERS</c> - Kafka bootstrap servers</description></item>
/// <item><description><c>MONGODB_CONNECTION_STRING</c> - MongoDB connection string</description></item>
/// </list>
/// </para>
/// </remarks>
public static class ContainerConnectionStringProvider
{
	// Environment variable names for CI/CD fallback
	private const string RabbitMqEnvVar = "RABBITMQ_CONNECTION_STRING";
	private const string PostgresEnvVar = "POSTGRES_CONNECTION_STRING";
	private const string SqlServerEnvVar = "SQLSERVER_CONNECTION_STRING";
	private const string RedisEnvVar = "REDIS_CONNECTION_STRING";
	private const string ElasticsearchEnvVar = "ELASTICSEARCH_URL";
	private const string KafkaEnvVar = "KAFKA_BOOTSTRAP_SERVERS";
	private const string MongoDbEnvVar = "MONGODB_CONNECTION_STRING";

	// TestContainers environment detection
	private const string TestContainersDockerHostEnvVar = "TESTCONTAINERS_DOCKER_HOST";
	private const string TestContainersRyukDisabledEnvVar = "TESTCONTAINERS_RYUK_DISABLED";

	#region RabbitMQ

	/// <summary>
	/// Gets the RabbitMQ connection string from the container fixture.
	/// </summary>
	/// <param name="container">The RabbitMQ container fixture.</param>
	/// <returns>The AMQP connection string.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="container"/> is null.</exception>
	public static string GetRabbitMqConnectionString(RabbitMqContainerFixture container)
	{
		ArgumentNullException.ThrowIfNull(container);
		return container.ConnectionString;
	}

	/// <summary>
	/// Gets the RabbitMQ connection string from environment variable.
	/// </summary>
	/// <returns>The connection string, or <c>null</c> if not configured.</returns>
	public static string? GetRabbitMqConnectionStringFromEnvironment()
		=> Environment.GetEnvironmentVariable(RabbitMqEnvVar);

	/// <summary>
	/// Gets the RabbitMQ connection string from the container or environment fallback.
	/// </summary>
	/// <param name="container">The container fixture (may be null or unavailable).</param>
	/// <returns>The connection string.</returns>
	/// <exception cref="InvalidOperationException">Thrown when neither container nor environment provides a connection string.</exception>
	public static string GetRabbitMqConnectionStringOrThrow(RabbitMqContainerFixture? container)
	{
		if (container?.IsReady == true)
		{
			return container.ConnectionString;
		}

		var envConnectionString = GetRabbitMqConnectionStringFromEnvironment();
		if (!string.IsNullOrEmpty(envConnectionString))
		{
			return envConnectionString;
		}

		throw new InvalidOperationException(
			$"No RabbitMQ connection available. Either start a TestContainer or set the {RabbitMqEnvVar} environment variable.");
	}

	#endregion

	#region Postgres

	/// <summary>
	/// Gets the Postgres connection string from the container fixture.
	/// </summary>
	/// <param name="container">The Postgres container fixture.</param>
	/// <returns>The connection string.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="container"/> is null.</exception>
	public static string GetPostgresConnectionString(PostgresContainerFixture container)
	{
		ArgumentNullException.ThrowIfNull(container);
		return container.ConnectionString;
	}

	/// <summary>
	/// Gets the Postgres connection string from environment variable.
	/// </summary>
	/// <returns>The connection string, or <c>null</c> if not configured.</returns>
	public static string? GetPostgresConnectionStringFromEnvironment()
		=> Environment.GetEnvironmentVariable(PostgresEnvVar);

	#endregion

	#region SQL Server

	/// <summary>
	/// Gets the SQL Server connection string from the container fixture.
	/// </summary>
	/// <param name="container">The SQL Server container fixture.</param>
	/// <returns>The connection string.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="container"/> is null.</exception>
	public static string GetSqlServerConnectionString(SqlServerContainerFixture container)
	{
		ArgumentNullException.ThrowIfNull(container);
		return container.ConnectionString;
	}

	/// <summary>
	/// Gets the SQL Server connection string from environment variable.
	/// </summary>
	/// <returns>The connection string, or <c>null</c> if not configured.</returns>
	public static string? GetSqlServerConnectionStringFromEnvironment()
		=> Environment.GetEnvironmentVariable(SqlServerEnvVar);

	#endregion

	#region Redis

	/// <summary>
	/// Gets the Redis connection string from the container fixture.
	/// </summary>
	/// <param name="container">The Redis container fixture.</param>
	/// <returns>The connection string.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="container"/> is null.</exception>
	public static string GetRedisConnectionString(RedisContainerFixture container)
	{
		ArgumentNullException.ThrowIfNull(container);
		return container.ConnectionString;
	}

	/// <summary>
	/// Gets the Redis connection string from environment variable.
	/// </summary>
	/// <returns>The connection string, or <c>null</c> if not configured.</returns>
	public static string? GetRedisConnectionStringFromEnvironment()
		=> Environment.GetEnvironmentVariable(RedisEnvVar);

	#endregion

	#region Elasticsearch

	/// <summary>
	/// Gets the Elasticsearch connection string (HTTP endpoint) from the container fixture.
	/// </summary>
	/// <param name="container">The Elasticsearch container fixture.</param>
	/// <returns>The HTTP endpoint string.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="container"/> is null.</exception>
	public static string GetElasticsearchConnectionString(ElasticsearchContainerFixture container)
	{
		ArgumentNullException.ThrowIfNull(container);
		return container.ConnectionString;
	}

	/// <summary>
	/// Gets the Elasticsearch connection string from environment variable.
	/// </summary>
	/// <returns>The connection string, or <c>null</c> if not configured.</returns>
	public static string? GetElasticsearchConnectionStringFromEnvironment()
		=> Environment.GetEnvironmentVariable(ElasticsearchEnvVar);

	#endregion

	#region Kafka

	/// <summary>
	/// Gets the Kafka bootstrap servers from the container fixture.
	/// </summary>
	/// <param name="container">The Kafka container fixture.</param>
	/// <returns>The bootstrap servers connection string.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="container"/> is null.</exception>
	public static string GetKafkaBootstrapServers(KafkaContainerFixture container)
	{
		ArgumentNullException.ThrowIfNull(container);
		return container.BootstrapServers;
	}

	/// <summary>
	/// Gets the Kafka bootstrap servers from environment variable.
	/// </summary>
	/// <returns>The bootstrap servers, or <c>null</c> if not configured.</returns>
	public static string? GetKafkaBootstrapServersFromEnvironment()
		=> Environment.GetEnvironmentVariable(KafkaEnvVar);

	#endregion

	#region MongoDB

	/// <summary>
	/// Gets the MongoDB connection string from the container fixture.
	/// </summary>
	/// <param name="container">The MongoDB container fixture.</param>
	/// <returns>The connection string.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="container"/> is null.</exception>
	public static string GetMongoDbConnectionString(MongoDbContainerFixture container)
	{
		ArgumentNullException.ThrowIfNull(container);
		return container.ConnectionString;
	}

	/// <summary>
	/// Gets the MongoDB connection string from environment variable.
	/// </summary>
	/// <returns>The connection string, or <c>null</c> if not configured.</returns>
	public static string? GetMongoDbConnectionStringFromEnvironment()
		=> Environment.GetEnvironmentVariable(MongoDbEnvVar);

	#endregion

	#region Environment Detection

	/// <summary>
	/// Detects whether the current environment is running inside a TestContainers context.
	/// </summary>
	/// <returns><c>true</c> if TestContainers Docker host is configured; otherwise, <c>false</c>.</returns>
	/// <remarks>
	/// This checks for the <c>TESTCONTAINERS_DOCKER_HOST</c> environment variable
	/// which is set when TestContainers is configured with a custom Docker host.
	/// </remarks>
	public static bool IsTestContainersEnvironment()
		=> !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(TestContainersDockerHostEnvVar));

	/// <summary>
	/// Detects whether TestContainers Ryuk cleanup is disabled.
	/// </summary>
	/// <returns><c>true</c> if Ryuk is explicitly disabled; otherwise, <c>false</c>.</returns>
	/// <remarks>
	/// Ryuk is the TestContainers resource reaper that cleans up orphaned containers.
	/// It may be disabled in some CI/CD environments.
	/// </remarks>
	public static bool IsRyukDisabled()
	{
		var ryukDisabled = Environment.GetEnvironmentVariable(TestContainersRyukDisabledEnvVar);
		return string.Equals(ryukDisabled, "true", StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Detects whether the current environment is a CI/CD pipeline.
	/// </summary>
	/// <returns><c>true</c> if running in a known CI/CD environment; otherwise, <c>false</c>.</returns>
	/// <remarks>
	/// Checks for common CI/CD environment variables:
	/// <list type="bullet">
	/// <item><description><c>CI</c> - Generic CI indicator</description></item>
	/// <item><description><c>GITHUB_ACTIONS</c> - GitHub Actions</description></item>
	/// <item><description><c>AZURE_PIPELINES</c> - Azure DevOps</description></item>
	/// <item><description><c>TF_BUILD</c> - Azure DevOps (alternate)</description></item>
	/// <item><description><c>GITLAB_CI</c> - GitLab CI</description></item>
	/// <item><description><c>JENKINS_URL</c> - Jenkins</description></item>
	/// </list>
	/// </remarks>
	public static bool IsCiEnvironment()
	{
		return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")) ||
				 !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")) ||
				 !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_PIPELINES")) ||
				 !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TF_BUILD")) ||
				 !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITLAB_CI")) ||
				 !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("JENKINS_URL"));
	}

	#endregion
}
