// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

using Excalibur.Data.Redis.Outbox;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

using Tests.Shared.Conformance.Outbox;

namespace Excalibur.Integration.Tests.Redis.Outbox;

/// <summary>
/// Conformance tests for <see cref="RedisOutboxStore"/> using the Outbox Conformance Test Kit.
/// </summary>
/// <remarks>
/// These tests verify that the Redis implementation correctly implements the
/// IOutboxStore interface contract using Redis via TestContainers.
/// </remarks>
[Collection(RedisTestCollection.CollectionName)]
public sealed class RedisOutboxStoreConformanceShould : OutboxStoreConformanceTestBase
{
	private readonly RedisContainerFixture _fixture;
	private ConnectionMultiplexer? _connection;
	private string _keyPrefix = string.Empty;

	/// <summary>
	/// Initializes a new instance of the <see cref="RedisOutboxStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The Redis container fixture.</param>
	public RedisOutboxStoreConformanceShould(RedisContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override async Task<IOutboxStore> CreateStoreAsync()
	{
		_keyPrefix = $"outbox-test-{Guid.NewGuid():N}";
		var connectionString = _fixture.ConnectionString;
		var options = Options.Create(new RedisOutboxOptions
		{
			ConnectionString = connectionString,
			KeyPrefix = _keyPrefix,
			SentMessageTtlSeconds = 604800,
			ConnectTimeoutMs = 5000,
			SyncTimeoutMs = 5000,
			AbortOnConnectFail = false
		});

		// Create connection for test cleanup
		_connection = await ConnectionMultiplexer.ConnectAsync(_fixture.ConnectionString).ConfigureAwait(false);

		var logger = NullLogger<RedisOutboxStore>.Instance;
		var store = new RedisOutboxStore(_connection, options, logger);

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

			// Find and delete all test keys matching our prefix
			await foreach (var key in server.KeysAsync(pattern: $"{_keyPrefix}*"))
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
