// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.DynamoDBStreams;
using Amazon.DynamoDBv2;
using Amazon.Runtime;

using Testcontainers.LocalStack;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Shared fixture for the DynamoDB <c>IEventStore</c> real-infrastructure conformance tests.
/// </summary>
/// <remarks>
/// <para>
/// Mirrors the DynamoDb snapshot-store and saga-store fixtures' LocalStack (DynamoDB) container setup.
/// It exposes both an <see cref="IAmazonDynamoDB"/> and an <see cref="IAmazonDynamoDBStreams"/> client —
/// the event store's constructor requires both — each built with the SDK's <em>default</em> configuration
/// (only the LocalStack <c>ServiceURL</c> and test credentials are supplied; no custom
/// serializers/converters), so the conformance suite exercises the same wire behaviour a default consumer
/// client produces.
/// </para>
/// <para>
/// Inherits <see cref="ContainerFixtureBase"/>, which fails loudly (does not degrade gracefully) when the
/// container cannot start, so the real-infra conformance is never silently skipped.
/// </para>
/// <para>
/// Unlike the snapshot store, the event store's client-injecting constructor leaves the store
/// <em>uninitialised</em>, so its own <c>CreateTableIfNotExists</c> path runs on first use and creates the
/// events table (HASH <c>pk</c> string + RANGE <c>sk</c> numeric). The conformance class therefore lets the
/// store create its own per-test table and deletes it via this fixture's client during cleanup; the fixture
/// itself creates no table.
/// </para>
/// </remarks>
public sealed class DynamoDbEventStoreContainerFixture : ContainerFixtureBase
{
	private LocalStackContainer? _container;
	private AmazonDynamoDBClient? _client;
	private AmazonDynamoDBStreamsClient? _streamsClient;

	/// <summary>
	/// Gets a unique base table name for this fixture's events tables.
	/// </summary>
	public string TableName { get; } = $"events_{Guid.NewGuid():N}";

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

	/// <summary>
	/// Gets the DynamoDB Streams client pointing at the LocalStack container, built with the SDK default config.
	/// </summary>
	public IAmazonDynamoDBStreams StreamsClient => _streamsClient
		?? throw new InvalidOperationException("Container not initialized");

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new LocalStackBuilder()
			.WithImage("localstack/localstack:latest")
			.WithName($"localstack-eventstore-dynamodb-{Guid.NewGuid():N}")
			.WithEnvironment("SERVICES", "dynamodb")
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);

		// Default SDK client config — only the LocalStack endpoint + test credentials, no custom serializers.
		var credentials = new BasicAWSCredentials("test", "test");
		var serviceUrl = _container.GetConnectionString();

		var dynamoConfig = new AmazonDynamoDBConfig { ServiceURL = serviceUrl };
		_client = new AmazonDynamoDBClient(credentials, dynamoConfig);

		var streamsConfig = new AmazonDynamoDBStreamsConfig { ServiceURL = serviceUrl };
		_streamsClient = new AmazonDynamoDBStreamsClient(credentials, streamsConfig);
	}

	/// <summary>
	/// Deletes the named events table (best effort), used by the conformance class for per-test cleanup.
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
			_streamsClient?.Dispose();
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
