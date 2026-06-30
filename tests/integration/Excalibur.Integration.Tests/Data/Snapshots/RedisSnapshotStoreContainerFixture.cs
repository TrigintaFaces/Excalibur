// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using StackExchange.Redis;

using Testcontainers.Redis;

using Tests.Shared.Fixtures;

namespace Excalibur.Integration.Tests.Data.Snapshots;

/// <summary>
/// Shared fixture for Redis SnapshotStore TestContainers.
/// </summary>
/// <remarks>
/// Creates and manages a Redis container for the snapshot store. Redis is schemaless, so no DDL
/// is required. Between tests the fixture issues a <c>FLUSHDB</c> via a dedicated admin connection
/// so each conformance test runs against a clean keyspace; the store under test still connects with
/// the default (non-admin) client surface that consumers use.
/// </remarks>
public sealed class RedisSnapshotStoreContainerFixture : ContainerFixtureBase
{
	private RedisContainer? _container;
	private ConnectionMultiplexer? _adminConnection;

	/// <summary>
	/// Gets the connection string for the Redis container.
	/// </summary>
	public string ConnectionString => _container?.GetConnectionString()
		?? throw new InvalidOperationException("Container not initialized");

	/// <inheritdoc/>
	protected override async Task InitializeContainerAsync(CancellationToken cancellationToken)
	{
		_container = new RedisBuilder()
			.WithImage("redis:7-alpine")
			.WithName($"redis-snapshotstore-test-{Guid.NewGuid():N}")
			.WithCleanUp(true)
			.Build();

		await _container.StartAsync(cancellationToken).ConfigureAwait(false);

		// Dedicated admin connection used ONLY for between-test FLUSHDB cleanup.
		// The store under test connects separately with the default client surface.
		var config = ConfigurationOptions.Parse(ConnectionString);
		config.AllowAdmin = true;
		_adminConnection = await ConnectionMultiplexer.ConnectAsync(config).ConfigureAwait(false);
	}

	/// <summary>
	/// Flushes the Redis database to isolate state between conformance tests.
	/// </summary>
	public async Task CleanupAsync()
	{
		if (_adminConnection is null)
		{
			return;
		}

		foreach (var endpoint in _adminConnection.GetEndPoints())
		{
			var server = _adminConnection.GetServer(endpoint);
			await server.FlushDatabaseAsync().ConfigureAwait(false);
		}
	}

	/// <inheritdoc/>
	protected override async Task DisposeContainerAsync(CancellationToken cancellationToken)
	{
		try
		{
			if (_adminConnection is not null)
			{
				await _adminConnection.DisposeAsync().ConfigureAwait(false);
			}
		}
		catch (Exception)
		{
			// Suppress disposal errors to prevent test host crash.
		}

		try
		{
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
