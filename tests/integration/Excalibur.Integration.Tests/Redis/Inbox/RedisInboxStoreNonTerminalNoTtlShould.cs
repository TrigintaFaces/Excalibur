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
/// Real-infrastructure regression lock for <see cref="RedisInboxStore"/> (qau9u8): a non-terminal claim key must carry
/// NO expiry, so a dedup key can never expire while the handler is still running and re-admit the message
/// (double-processing). The retention TTL applies only on the terminal <c>Processed</c> transition.
/// </summary>
/// <remarks>
/// Deterministic (no timed wait): the test queries the Redis key's TTL directly. After a claim, the key's TTL must be
/// unset (<c>KeyTimeToLiveAsync</c> returns <see langword="null"/> = persistent / <c>TTL -1</c>); after the terminal
/// <c>MarkProcessedAsync</c>, the key must carry the retention TTL (non-null). <b>RED mutant:</b> restore the TTL on
/// the claim path → the claim key gets an expiry → the first assertion fails (and at runtime the message would be
/// re-admitted mid-handler = double-processing). Redis via TestContainers is reliable; never skipped.
/// </remarks>
[Collection(RedisTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Database", "Redis")]
[Trait("Component", "Inbox")]
public sealed class RedisInboxStoreNonTerminalNoTtlShould
{
	private const string HandlerType = "TestHandler";
	private const int RetentionTtlSeconds = 3600;
	private readonly RedisContainerFixture _fixture;

	public RedisInboxStoreNonTerminalNoTtlShould(RedisContainerFixture fixture)
	{
		_fixture = fixture;
	}

	private (RedisInboxStore Store, ConnectionMultiplexer Connection, string KeyPrefix) CreateStore(ConnectionMultiplexer connection)
	{
		var keyPrefix = $"inbox-noTtl-{Guid.NewGuid():N}";
		var options = Options.Create(new RedisInboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			KeyPrefix = keyPrefix,
			DefaultTtlSeconds = RetentionTtlSeconds,
			ConnectTimeoutMs = 5000,
			SyncTimeoutMs = 5000,
			AbortOnConnectFail = false,
		});
		return (new RedisInboxStore(connection, options, NullLogger<RedisInboxStore>.Instance), connection, keyPrefix);
	}

	[Fact]
	public async Task Set_no_expiry_on_a_non_terminal_claim_but_keep_the_retention_ttl_on_the_terminal_state()
	{
		await using var connection = await ConnectionMultiplexer.ConnectAsync(_fixture.ConnectionString).ConfigureAwait(false);
		var (store, _, keyPrefix) = CreateStore(connection);
		var db = connection.GetDatabase();

		const string messageId = "msg-noTtl-claim";
		var key = $"{keyPrefix}:{messageId}:{HandlerType}"; // RedisInboxStore key format
		var ct = CancellationToken.None;

		// Non-terminal claim (Processing): the key must have NO expiry, else it could lapse mid-handler.
		(await store.TryClaimAsync(messageId, HandlerType, ct)).ShouldBeTrue();
		(await db.KeyTimeToLiveAsync(key)).ShouldBeNull(
			"a non-terminal claim must carry no expiry (TTL -1) — a retention TTL here would re-admit the in-flight "
			+ "message on expiry = double-processing");

		// Terminal Processed: the retention TTL IS applied (the dedup record is allowed to age out once finalized).
		await store.MarkProcessedAsync(messageId, HandlerType, ct);
		(await db.KeyTimeToLiveAsync(key)).ShouldNotBeNull(
			"the terminal Processed state must carry the retention TTL");
	}
}
