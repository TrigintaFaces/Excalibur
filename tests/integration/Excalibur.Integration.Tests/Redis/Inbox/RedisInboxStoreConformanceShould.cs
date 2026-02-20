// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Excalibur.Data.Redis.Inbox;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

using Tests.Shared.Conformance.Inbox;

namespace Excalibur.Integration.Tests.Redis.Inbox;

/// <summary>
/// Conformance tests for <see cref="RedisInboxStore"/> using the Inbox Conformance Test Kit.
/// </summary>
/// <remarks>
/// These tests verify that the Redis implementation correctly implements the
/// IInboxStore interface contract using Redis via TestContainers.
/// </remarks>
[Collection(RedisTestCollection.CollectionName)]
public sealed class RedisInboxStoreConformanceShould : InboxStoreConformanceTestBase
{
	private readonly RedisContainerFixture _fixture;
	private ConnectionMultiplexer? _connection;

	/// <summary>
	/// Initializes a new instance of the <see cref="RedisInboxStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The Redis container fixture.</param>
	public RedisInboxStoreConformanceShould(RedisContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override async Task<IInboxStore> CreateStoreAsync()
	{
		var connectionString = _fixture.ConnectionString;
		var options = Options.Create(new RedisInboxOptions
		{
			ConnectionString = connectionString,
			KeyPrefix = $"inbox-test-{Guid.NewGuid():N}",
			DefaultTtlSeconds = 604800,
			ConnectTimeoutMs = 5000,
			SyncTimeoutMs = 5000,
			AbortOnConnectFail = false
		});

		// Create connection for test cleanup
		_connection = await ConnectionMultiplexer.ConnectAsync(_fixture.ConnectionString).ConfigureAwait(false);

		var logger = NullLogger<RedisInboxStore>.Instance;
		var store = new RedisInboxStore(_connection, options, logger);

		return store;
	}

	/// <inheritdoc/>
	protected override async Task CleanupAsync()
	{
		// Clean up test keys
		if (_connection != null)
		{
			var server = _connection.GetServer(_connection.GetEndPoints().First());
			var database = _connection.GetDatabase();

			// Find and delete all test keys
			await foreach (var key in server.KeysAsync(pattern: "inbox-test-*"))
			{
				_ = await database.KeyDeleteAsync(key).ConfigureAwait(false);
			}

			// Close connection after cleanup
			await _connection.CloseAsync().ConfigureAwait(false);
			_connection.Dispose();
			_connection = null;
		}
	}
}
