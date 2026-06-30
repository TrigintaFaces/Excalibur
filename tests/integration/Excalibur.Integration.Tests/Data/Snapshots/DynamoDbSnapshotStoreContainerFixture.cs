// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.DynamoDBv2;
using Amazon.Runtime;

using Testcontainers.LocalStack;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.Data.Snapshots;

/// <summary>
/// Shared fixture for the DynamoDB SnapshotStore real-infrastructure conformance tests.
/// </summary>
/// <remarks>
/// <para>
/// Mirrors the DynamoDb saga-store fixture's LocalStack (DynamoDB) container setup and exposes an
/// <see cref="IAmazonDynamoDB"/> client built with the SDK's <em>default</em> configuration (only the
/// LocalStack <c>ServiceURL</c> and test credentials are supplied — no custom serializers/converters),
/// so the conformance suite exercises the same wire behaviour a default consumer client produces.
/// </para>
/// <para>
/// Inherits <see cref="ContainerFixtureBase"/>, which fails loudly (does not degrade gracefully) when
/// the container cannot start, so the real-infra conformance is never silently skipped.
/// </para>
/// <para>
/// The store auto-creates its own table on first use (the client-injecting constructor preserves the
/// injected client and still honours <c>CreateTableIfNotExists</c>), so the fixture performs no DDL — it
/// exercises the real consumer-supplied-client auto-create path. The table is deleted on dispose.
/// </para>
/// </remarks>
public sealed class DynamoDbSnapshotStoreContainerFixture : ContainerFixtureBase
{
	private LocalStackContainer? _container;
	private AmazonDynamoDBClient? _client;

	/// <summary>
	/// Gets the unique table name for this fixture's snapshots.
	/// </summary>
	public string TableName { get; } = $"snapshots_{Guid.NewGuid():N}";

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
			.WithName($"localstack-snapshot-dynamodb-{Guid.NewGuid():N}")
			.WithEnvironment("SERVICES", "dynamodb")
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);

		// Default SDK client config — only the LocalStack endpoint + test credentials, no custom serializers.
		var config = new AmazonDynamoDBConfig { ServiceURL = _container.GetConnectionString() };
		_client = new AmazonDynamoDBClient(new BasicAWSCredentials("test", "test"), config);

		// No DDL: the store auto-creates its table on first use via CreateTableIfNotExists.
	}

	/// <inheritdoc/>
	protected override async Task DisposeContainerAsync(CancellationToken cancellationToken)
	{
		try
		{
			if (_client is not null)
			{
				try
				{
					_ = await _client.DeleteTableAsync(TableName, cancellationToken).ConfigureAwait(false);
				}
				catch (Exception)
				{
					// Best effort — the container disposal below removes the data regardless.
				}

				_client.Dispose();
			}

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
