// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Outbox.Redis;

using Microsoft.Extensions.Logging.Abstractions;

using StackExchange.Redis;

using Tests.Shared.Fixtures;
using Tests.Shared.Infrastructure;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.Redis;

/// <summary>
/// bd-5gtfje real-infra regression lock (author≠impl, TestsDeveloper; seam pinned by PlatformDeveloper 17598):
/// <see cref="RedisOutboxStore.GetUnsentMessagesAsync"/> is an <b>atomic disjoint lease-claim</b> over the
/// staged sorted-set (SQS-style visibility timeout) — concurrent processors never claim the same message
/// (FR-J1.1/J1.4), an expired lease is reclaimed so an in-flight message is never lost (FR-J1.2/J1.5), and a
/// terminal <see cref="RedisOutboxStore.MarkSentAsync"/> clears the lease so the message is not re-claimable
/// (FR-J1.3).
/// </summary>
/// <remarks>
/// <para>
/// <b>Real infrastructure, never skipped</b> (<c>verify-against-real-infra-not-mock</c>): the atomic Lua
/// ZREM-from-staged disjoint guarantee and the time-based lease reclaim cannot be reproduced by a mock — this
/// runs against a real Redis via TestContainers and asserts <see cref="ContainerFixtureBase.DockerAvailable"/>
/// rather than skip-gating. Serial (<c>-m:1</c>); per-test isolation via a unique <c>KeyPrefix</c>.
/// </para>
/// <para>
/// <b>Non-vacuous:</b> the disjoint test is RED on a non-atomic read-then-remove claim (both processors read
/// the same staged ids before either removes them → overlap); the reclaim test is RED on a no-lease /
/// no-reclaim impl (the message is never returned again after the first claim → lost); the mark-sent test is
/// RED if the terminal transition does not ZREM the leased index (the message stays claimable).
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.Redis)]
[Trait("Database", "Redis")]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Feature", "Outbox")]
public sealed class RedisOutboxLeaseClaimShould : IntegrationTestBase
{
	/// <summary>
	/// How long the reclaim arm keeps asking before it calls the in-flight message lost. Generous on purpose:
	/// it absorbs the container-clock stalls documented at the assertion below, and is not a tolerance on the
	/// store's behaviour — an implementation that never reclaims still fails when it expires.
	/// </summary>
	private static readonly TimeSpan ReclaimWindow = TimeSpan.FromSeconds(30);

	/// <summary>How often the reclaim arm re-asks while the window is open.</summary>
	private static readonly TimeSpan ReclaimPollInterval = TimeSpan.FromMilliseconds(250);

	private readonly RedisContainerFixture _redisFixture;

	public RedisOutboxLeaseClaimShould(RedisContainerFixture redisFixture)
	{
		_redisFixture = redisFixture;
	}

	[Fact]
	public async Task ClaimDisjointSets_WhenTwoProcessorsConcurrentlyClaimTheSameStagedBatch()
	{
		_redisFixture.DockerAvailable.ShouldBeTrue(
			"the real-Redis disjoint-claim lock must never be skipped (verify-against-real-infra-not-mock)");

		var prefix = $"outbox-5gtfje-disjoint-{Guid.NewGuid():N}";
		await using var seeder = CreateStore(prefix, processorId: "seeder");

		const int staged = 20;
		var stagedIds = new List<string>(staged);
		for (var i = 0; i < staged; i++)
		{
			var msg = NewMessage();
			stagedIds.Add(msg.Id);
			await seeder.StageMessageAsync(msg, TestCancellationToken);
		}

		// Two independent processors (distinct lease owners) claim from the SAME staged index concurrently.
		await using var processorA = CreateStore(prefix, processorId: "proc-a");
		await using var processorB = CreateStore(prefix, processorId: "proc-b");

		var claims = await Task.WhenAll(
			Task.Run(async () => (await processorA.GetUnsentMessagesAsync(staged, TestCancellationToken)).Select(m => m.Id).ToList(), TestCancellationToken),
			Task.Run(async () => (await processorB.GetUnsentMessagesAsync(staged, TestCancellationToken)).Select(m => m.Id).ToList(), TestCancellationToken));

		var claimedA = claims[0];
		var claimedB = claims[1];

		// DISJOINT: no message is claimed by both processors (the atomic ZREM-from-staged guarantee).
		var overlap = claimedA.Intersect(claimedB, StringComparer.Ordinal).ToList();
		overlap.ShouldBeEmpty(
			$"FR-J1.1/J1.4: concurrent lease-claims must be disjoint — {overlap.Count} id(s) were claimed by BOTH "
			+ "processors (a non-atomic read-then-remove claim would double-deliver these).");

		// Union ⊆ staged (no phantom ids), and each claimed id is unique within its own claim.
		var union = claimedA.Concat(claimedB).ToList();
		union.ShouldAllBe(id => stagedIds.Contains(id), "every claimed id must be one of the staged messages");
		union.Count.ShouldBe(union.Distinct(StringComparer.Ordinal).Count(), "no id may appear twice across the claims");
	}

