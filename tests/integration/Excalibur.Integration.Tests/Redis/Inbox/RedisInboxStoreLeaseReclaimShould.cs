// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Inbox.Redis;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

using Tests.Shared.Infrastructure;

namespace Excalibur.Integration.Tests.Redis.Inbox;

/// <summary>
/// kj847e (S868) — independent (author≠impl, TestsDeveloper) NON-SKIPPED real-Redis concurrency lock for the
/// <b>lease-aware</b> atomic-claim overload
/// <c>IClaimableInboxStore.TryClaimAsync(messageId, handlerType, leaseDuration, ct)</c>. The CAS is a Lua
/// script keyed on the <c>leaseExpiresAt</c> hash field (unix-ms) evaluated against the <b>Redis server
/// clock</b> (<c>redis.call('TIME')</c>): claim IFF <c>absent OR Received OR (Processing AND leaseExpiry &lt;
/// now)</c>, NEVER when terminal <see cref="InboxStatus.Processed"/>.
/// </summary>
/// <remarks>
/// Complements <c>RedisInboxStoreClaimAtomicityShould</c> (the 2-arg overload, which STAYS). Redis is
/// schemaless — no fixture DDL; the store owns the <c>leaseExpiresAt</c> field. Reclaim is proven with a
/// short lease + real elapsed time (bounded poll, lower-bound only — no faked clock; per
/// <c>verify-against-real-infra</c>). <b>RED on a no-lease impl</b> (inherits the interface's
/// <see cref="System.NotSupportedException"/> default, or a claim-IFF-absent override): the expired
/// <see cref="InboxStatus.Processing"/> entry never becomes reclaimable ⇒ the poll times out ⇒ RED.
/// </remarks>
[Collection(RedisTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Database", "Redis")]
[Trait("Component", "Inbox")]
public sealed class RedisInboxStoreLeaseReclaimShould
{
	private const int Concurrency = 16;
	private static readonly TimeSpan ShortLease = TimeSpan.FromMilliseconds(250);
	private static readonly TimeSpan LongLease = TimeSpan.FromSeconds(30);

	private readonly RedisContainerFixture _fixture;

	public RedisInboxStoreLeaseReclaimShould(RedisContainerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task Admit_exactly_one_lease_claim_when_concurrent_callers_race_the_same_message()
	{
		await using var connection = await ConnectionMultiplexer.ConnectAsync(_fixture.ConnectionString).ConfigureAwait(false);
		var store = CreateStore(connection);
		const string messageId = "msg-lease-concurrent";
		const string handlerType = "TestHandler";

		var tasks = Enumerable.Range(0, Concurrency)
			.Select(_ => Task.Run(() => store.TryClaimAsync(messageId, handlerType, LongLease, CancellationToken.None).AsTask()))
			.ToArray();

		var results = await Task.WhenAll(tasks).ConfigureAwait(false);

		results.Count(claimed => claimed).ShouldBe(
			1,
			$"the lease CAS must admit exactly one of {Concurrency} concurrent claims; got [{string.Join(",", results)}]");
	}

	[Fact]
	public async Task Deny_a_second_claimer_while_the_lease_is_live()
	{
		await using var connection = await ConnectionMultiplexer.ConnectAsync(_fixture.ConnectionString).ConfigureAwait(false);
		var store = CreateStore(connection);
		const string messageId = "msg-lease-live";
		const string handlerType = "TestHandler";

		(await store.TryClaimAsync(messageId, handlerType, LongLease, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeTrue("first caller acquires the lease");
		(await store.TryClaimAsync(messageId, handlerType, LongLease, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeFalse("a live lease must deny a concurrent second claim (no double-processing)");
	}

	[Fact]
	public async Task Reclaim_the_message_after_the_lease_expires()
	{
		await using var connection = await ConnectionMultiplexer.ConnectAsync(_fixture.ConnectionString).ConfigureAwait(false);
		var store = CreateStore(connection);
		const string messageId = "msg-lease-expire";
		const string handlerType = "TestHandler";

		(await store.TryClaimAsync(messageId, handlerType, ShortLease, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeTrue("the dead processor acquires the initial lease");
		(await store.TryClaimAsync(messageId, handlerType, ShortLease, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeFalse("the lease is still live immediately after it was taken");

		// RED on a no-lease impl: an expired Processing entry never becomes reclaimable ⇒ this times out.
		var reclaimed = await WaitHelpers.WaitUntilAsync(
			async () => await store.TryClaimAsync(messageId, handlerType, ShortLease, CancellationToken.None).ConfigureAwait(false),
			timeout: TimeSpan.FromSeconds(10),
			pollInterval: TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);

		reclaimed.ShouldBeTrue(
			"an expired lease must let a new processor reclaim the abandoned message (Redis server-clock expiry)");
	}

	[Fact]
	public async Task Never_reclaim_a_terminal_processed_message()
	{
		await using var connection = await ConnectionMultiplexer.ConnectAsync(_fixture.ConnectionString).ConfigureAwait(false);
		var store = CreateStore(connection);
		const string messageId = "msg-lease-processed";
		const string handlerType = "TestHandler";

		(await store.TryClaimAsync(messageId, handlerType, ShortLease, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeTrue("claim the message for processing");
		await store.MarkProcessedAsync(messageId, handlerType, CancellationToken.None).ConfigureAwait(false);

		await Task.Delay(ShortLease + TimeSpan.FromMilliseconds(250)).ConfigureAwait(false);

		(await store.TryClaimAsync(messageId, handlerType, ShortLease, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeFalse("a completed (Processed) message must never be reclaimed via the lease path");
	}

	// d2afxn: a Failed entry is RE-ADMITTABLE on redelivery (retry). RED on the pre-fix predicate
	// (absent | Received | expired-Processing) which denies Failed → TryClaim false → silent drop.
	[Fact]
	public async Task Readmit_and_retry_a_failed_entry_on_redelivery()
	{
		await using var connection = await ConnectionMultiplexer.ConnectAsync(_fixture.ConnectionString).ConfigureAwait(false);
		var store = CreateStore(connection);
		const string messageId = "msg-lease-failed-readmit";
		const string handlerType = "TestHandler";

		(await store.TryClaimAsync(messageId, handlerType, LongLease, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeTrue("the initial claim acquires the lease");
		await store.MarkFailedAsync(messageId, handlerType, "handler boom", CancellationToken.None).ConfigureAwait(false);

		var afterFail = await store.GetEntryAsync(messageId, handlerType, CancellationToken.None).ConfigureAwait(false);
		afterFail.ShouldNotBeNull();
		afterFail!.Status.ShouldBe(InboxStatus.Failed);

		(await store.TryClaimAsync(messageId, handlerType, LongLease, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeTrue("a Failed entry MUST be re-admittable on redelivery (at-least-once + idempotent-handler contract)");

		var afterReclaim = await store.GetEntryAsync(messageId, handlerType, CancellationToken.None).ConfigureAwait(false);
		afterReclaim.ShouldNotBeNull();
		afterReclaim!.Status.ShouldBe(
			InboxStatus.Processing,
			"re-admitting a Failed entry transitions it back to Processing under a fresh lease");

		// d2afxn monotonic-RetryCount guarantee (SA-confirmed preserve-only design): re-admit PRESERVES the
		// retry history (never resets to 0); RetryCount increments exactly once per failed attempt at the shared
		// finalize. Impl-agnostic monotonic assertion — non-decreasing across re-admit, strictly greater after a
		// second failed attempt. RED on a reset-to-0 re-admit.
		var retriesAfterFirstFail = afterFail!.RetryCount;
		retriesAfterFirstFail.ShouldBeGreaterThanOrEqualTo(1, "the first failed attempt must record at least one retry");
		afterReclaim.RetryCount.ShouldBeGreaterThanOrEqualTo(
			retriesAfterFirstFail,
			"re-admitting a Failed entry must PRESERVE the retry count (never reset it to 0)");

		await store.MarkFailedAsync(messageId, handlerType, "handler boom again", CancellationToken.None).ConfigureAwait(false);

		var afterSecondFail = await store.GetEntryAsync(messageId, handlerType, CancellationToken.None).ConfigureAwait(false);
		afterSecondFail.ShouldNotBeNull();
		afterSecondFail!.RetryCount.ShouldBeGreaterThan(
			retriesAfterFirstFail,
			"RetryCount MUST be monotonic across re-admits — a second failed attempt strictly increases it, never resets");
	}

	private RedisInboxStore CreateStore(ConnectionMultiplexer connection)
	{
		// Unique key prefix per test-class instance isolates the (messageId,handlerType) keyspace.
		var options = Options.Create(new RedisInboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			KeyPrefix = $"inbox-lease-reclaim-{Guid.NewGuid():N}",
			DefaultTtlSeconds = 604800,
			ConnectTimeoutMs = 5000,
			SyncTimeoutMs = 5000,
			AbortOnConnectFail = false,
		});
		return new RedisInboxStore(connection, options, NullLogger<RedisInboxStore>.Instance);
	}
}
