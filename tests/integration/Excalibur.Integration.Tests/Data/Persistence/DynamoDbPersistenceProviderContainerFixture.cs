// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;

using Testcontainers.LocalStack;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.Data.Persistence;

/// <summary>
/// LocalStack (DynamoDB) container fixture for the DynamoDB persistence-provider conformance suite.
/// </summary>
/// <remarks>
/// Mirrors the DynamoDb event-store fixture's LocalStack setup and exposes an <see cref="IAmazonDynamoDB"/>
/// built with the SDK's <em>default</em> configuration (only the LocalStack <c>ServiceURL</c> and test
/// credentials are supplied), so the suite exercises the surface a default consumer client produces.
/// Extends <see cref="ContainerFixtureBase"/>: real-infra conformance is never skipped.
/// </remarks>
public sealed class DynamoDbPersistenceProviderContainerFixture : ContainerFixtureBase
{
	private LocalStackContainer? _container;
	private AmazonDynamoDBClient? _client;

	/// <summary>
	/// Gets the table the conformance suite's provider reads and writes, keyed <c>pk</c>/<c>sk</c> (the
	/// provider's default key attribute names).
	/// </summary>
	public string ConformanceTableName { get; } = "persistence_conformance";

	/// <summary>
	/// Gets the LocalStack edge endpoint (the DynamoDB <c>ServiceUrl</c>).
	/// </summary>
	public string ServiceUrl => _container?.GetConnectionString()
		?? throw new InvalidOperationException("Container not initialized");

	/// <summary>
	/// Gets the DynamoDB client pointing at the LocalStack container, built with the SDK default config.
	/// </summary>
	public IAmazonDynamoDB Client => _client
		?? throw new InvalidOperationException("Container not initialized");

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new LocalStackBuilder()
			.WithImage("localstack/localstack:4")
			.WithName($"localstack-persistence-dynamodb-{Guid.NewGuid():N}")
			.WithEnvironment("SERVICES", "dynamodb")
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);

		var credentials = new BasicAWSCredentials("test", "test");
		_client = new AmazonDynamoDBClient(
			credentials,
			new AmazonDynamoDBConfig { ServiceURL = _container.GetConnectionString() });

		// The suite's provider is built through the client-injecting constructor, which never creates a table.
		// Without this table every document operation the kit issues fails, and an arm that seeds data and
		// declines when seeding fails would report PASS having asserted nothing.
		_ = await _client.CreateTableAsync(
			new CreateTableRequest
			{
				TableName = ConformanceTableName,
				AttributeDefinitions =
				[
					new AttributeDefinition("pk", ScalarAttributeType.S),
					new AttributeDefinition("sk", ScalarAttributeType.S)
				],
				KeySchema =
				[
					new KeySchemaElement("pk", KeyType.HASH),
					new KeySchemaElement("sk", KeyType.RANGE)
				],
				BillingMode = BillingMode.PAY_PER_REQUEST
			},
			cancellationToken).ConfigureAwait(false);

		for (var attempt = 0; ; attempt++)
		{
			var described = await _client.DescribeTableAsync(ConformanceTableName, cancellationToken).ConfigureAwait(false);
			if (described.Table.TableStatus == TableStatus.ACTIVE)
			{
				break;
			}

			if (attempt >= 120)
			{
				throw new InvalidOperationException($"Table '{ConformanceTableName}' did not become ACTIVE.");
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}
	}

	/// <inheritdoc/>
	protected override async Task DisposeContainerAsync(CancellationToken cancellationToken)
	{
		try
		{
			_client?.Dispose();

			if (_container is not null)
			{
				using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
				await _container.DisposeAsync().AsTask().WaitAsync(cts.Token).ConfigureAwait(false);
			}
		}
		catch (Exception)
		{
			// Suppress disposal errors and timeouts to prevent test host crash.
		}
	}
}

/// <summary>
/// xUnit collection definition for the DynamoDB persistence-provider conformance suite.
/// </summary>
[CollectionDefinition(CollectionName)]
public sealed class DynamoDbPersistenceProviderTestCollection
	: ICollectionFixture<DynamoDbPersistenceProviderContainerFixture>
{
	/// <summary>
	/// The collection name used by test classes.
	/// </summary>
	public const string CollectionName = "DynamoDb Persistence Provider Integration Tests";
}
