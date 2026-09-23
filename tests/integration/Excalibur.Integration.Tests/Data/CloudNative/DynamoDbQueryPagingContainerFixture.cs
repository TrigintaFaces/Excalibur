// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;

using Testcontainers.LocalStack;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.Data.CloudNative;

/// <summary>
/// Real DynamoDB (LocalStack) fixture for the cloud-native query paging conformance.
/// </summary>
/// <remarks>
/// <para>
/// The continuation token is produced by the service, not by us: it is the serialized
/// <c>LastEvaluatedKey</c> DynamoDB returns. A faked client would return whatever second page the test
/// author imagined and would prove nothing about whether the token we hand back is a token the service
/// accepts. Only the real service closes that loop, so this fixture takes no skip path.
/// </para>
/// <para>
/// Inherits <see cref="ContainerFixtureBase"/>, which fails loudly when the container cannot start rather
/// than degrading into a silent skip.
/// </para>
/// </remarks>
public sealed class DynamoDbQueryPagingContainerFixture : ContainerFixtureBase
{
	private LocalStackContainer? _container;
	private AmazonDynamoDBClient? _client;

	/// <summary>
	/// Gets the table these tests query.
	/// </summary>
	public string TableName { get; } = $"paging_{Guid.NewGuid():N}";

	/// <summary>
	/// Gets the DynamoDB client pointing at the container, built with the SDK's default configuration.
	/// </summary>
	public IAmazonDynamoDB Client => _client
		?? throw new InvalidOperationException("Container not initialized");

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new LocalStackBuilder()
			.WithImage("localstack/localstack:4")
			.WithName($"localstack-querypaging-dynamodb-{Guid.NewGuid():N}")
			.WithEnvironment("SERVICES", "dynamodb")
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);

		var credentials = new BasicAWSCredentials("test", "test");
		var config = new AmazonDynamoDBConfig { ServiceURL = _container.GetConnectionString() };
		_client = new AmazonDynamoDBClient(credentials, config);

		// The provider's client-injecting constructor treats the store as already initialized, so it never
		// creates a table. The key schema mirrors what SerializeDocument writes: pk = partition key,
		// sk = document id, both strings.
		_ = await _client.CreateTableAsync(
			new CreateTableRequest
			{
				TableName = TableName,
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

		await WaitForTableAsync(cancellationToken).ConfigureAwait(false);
	}

	private async Task WaitForTableAsync(CancellationToken cancellationToken)
	{
		for (var attempt = 0; attempt < 60; attempt++)
		{
			var described = await _client!
				.DescribeTableAsync(TableName, cancellationToken).ConfigureAwait(false);

			if (described.Table.TableStatus == TableStatus.ACTIVE)
			{
				return;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
		}

		throw new InvalidOperationException($"Table '{TableName}' did not become ACTIVE.");
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
			// Suppress disposal errors and timeouts to prevent a test-host crash.
		}
	}
}
