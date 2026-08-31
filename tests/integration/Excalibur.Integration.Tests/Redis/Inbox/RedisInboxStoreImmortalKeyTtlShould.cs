// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Inbox.Redis;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using StackExchange.Redis;

namespace Excalibur.Integration.Tests.Redis.Inbox;

/// <summary>
/// Real-infrastructure regression locks for <see cref="RedisInboxStore"/> (m0rm5r):
/// <list type="number">
///   <item><b>No immortal dedup key.</b> The terminal <c>TryMarkAsProcessedAsync</c> writes the value AND its
///   retention TTL in a SINGLE atomic <c>SET value EX ttl NX</c>. Pre-fix it was a SETNX followed by a SEPARATE
///   EXPIRE — a crash between the two left a terminal (Processed) key with NO expiry: an immortal dedup key that
///   never ages out. This lock asserts the key carries a TTL &gt; 0 in one round-trip after the terminal write.</item>
///   <item><b>Shared multiplexer is not torn down.</b> A caller-supplied (injected) <see cref="ConnectionMultiplexer"/>
///   is owned by the caller; <c>DisposeAsync</c> must NOT close it (other consumers share it). Pre-fix the store
///   tore down the shared connection.</item>
/// </list>
/// </summary>
/// <remarks>
/// Deterministic (no timed wait): TTL is read directly via <c>KeyTimeToLiveAsync</c>; connection liveness via
/// <c>IsConnected</c>. Redis via TestContainers is reliable and these locks are NEVER skipped.
/// </remarks>
[Collection(RedisTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Database", "Redis")]
[Trait("Component", "Inbox")]
public sealed class RedisInboxStoreImmortalKeyTtlShould
{
	private const string HandlerType = "TestHandler";
	private const int RetentionTtlSeconds = 3600;
	private readonly RedisContainerFixture _fixture;

	public RedisInboxStoreImmortalKeyTtlShould(RedisContainerFixture fixture)
	{
		_fixture = fixture;
		_fixture.DockerAvailable.ShouldBeTrue("Redis container must be available — this real-infra lock is never skipped.");
	}

	private (RedisInboxStore Store, string KeyPrefix) CreateStore(ConnectionMultiplexer connection)
	{
		var keyPrefix = $"inbox-immortal-{Guid.NewGuid():N}";
		var options = Options.Create(new RedisInboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			KeyPrefix = keyPrefix,
			DefaultTtlSeconds = RetentionTtlSeconds,
			ConnectTimeoutMs = 5000,
			SyncTimeoutMs = 5000,
			AbortOnConnectFail = false,
		});
		return (new RedisInboxStore(connection, options, NullLogger<RedisInboxStore>.Instance, SingleTenantTestContext.Instance), keyPrefix);
	}

	[Fact]
	public async Task Write_the_retention_ttl_atomically_with_the_terminal_TryMarkAsProcessed_value()
	{
		await using var connection = await ConnectionMultiplexer.ConnectAsync(_fixture.ConnectionString).ConfigureAwait(false);
		var (store, keyPrefix) = CreateStore(connection);
		var db = connection.GetDatabase();

		const string messageId = "msg-immortal-key";
		// RedisInboxStore key format. The store composes its resolved tenant into the key, and every
		// constructible context resolves one, so the locator carries the tenant segment. This is the
		// key the store writes for the context above — the property asserted below is unchanged.
		var key = $"{keyPrefix}:{TenantDefaults.DefaultTenantId}:{messageId}:{HandlerType}";

		// Terminal first-writer-wins finalize: value + retention TTL must be set together (atomic SET ... EX NX).
		(await store.TryMarkAsProcessedAsync(messageId, HandlerType, CancellationToken.None)).ShouldBeTrue();

		var ttl = await db.KeyTimeToLiveAsync(key);
		ttl.ShouldNotBeNull(
			"the terminal Processed dedup key must carry a retention TTL written atomically with its value — "
			+ "a SETNX-then-separate-EXPIRE could crash in between and leave an immortal (never-expiring) dedup key");
		ttl!.Value.ShouldBeGreaterThan(TimeSpan.Zero);
	}

	[Fact]
	public async Task Not_dispose_a_caller_supplied_shared_multiplexer_on_DisposeAsync()
	{
		await using var connection = await ConnectionMultiplexer.ConnectAsync(_fixture.ConnectionString).ConfigureAwait(false);
		var (store, _) = CreateStore(connection);

		connection.IsConnected.ShouldBeTrue("precondition: the injected multiplexer is connected before disposal");

		// Dispose the store — it must NOT tear down a connection it does not own.
		await store.DisposeAsync();

		connection.IsConnected.ShouldBeTrue(
			"a caller-supplied (injected) multiplexer is owned by the caller — the store must not close it on DisposeAsync, "
			+ "or other consumers sharing the same connection break");
	}
}