	[Fact]
	public async Task ReclaimExpiredLease_SoAnInFlightMessageIsNeverLost()
	{
		_redisFixture.DockerAvailable.ShouldBeTrue(
			"the real-Redis lease-reclaim lock must never be skipped (verify-against-real-infra-not-mock)");

		var prefix = $"outbox-5gtfje-reclaim-{Guid.NewGuid():N}";

		// Processor A claims with a SHORT lease, then "crashes" (never marks sent).
		var message = NewMessage();
		await using (var processorA = CreateStore(prefix, processorId: "proc-a", leaseTimeoutSeconds: 1))
		{
			await processorA.StageMessageAsync(message, TestCancellationToken);

			var firstClaim = (await processorA.GetUnsentMessagesAsync(10, TestCancellationToken)).Select(m => m.Id).ToList();
			firstClaim.ShouldContain(message.Id, "processor A must claim the staged message on the first poll");

			// While the lease is still held (unexpired), a different processor must NOT re-claim it.
			await using var processorBEager = CreateStore(prefix, processorId: "proc-b", leaseTimeoutSeconds: 1);
			var eager = (await processorBEager.GetUnsentMessagesAsync(10, TestCancellationToken)).Select(m => m.Id).ToList();
			eager.ShouldNotContain(message.Id, "an unexpired lease must not be re-claimed by another processor (no double-delivery)");
		}

		// Let the lease expire (1s lease + generous buffer for real-infra timing).
		await Task.Delay(TimeSpan.FromSeconds(2.5), TestCancellationToken);

		// A fresh processor reclaims the now-expired lease — the in-flight message is recovered, never lost.
		//
		// Asked repeatedly rather than sampled once. The lease is stamped and judged on the REDIS SERVER's
		// clock (redis.call('TIME')); the wait above is measured on the TEST HOST's. Those are two different
		// clocks, and on a loaded machine a container's clock does not advance in step with the host's — it
		// stalls and then catches up. Measured on the machine this suite runs on, sampling a container's clock
		// across fifty host-side 2.5 second waits: six of the fifty advanced the container clock by only 345
		// to 541 ms — far short of the one second this lease needs, and well inside the 1.5 seconds of slack
		// a 2.5 second wait appears to leave. A failing run of this very test was caught in the act: the
		// reclaim's own ZRANGEBYSCORE ran at a server instant EARLIER than the lease it was meant to expire,
		// so on Redis's clock the lease was still live.
		//
		// A single sample therefore asserts that the SERVER has seen the lease expire when only the HOST has,
		// and reports a store that is behaving correctly — refusing to reclaim a lease its own clock still
		// considers live — as a lost message. Refusing is the safe direction, so the store is right and the
		// sample is wrong. Polling asserts the same property the single sample was written to assert, without
		// assuming the two clocks agree: a no-lease/no-reclaim implementation still fails here, because it
		// never returns the message no matter how long it is asked.
		await using var processorB = CreateStore(prefix, processorId: "proc-b", leaseTimeoutSeconds: 1);
		var reclaimed = await WaitHelpers.WaitUntilAsync(
			async () => (await processorB.GetUnsentMessagesAsync(10, TestCancellationToken))
				.Any(m => string.Equals(m.Id, message.Id, StringComparison.Ordinal)),
			ReclaimWindow,
			ReclaimPollInterval,
			TestCancellationToken);

		reclaimed.ShouldBeTrue(
			"FR-J1.2/J1.5: an expired lease must be reclaimed so a claimed-but-unsent message is never lost "
			+ "(a no-lease/no-reclaim impl would never return it again). This arm kept asking for "
			+ $"{ReclaimWindow.TotalSeconds:0} seconds, which is far longer than any clock stall observed on "
			+ "this host, so the message was never handed back.");
	}

	[Fact]
	public async Task ClearLease_AndStopReclaim_WhenMarkedSent()
	{
		_redisFixture.DockerAvailable.ShouldBeTrue(
			"the real-Redis mark-sent lock must never be skipped (verify-against-real-infra-not-mock)");

		var prefix = $"outbox-5gtfje-marksent-{Guid.NewGuid():N}";
		await using var store = CreateStore(prefix, processorId: "proc-a", leaseTimeoutSeconds: 1);

		var message = NewMessage();
		await store.StageMessageAsync(message, TestCancellationToken);

		var claim = (await store.GetUnsentMessagesAsync(10, TestCancellationToken)).Select(m => m.Id).ToList();
		claim.ShouldContain(message.Id);

		// Terminal transition clears the lease (ZREM the leased index).
		await store.MarkSentAsync(message.Id, TestCancellationToken);

		var stats = await store.GetAllTenantsStatisticsAsync(TestCancellationToken);
		stats.SentMessageCount.ShouldBe(1, "the message must be counted sent");
		stats.SendingMessageCount.ShouldBe(0, "FR-J1.3: a sent message must leave the leased (in-flight) index");

		// Even after the lease window passes, a sent message is NOT re-claimable.
		await Task.Delay(TimeSpan.FromSeconds(2.5), TestCancellationToken);
		var afterExpiry = (await store.GetUnsentMessagesAsync(10, TestCancellationToken)).Select(m => m.Id).ToList();
		afterExpiry.ShouldNotContain(message.Id, "a sent message must never be reclaimed after its lease window");
	}

	// ── Helpers ──────────────────────────────────────────────────────────────

	private RedisOutboxStore CreateStore(string keyPrefix, string processorId, int leaseTimeoutSeconds = 300)
	{
		var options = MsOptions.Create(new RedisOutboxOptions
		{
			ConnectionString = _redisFixture.ConnectionString,
			KeyPrefix = keyPrefix,
			DatabaseId = 0,
			ProcessorId = processorId,
			LeaseTimeoutSeconds = leaseTimeoutSeconds,
			SentMessageTtlSeconds = 600,
			ConnectTimeoutMs = 5000,
			SyncTimeoutMs = 5000,
			AbortOnConnectFail = false,
		});

		var connection = ConnectionMultiplexer.Connect(_redisFixture.ConnectionString);
		return new RedisOutboxStore(connection, options, NullLogger<RedisOutboxStore>.Instance);
	}

	private static OutboundMessage NewMessage() =>
		new("TestMessage", System.Text.Encoding.UTF8.GetBytes("{\"data\":\"5gtfje\"}"), "test-destination");
}
