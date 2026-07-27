// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Outbox.Redis;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using StackExchange.Redis;

namespace Excalibur.Integration.Tests.Redis.Outbox;

/// <summary>
/// Real-infrastructure regression locks for <see cref="RedisOutboxStore"/> (m0rm5r):
/// <list type="number">
///   <item><b>No immortal Sent key.</b> The terminal <c>MarkSentAsync</c> applies the sent-message retention TTL
///   inside the SAME atomic Lua script that flips the status to Sent — so a crash can never leave a Sent message
///   with no expiry. This lock asserts the message key carries a TTL &gt; 0 after <c>MarkSentAsync</c>.</item>
///   <item><b>Shared multiplexer is not torn down.</b> A caller-supplied (injected) <see cref="ConnectionMultiplexer"/>
///   is owned by the caller; <c>DisposeAsync</c> must NOT close it.</item>
/// </list>
/// </summary>
/// <remarks>
/// Deterministic (no timed wait): TTL is read directly via <c>KeyTimeToLiveAsync</c>; connection liveness via
/// <c>IsConnected</c>. Redis via TestContainers is reliable and these locks are NEVER skipped.
/// </remarks>
[Collection(RedisTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Database", "Redis")]
[Trait("Component", "Outbox")]
public sealed class RedisOutboxStoreImmortalKeyTtlShould
{
	private const int SentTtlSeconds = 604_800;
	private readonly RedisContainerFixture _fixture;

	public RedisOutboxStoreImmortalKeyTtlShould(RedisContainerFixture fixture)
	{
		_fixture = fixture;
		_fixture.DockerAvailable.ShouldBeTrue("Redis container must be available — this real-infra lock is never skipped.");
	}

	private (RedisOutboxStore Store, string KeyPrefix) CreateStore(ConnectionMultiplexer connection)
	{
		var keyPrefix = $"outbox-immortal-{Guid.NewGuid():N}";
		var options = Options.Create(new RedisOutboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			KeyPrefix = keyPrefix,
			SentMessageTtlSeconds = SentTtlSeconds,
			ConnectTimeoutMs = 5000,
			SyncTimeoutMs = 5000,
			AbortOnConnectFail = false,
		});
		return (new RedisOutboxStore(connection, options, NullLogger<RedisOutboxStore>.Instance), keyPrefix);
	}

	[Fact]
	public async Task Apply_the_sent_retention_ttl_atomically_with_the_terminal_MarkSent_transition()
	{
		await using var connection = await ConnectionMultiplexer.ConnectAsync(_fixture.ConnectionString).ConfigureAwait(false);
		var (store, keyPrefix) = CreateStore(connection);
		var db = connection.GetDatabase();
		var ct = CancellationToken.None;

		var message = new OutboundMessage("Test.MessageType", "payload"u8.ToArray(), "test-queue")
		{
			Id = $"msg-immortal-{Guid.NewGuid():N}",
		};
		var key = $"{keyPrefix}:msg:{message.Id}"; // RedisOutboxStore message-key format

		// Stage then mark sent — the terminal transition must stamp the retention TTL in the same atomic script.
		await store.StageMessageAsync(message, ct);
		await store.MarkSentAsync(message.Id, ct);

		var ttl = await db.KeyTimeToLiveAsync(key);
		ttl.ShouldNotBeNull(
			"the terminal Sent message key must carry the retention TTL applied within the same atomic MarkSent script — "
			+ "a status-flip-then-separate-EXPIRE could crash in between and leave an immortal (never-expiring) Sent key");
		ttl!.Value.ShouldBeGreaterThan(TimeSpan.Zero);
	}

	[Fact]
	public async Task Not_dispose_a_caller_supplied_shared_multiplexer_on_DisposeAsync()
	{
		await using var connection = await ConnectionMultiplexer.ConnectAsync(_fixture.ConnectionString).ConfigureAwait(false);
		var (store, _) = CreateStore(connection);

		connection.IsConnected.ShouldBeTrue("precondition: the injected multiplexer is connected before disposal");

		await store.DisposeAsync();

		connection.IsConnected.ShouldBeTrue(
			"a caller-supplied (injected) multiplexer is owned by the caller — the store must not close it on DisposeAsync, "
			+ "or other consumers sharing the same connection break");
	}
}
