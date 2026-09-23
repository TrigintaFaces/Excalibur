// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

using Excalibur.Inbox.Redis;

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
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
public sealed class RedisInboxStoreConformanceShould : InboxStoreConformanceTestBase
{
	private readonly RedisContainerFixture _fixture;
	private ConnectionMultiplexer? _connection;
	private string? _primaryKeyPrefix;

	/// <summary>
	/// Initializes a new instance of the <see cref="RedisInboxStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The Redis container fixture.</param>
	public RedisInboxStoreConformanceShould(RedisContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	/// <remarks>The same context <see cref="CreateStoreAsync"/> hands the store.</remarks>
	protected override ITenantContext StoreTenantContext => SingleTenantTestContext.Instance;

	/// <inheritdoc/>
	protected override async Task<IInboxStore> CreateStoreAsync()
	{
		var connectionString = _fixture.ConnectionString;
		_primaryKeyPrefix = $"inbox-test-{Guid.NewGuid():N}";
		var options = Options.Create(new RedisInboxOptions
		{
			ConnectionString = connectionString,
			KeyPrefix = _primaryKeyPrefix,
			DefaultTtlSeconds = 604800,
			ConnectTimeoutMs = 5000,
			SyncTimeoutMs = 5000,
			AbortOnConnectFail = false
		});

		// Create connection for test cleanup
		_connection = await ConnectionMultiplexer.ConnectAsync(_fixture.ConnectionString).ConfigureAwait(false);

		var logger = NullLogger<RedisInboxStore>.Instance;
		var store = new RedisInboxStore(_connection, options, logger, SingleTenantTestContext.Instance);

		return store;
	}

	// 2mek4x: CreateStoreAsync randomises KeyPrefix per call so DIFFERENT tests sharing the collection's
	// one container never collide -- which means a naive second CreateStoreAsync call for the fresh-
	// instance durability read-back would build a store pointed at a namespace of its own and could never
	// observe what the primary store wrote. Reuse the primary store's OWN prefix (and connection -- Redis
	// keys are visible to every connection against the same server, so sharing it costs nothing) so the
	// "fresh instance" is fresh in the sense this arm needs: an independent RedisInboxStore object, same
	// keyspace.
	/// <inheritdoc/>
	protected override Task<IInboxStore> CreateVerificationStoreAsync()
	{
		if (_connection is null || _primaryKeyPrefix is null)
		{
			throw new InvalidOperationException(
				"RedisInboxStoreConformanceShould.CreateStoreAsync must run before CreateVerificationStoreAsync.");
		}

		var options = Options.Create(new RedisInboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			KeyPrefix = _primaryKeyPrefix,
			DefaultTtlSeconds = 604800,
			ConnectTimeoutMs = 5000,
			SyncTimeoutMs = 5000,
			AbortOnConnectFail = false
		});

		IInboxStore store = new RedisInboxStore(
			_connection, options, NullLogger<RedisInboxStore>.Instance, SingleTenantTestContext.Instance);
		return Task.FromResult(store);
	}

	// 2mek4x: a real, provider-side persistence rejection -- never a mocked client. Denying the connecting
	// user's write command category via ACL makes every write Redis sees rejected with a genuine NOPERM,
	// then restores it. Reads (the fresh-store read-back this arm needs) are unaffected -- @write does not
	// cover GET-family commands -- and this does not touch data or replication topology, so it is safe to
	// reverse even if a step in between throws.
	/// <inheritdoc/>
	protected override async Task InjectPersistenceFaultAsync()
	{
		var connection = _connection ?? throw new InvalidOperationException(
			"RedisInboxStoreConformanceShould.CreateStoreAsync must run before InjectPersistenceFaultAsync.");
		var server = connection.GetServer(connection.GetEndPoints().First());
		_ = await server.ExecuteAsync("ACL", "SETUSER", "default", "-@write").ConfigureAwait(false);
	}

	/// <inheritdoc/>
	protected override async Task RemovePersistenceFaultAsync()
	{
		var connection = _connection ?? throw new InvalidOperationException(
			"RedisInboxStoreConformanceShould.CreateStoreAsync must run before RemovePersistenceFaultAsync.");
		var server = connection.GetServer(connection.GetEndPoints().First());
		_ = await server.ExecuteAsync("ACL", "SETUSER", "default", "+@write").ConfigureAwait(false);
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
