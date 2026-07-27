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
/// Real-infrastructure lock (bead bkra3g) for <see cref="RedisInboxStore"/>'s terminal-protected status
/// transitions: once an entry is finalized to <see cref="InboxStatus.Processed"/>, no later
/// <c>MarkProcessing</c> / <c>MarkFailed</c> may downgrade it back to a non-terminal state.
/// </summary>
/// <remarks>
/// <para>
/// <b>Hazard:</b> a blind GET→modify→SET could overwrite a terminal <c>Processed</c> entry with a
/// non-terminal status under concurrency → re-admit the message → double-processing. The fix routes
/// every mutating transition through an atomic Lua CAS (<c>GuardedTransitionIfNotProcessedScript</c>)
/// that refuses to write when the persisted status is already <c>Processed</c>.
/// </para>
/// <para>
/// <b>Non-vacuity (RED on pre-fix blind SET):</b> after finalize, a <c>MarkProcessing</c>/<c>MarkFailed</c>
/// on the pre-fix impl unconditionally SETs the entry to Processing/Failed → the status assertion is RED.
/// Determinism comes from the CAS guard, not timing — the sequential finalize-then-downgrade ordering is
/// sufficient to prove the guard (no sleep/barrier). The store is mocked nowhere — a fake StackExchange
/// connection cannot reproduce the server-side Lua CAS (per <c>verify-against-real-infra-not-mock</c>).
/// Never skipped: the Redis collection fixture fails fast when Docker is unavailable.
/// </para>
/// </remarks>
[Collection(RedisTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Database", "Redis")]
[Trait("Component", "Inbox")]
public sealed class RedisInboxStoreTerminalTransitionGuardShould
{
	private const string HandlerType = "TestHandler";
	private readonly RedisContainerFixture _fixture;

	public RedisInboxStoreTerminalTransitionGuardShould(RedisContainerFixture fixture)
	{
		_fixture = fixture;
	}

	private async Task<RedisInboxStore> CreateStoreAsync()
	{
		var options = Options.Create(new RedisInboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			KeyPrefix = $"inbox-terminal-guard-{Guid.NewGuid():N}",
			DefaultTtlSeconds = 604800,
			ConnectTimeoutMs = 5000,
			SyncTimeoutMs = 5000,
			AbortOnConnectFail = false,
		});

		var connection = await ConnectionMultiplexer.ConnectAsync(_fixture.ConnectionString).ConfigureAwait(false);
		return new RedisInboxStore(connection, options, NullLogger<RedisInboxStore>.Instance);
	}

	[Fact]
	public async Task Refuse_to_downgrade_a_Processed_entry_via_MarkProcessing()
	{
		var store = await CreateStoreAsync();
		var messageId = $"msg-guard-processing-{Guid.NewGuid():N}";
		var ct = CancellationToken.None;

		(await store.TryClaimAsync(messageId, HandlerType, ct)).ShouldBeTrue();
		await store.MarkProcessedAsync(messageId, HandlerType, ct);
		(await store.IsProcessedAsync(messageId, HandlerType, ct)).ShouldBeTrue();

		// Attempted downgrade — must be a no-op on the terminal entry.
		await store.MarkProcessingAsync(messageId, HandlerType, ct);

		var entry = await store.GetEntryAsync(messageId, HandlerType, ct);
		_ = entry.ShouldNotBeNull();
		entry.Status.ShouldBe(
			InboxStatus.Processed,
			"a terminal Processed entry must never be downgraded to Processing");
	}

	[Fact]
	public async Task Refuse_to_downgrade_a_Processed_entry_via_MarkFailed()
	{
		var store = await CreateStoreAsync();
		var messageId = $"msg-guard-failed-{Guid.NewGuid():N}";
		var ct = CancellationToken.None;

		(await store.TryClaimAsync(messageId, HandlerType, ct)).ShouldBeTrue();
		await store.MarkProcessedAsync(messageId, HandlerType, ct);
		(await store.IsProcessedAsync(messageId, HandlerType, ct)).ShouldBeTrue();

		// Attempted downgrade via both MarkFailed overloads — must be no-ops on the terminal entry.
		await store.MarkFailedAsync(messageId, HandlerType, "boom", ct);
		await store.MarkFailedAsync(messageId, HandlerType, "boom-again", retryCount: 3, ct);

		var entry = await store.GetEntryAsync(messageId, HandlerType, ct);
		_ = entry.ShouldNotBeNull();
		entry.Status.ShouldBe(
			InboxStatus.Processed,
			"a terminal Processed entry must never be downgraded to Failed");
	}

	[Fact]
	public async Task Hold_Processed_under_a_concurrent_downgrade_race()
	{
		const int Concurrency = 16;
		var store = await CreateStoreAsync();
		var messageId = $"msg-guard-race-{Guid.NewGuid():N}";
		var ct = CancellationToken.None;

		(await store.TryClaimAsync(messageId, HandlerType, ct)).ShouldBeTrue();
		await store.MarkProcessedAsync(messageId, HandlerType, ct);
		(await store.IsProcessedAsync(messageId, HandlerType, ct)).ShouldBeTrue();

		// N callers concurrently attempt to downgrade the terminal entry. The Lua CAS must reject all.
		var tasks = Enumerable.Range(0, Concurrency)
			.Select(i => Task.Run(async () =>
			{
				if (i % 2 == 0)
				{
					await store.MarkProcessingAsync(messageId, HandlerType, ct).ConfigureAwait(false);
				}
				else
				{
					await store.MarkFailedAsync(messageId, HandlerType, "race", ct).ConfigureAwait(false);
				}
			}))
			.ToArray();
		await Task.WhenAll(tasks).ConfigureAwait(false);

		var entry = await store.GetEntryAsync(messageId, HandlerType, ct);
		_ = entry.ShouldNotBeNull();
		entry.Status.ShouldBe(
			InboxStatus.Processed,
			$"none of {Concurrency} concurrent downgrade attempts may move a terminal Processed entry");
	}
}
