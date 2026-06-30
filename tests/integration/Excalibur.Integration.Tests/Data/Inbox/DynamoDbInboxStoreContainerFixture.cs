// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.DynamoDBv2;
using Amazon.Runtime;

using Testcontainers.LocalStack;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.Data.Inbox;

/// <summary>
/// Shared fixture for the DynamoDB <c>IInboxStore</c> real-infrastructure conformance tests, backed by a
/// LocalStack DynamoDB container.
/// </summary>
/// <remarks>
/// <para>
/// Mirrors the DynamoDb event/snapshot/saga fixtures' LocalStack setup. The <see cref="IAmazonDynamoDB"/>
/// client is built with the SDK's <em>default</em> configuration (only the LocalStack <c>ServiceURL</c>
/// and test credentials; no custom serializers), so the conformance suite exercises the same wire
/// behaviour a default consumer client produces.
/// </para>
/// <para>
/// The store auto-creates its table on the consumer-supplied-client path (its <c>InitializeAsync</c> runs
/// <c>CreateTableIfNotExists</c>), so this fixture performs no table DDL — the deriver lets the store create
/// its own per-test table and the fixture only deletes it during cleanup.
/// </para>
/// <para>
/// Inherits <see cref="ContainerFixtureBase"/>, which fails loudly (does not degrade gracefully) when the
/// container cannot start, so the real-infra conformance is never silently skipped.
/// </para>
/// </remarks>
public sealed class DynamoDbInboxStoreContainerFixture : ContainerFixtureBase
{
	private LocalStackContainer? _container;
	private AmazonDynamoDBClient? _client;

	/// <summary>
	/// Gets a unique base table name for this fixture's inbox tables.
	/// </summary>
	public string TableName { get; } = $"inbox_{Guid.NewGuid():N}";

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
			.WithImage("localstack/localstack:latest")
			.WithName($"localstack-inbox-dynamodb-{Guid.NewGuid():N}")
			.WithEnvironment("SERVICES", "dynamodb")
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);

		// Default SDK client config — only the LocalStack endpoint + test credentials, no custom serializers.
		var credentials = new BasicAWSCredentials("test", "test");
		var dynamoConfig = new AmazonDynamoDBConfig { ServiceURL = _container.GetConnectionString() };
		_client = new AmazonDynamoDBClient(credentials, dynamoConfig);
	}

	/// <summary>
	/// Deletes the named inbox table (best effort), used by the conformance class for per-test cleanup.
	/// </summary>
	/// <param name="tableName">The table to delete.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	public async Task DeleteTableAsync(string tableName, CancellationToken cancellationToken)
	{
		if (_client is null || string.IsNullOrEmpty(tableName))
		{
			return;
		}

		try
		{
			_ = await _client.DeleteTableAsync(tableName, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception)
		{
			// Best effort — the container disposal removes the data regardless.
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
