// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;

using Excalibur.Dispatch;
using Excalibur.Inbox.Redis;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

using Tests.Shared.Infrastructure;

namespace Excalibur.Integration.Tests.Redis.Inbox;

/// <summary>
/// Real-Redis lock for the <b>fenced</b> half of the lease protocol: a caller whose lease has lapsed must
/// not be able to finalize the record of the caller that replaced it.
/// </summary>
/// <remarks>
/// <para>
/// The sibling suite (<c>RedisInboxStoreLeaseReclaimShould</c>) binds <i>admission</i> — who gets the lease.
/// This one binds <i>finalization</i> — who is still allowed to write once they have it. Passing admission
/// arms are not evidence for this: admission proves at most one caller is <i>told</i> it holds the message;
/// the fence is about whether the one who was told <i>keeps</i> it.
/// </para>
/// <para>
/// <b>Why real infrastructure.</b> The term is the expiry the SERVER resolved from <c>redis.call('TIME')</c>
/// inside the claim script, returned from that same script and compared inside the finalize script's own
/// atomic step. A mocked multiplexer returns whatever it was told and can certify neither half — nor the
/// round trip, which here is a Lua number formatted with <c>string.format('%.0f', ...)</c> on both sides.
/// Lua numbers are IEEE doubles, so a formatting divergence between the mint and the compare would make
/// every finalization by the legitimate holder silently fail closed and look exactly like an early expiry —
/// which is what <see cref="CompleteUnderALiveTerm"/> exists to catch.
/// </para>
/// <para>
/// <b>Determinism:</b> a short lease and real elapsed time, polled past expiry with a bounded wait. Every
/// assertion is an eventual truth or a lower bound, so load can only lengthen the poll — never flip an
/// outcome. No wall-clock upper bounds.
/// </para>
/// <para>
/// <b>Non-vacuity:</b> the SAFETY arms go RED against a finalization that carries no term. The LIVENESS arms
/// fail a store that simply refuses everything, which would otherwise satisfy every safety arm by doing
/// nothing. The MONOTONICITY arm goes RED against the one-token mutant that relaxes the reclaim comparison
/// from <c>&lt;</c> to <c>&lt;=</c>, which is the only way to make two terms compare equal.
/// </para>
/// </remarks>
[Collection(RedisTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Database", "Redis")]
[Trait("Component", "Inbox")]
public sealed class RedisInboxStoreLeaseFencingShould
{
	private static readonly TimeSpan ShortLease = TimeSpan.FromMilliseconds(750);
	private static readonly TimeSpan LongLease = TimeSpan.FromMinutes(5);
	private static readonly TimeSpan ReclaimDeadline = TimeSpan.FromSeconds(30);

	private readonly RedisContainerFixture _fixture;

	public RedisInboxStoreLeaseFencingShould(RedisContainerFixture fixture) => _fixture = fixture;

	/// <summary>
	/// Drives the two-caller lapse against the real store: A acquires under a short lease, the lease runs
	/// out on the SERVER clock, B reclaims. Returns both terms.
	/// </summary>
	private static async Task<(string MessageId, string HandlerType, LeaseToken TermA, LeaseToken TermB)>
		LapsedReclaimAsync(RedisInboxStore store)
	{
		var messageId = $"msg-{Guid.NewGuid():N}";
		var handlerType = $"handler-{Guid.NewGuid():N}";
		var ct = CancellationToken.None;

		var termA = (await store.TryAcquireLeaseAsync(messageId, handlerType, ShortLease, ct).ConfigureAwait(false))
			.ShouldNotBeNull("the first caller must be admitted on a key the store has never seen");

		LeaseToken? reclaimed = null;
		var deadline = DateTime.UtcNow + ReclaimDeadline;

		while (DateTime.UtcNow < deadline)
		{
			reclaimed = await store.TryAcquireLeaseAsync(messageId, handlerType, LongLease, ct).ConfigureAwait(false);

			if (reclaimed is not null)
			{
				break;
			}

			await Task.Delay(50, ct).ConfigureAwait(false);
		}

		var termB = reclaimed.ShouldNotBeNull(
			"an expired lease MUST be reclaimable, or a dead processor would block the message forever");

		return (messageId, handlerType, termA, termB);
	}

	// SAFETY (headline) — the lapsed caller cannot finalize its successor's record.
	[Fact]
	public async Task RefuseToCompleteUnderALapsedTerm()
	{
		await using var connection = await ConnectionMultiplexer.ConnectAsync(_fixture.ConnectionString).ConfigureAwait(false);
		var store = CreateStore(connection);
		var (messageId, handlerType, termA, _) = await LapsedReclaimAsync(store).ConfigureAwait(false);
		var ct = CancellationToken.None;

		(await store.CompleteAsync(messageId, handlerType, termA, ct).ConfigureAwait(false)).ShouldBeFalse(
			"A's lease had lapsed and been reclaimed, so its finalize must match no entry");

		(await store.IsProcessedAsync(messageId, handlerType, ct).ConfigureAwait(false)).ShouldBeFalse(
			"B is still processing; A must not have marked B's entry terminal");

		var entry = await store.GetEntryAsync(messageId, handlerType, ct).ConfigureAwait(false);
		entry.ShouldNotBeNull();
		entry.Status.ShouldBe(InboxStatus.Processing, "the entry still belongs to B");
	}

	// SAFETY — the failure path, which is the one that would resurrect a terminal entry.
	[Fact]
	public async Task RefuseToFailUnderALapsedTerm()
	{
		await using var connection = await ConnectionMultiplexer.ConnectAsync(_fixture.ConnectionString).ConfigureAwait(false);
		var store = CreateStore(connection);
		var (messageId, handlerType, termA, _) = await LapsedReclaimAsync(store).ConfigureAwait(false);
		var ct = CancellationToken.None;

		(await store.FailAsync(messageId, handlerType, termA, "A threw after losing its lease", ct).ConfigureAwait(false))
			.ShouldBeFalse("A's lease had lapsed, so its failure must not be recorded against B's entry");

		var entry = await store.GetEntryAsync(messageId, handlerType, ct).ConfigureAwait(false);
		entry.ShouldNotBeNull();
		entry.Status.ShouldBe(InboxStatus.Processing, "B is still processing; A must not have marked it Failed");
	}

	// LIVENESS — the fence must not block the caller it belongs to. This is the arm that catches a term
	// lost or gained between the Lua mint and the Lua compare.
	[Fact]
	public async Task CompleteUnderALiveTerm()
	{
		await using var connection = await ConnectionMultiplexer.ConnectAsync(_fixture.ConnectionString).ConfigureAwait(false);
		var store = CreateStore(connection);
		var messageId = $"msg-{Guid.NewGuid():N}";
		var handlerType = $"handler-{Guid.NewGuid():N}";
		var ct = CancellationToken.None;

		var term = (await store.TryAcquireLeaseAsync(messageId, handlerType, LongLease, ct).ConfigureAwait(false))
			.ShouldNotBeNull();

		(await store.CompleteAsync(messageId, handlerType, term, ct).ConfigureAwait(false)).ShouldBeTrue(
			"the holder of a live term must be able to finalize — if this is RED the term does not survive "
			+ "the round-trip through the entry document and every finalization fails closed");

		(await store.IsProcessedAsync(messageId, handlerType, ct).ConfigureAwait(false)).ShouldBeTrue();
	}

	// LIVENESS — the failure path, and the term being cleared so a redelivery is immediately re-admittable.
	[Fact]
	public async Task FailUnderALiveTermAndLeaveTheEntryReAdmittable()
	{
		await using var connection = await ConnectionMultiplexer.ConnectAsync(_fixture.ConnectionString).ConfigureAwait(false);
		var store = CreateStore(connection);
		var messageId = $"msg-{Guid.NewGuid():N}";
		var handlerType = $"handler-{Guid.NewGuid():N}";
		var ct = CancellationToken.None;

		var term = (await store.TryAcquireLeaseAsync(messageId, handlerType, LongLease, ct).ConfigureAwait(false))
			.ShouldNotBeNull();

		(await store.FailAsync(messageId, handlerType, term, "handler failed", ct).ConfigureAwait(false)).ShouldBeTrue(
			"the holder of a live term must be able to record its own failure");

		(await store.TryAcquireLeaseAsync(messageId, handlerType, LongLease, ct).ConfigureAwait(false))
			.ShouldNotBeNull("a Failed entry carries no holder, so a redelivery must be admitted immediately");
	}

	// LIVENESS — the arm that catches a fence which simply refuses everything.
	[Fact]
	public async Task StillLetTheReclaimingCallerFinalizeAfterTheLapsedOneIsRefused()
	{
		await using var connection = await ConnectionMultiplexer.ConnectAsync(_fixture.ConnectionString).ConfigureAwait(false);
		var store = CreateStore(connection);
		var (messageId, handlerType, termA, termB) = await LapsedReclaimAsync(store).ConfigureAwait(false);
		var ct = CancellationToken.None;

		(await store.CompleteAsync(messageId, handlerType, termA, ct).ConfigureAwait(false)).ShouldBeFalse();

		(await store.CompleteAsync(messageId, handlerType, termB, ct).ConfigureAwait(false)).ShouldBeTrue(
			"the live holder must still finalize after the lapsed caller was fenced out");
		(await store.IsProcessedAsync(messageId, handlerType, ct).ConfigureAwait(false)).ShouldBeTrue();
	}

	// MONOTONICITY, measured on the server clock rather than inspected.
	//
	// Reclaim admits only when the recorded expiry is STRICTLY less than the server's TIME, and the
	// replacement is that same instant plus a non-negative duration. Relaxing that one comparison to <=
	// would let a reclaim reissue the same term and the fence would stop discriminating without failing.
	[Fact]
	public async Task IssueAStrictlyGreaterTermToTheReclaimingCaller()
	{
		await using var connection = await ConnectionMultiplexer.ConnectAsync(_fixture.ConnectionString).ConfigureAwait(false);
		var store = CreateStore(connection);
		var (_, _, termA, termB) = await LapsedReclaimAsync(store).ConfigureAwait(false);

		termB.ShouldNotBe(termA, "a reclaim that reissued the same term would fence nothing");

		var a = long.Parse(termA.Value, CultureInfo.InvariantCulture);
		var b = long.Parse(termB.Value, CultureInfo.InvariantCulture);

		b.ShouldBeGreaterThan(a,
			"the reclaimed term must be STRICTLY greater than the one it displaced — that is what makes the "
			+ "value an identity rather than merely a deadline");
	}

	private RedisInboxStore CreateStore(ConnectionMultiplexer connection)
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Redis container must be available — the real-infra fencing lock is never skipped.");

		// Unique key prefix per store isolates the (messageId, handlerType) keyspace from every other suite.
		var options = Options.Create(new RedisInboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			KeyPrefix = $"inbox-lease-fencing-{Guid.NewGuid():N}",
			DefaultTtlSeconds = 604800,
			ConnectTimeoutMs = 5000,
			SyncTimeoutMs = 5000,
			AbortOnConnectFail = false,
		});

		return new RedisInboxStore(connection, options, NullLogger<RedisInboxStore>.Instance, SingleTenantTestContext.Instance);
	}
}
