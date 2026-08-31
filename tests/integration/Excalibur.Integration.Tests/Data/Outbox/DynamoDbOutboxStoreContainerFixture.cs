// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.DynamoDBv2;
using Amazon.Runtime;

using Excalibur.Outbox.DynamoDb;

using Testcontainers.LocalStack;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Shared fixture for the DynamoDB <c>ICloudNativeOutboxStore</c> real-infrastructure tests, backed by a
/// LocalStack DynamoDB container.
/// </summary>
/// <remarks>
/// <para>
/// Mirrors the DynamoDb event/snapshot/saga/inbox fixtures' LocalStack setup (see
/// <c>DynamoDbEventStoreContainerFixture</c>). <see cref="DynamoDbOutboxStore"/> builds its own client
/// from <c>DynamoDbOutboxOptions.Connection</c> rather than accepting an injected one, so this fixture
/// exposes only the LocalStack endpoint — the store's own <c>CreateTableIfNotExists</c> path provisions
/// the table on <c>InitializeAsync</c>.
/// </para>
/// <para>
/// Inherits <see cref="ContainerFixtureBase"/>, which fails loudly when the container cannot start, so
/// the real-infra lock is never silently skipped.
/// </para>
/// </remarks>
public sealed class DynamoDbOutboxStoreContainerFixture : ContainerFixtureBase
{
	private LocalStackContainer? _container;
	private AmazonDynamoDBClient? _client;

	/// <summary>
	/// Gets the LocalStack edge endpoint (the DynamoDB <c>ServiceUrl</c>).
	/// </summary>
	public string ServiceUrl => _container?.GetConnectionString()
		?? throw new InvalidOperationException("Container not initialized");

	/// <summary>
	/// Gets a DynamoDB client pointing at the LocalStack container, used only for per-test table cleanup.
	/// </summary>
	public IAmazonDynamoDB Client => _client
		?? throw new InvalidOperationException("Container not initialized");

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new LocalStackBuilder()
			.WithImage("localstack/localstack:4")
			.WithName($"localstack-outbox-dynamodb-{Guid.NewGuid():N}")
			.WithEnvironment("SERVICES", "dynamodb")
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);

		var credentials = new BasicAWSCredentials("test", "test");
		var config = new AmazonDynamoDBConfig { ServiceURL = _container.GetConnectionString() };
		_client = new AmazonDynamoDBClient(credentials, config);
	}

	/// <summary>
	/// Deletes the named outbox table (best effort), used for per-test cleanup.
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
