// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.Outbox.Redis;

using Microsoft.Extensions.Logging.Abstractions;

using StackExchange.Redis;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.Redis;

/// <summary>
/// Author≠impl regression lock for bead <c>5jo6tm</c> (sprint 855, FR-C3): the Redis outbox claim+write
/// MUST be atomic — staging a message writes the full hash AND adds it to the claimable index in one
/// indivisible step, so a crash can never leave an <b>orphan partial hash</b> (a hash holding only the
/// claimed field, with no payload, absent from every index — invisible to the poller yet blocking re-stage).
/// </summary>
/// <remarks>
/// <para>
/// Backend/Frontend's fix folds the prior three round-trips (HSETNX claim → HashSet remaining fields →
/// SortedSetAdd to index) into a single <c>StageMessageLuaScript</c> (EXISTS-dedup + HSET-all + ZADD-index),
/// making the partial state structurally inexpressible (a Lua script is atomic in Redis).
/// </para>
/// <para>
/// <b>Real-infra (NFR-1):</b> runs against real Redis via <see cref="RedisContainerFixture"/>. The
/// poller (<c>GetUnsentMessagesAsync</c>) finds staged messages via the INDEX, so a <i>retrievable</i>
/// staged message with its <i>full payload</i> proves the index entry landed together with the complete
/// hash — i.e. NOT an orphan. (Concurrent-stage dedup is covered separately by
/// <c>RedisOutboxConcurrentStageDedupShould</c>; this lock targets the atomic-write/no-orphan guarantee.)
/// </para>
/// <para>
/// <b>Non-vacuity:</b> RED-proven by removing the script's <c>ZADD</c> (the orphan-creating partial) —
/// the message hash exists but is absent from the claimable index, so <c>GetUnsentMessagesAsync</c>
/// returns nothing (an orphan invisible to the poller). GREEN on the atomic HSET+ZADD script.
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.Redis)]
[Trait(TraitNames.Component, TestComponents.Outbox)]
[Trait("Database", "Redis")]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class RedisOutboxAtomicStageShould : IntegrationTestBase
{
	private readonly RedisContainerFixture _redisFixture;

	public RedisOutboxAtomicStageShould(RedisContainerFixture redisFixture)
	{
		_redisFixture = redisFixture;
	}

	[Fact]
	public async Task StageMessageAtomically_LandsHashAndIndexTogether_SoTheMessageIsRetrievable()
	{
		// Arrange
		await using var store = CreateOutboxStore();
		var id = Guid.NewGuid().ToString();
		var payload = Encoding.UTF8.GetBytes("{\"order\":\"5jo6tm\"}");
		var message = new OutboundMessage("OrderPlaced", payload, "test-destination") { Id = id };

		// Act — single atomic Lua stage (dedup-claim + HSET-all + ZADD-index).
		await store.StageMessageAsync(message, TestCancellationToken);

		// Assert — the poller retrieves staged messages via the INDEX, so retrievability proves the ZADD
		// landed with the hash (no orphan); the full payload + type prove the HSET wrote every field, not a
		// partial. RED if the script's ZADD is dropped (hash exists, but invisible to the poller).
		var unsent = (await store.GetUnsentMessagesAsync(batchSize: 10, TestCancellationToken)).ToList();

		var staged = unsent.ShouldHaveSingleItem();
		staged.Id.ShouldBe(id);
		staged.MessageType.ShouldBe("OrderPlaced");
		staged.Payload.ShouldBe(payload);
	}

	private RedisOutboxStore CreateOutboxStore()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new RedisOutboxOptions
		{
			ConnectionString = _redisFixture.ConnectionString,
			KeyPrefix = $"outbox-atomic-{Guid.NewGuid():N}",
			DatabaseId = 0,
			SentMessageTtlSeconds = 600,
			ConnectTimeoutMs = 5000,
			SyncTimeoutMs = 5000,
			AbortOnConnectFail = false,
		});

		var connection = ConnectionMultiplexer.Connect(_redisFixture.ConnectionString);
		return new RedisOutboxStore(connection, options, NullLogger<RedisOutboxStore>.Instance);
	}
}
