// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Outbox.InMemory;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.InMemory.Tests.InMemory;

// Regression lock for capacity eviction (bd-ddgikw).
//
// SAFETY PROPERTY: a message that has been accepted by the store and is still owed delivery is never
// removed by capacity eviction. INVARIANT over every reachable state: every message removed by eviction has
// a TERMINAL status — Sent (delivered) or DeadLettered (terminally abandoned at the retry ceiling). Staged,
// Sending, Failed and PartiallyFailed are all still owed an at-least-once delivery.
//
// THE STATE THAT VIOLATED IT was reachable single-threadedly, no interleaving required: the eviction
// candidate scan fell back to "oldest message overall" whenever no Sent message existed, so a store holding
// only unsent messages evicted one of them on the next stage. It is worse than it sounds, because the store
// is at its most crowded precisely when the drain is behind — so the fallback fired exactly when every
// message present was still awaiting delivery, and it also tore down the lease of a message a claimer was
// at that moment holding. Eviction removes the message and its lease together, so the claimer's subsequent
// MarkSent/MarkFailed reports against a message that no longer exists.
//
// These arms are deterministic by construction: no threads, no wall clock, no ordering dependency. Capacity
// is reached by staging a fixed number of messages, and both terminal states are reached by an explicit call.
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class OutboxCapacityEvictionShould
{
	private const int Capacity = 2;

	private static InMemoryOutboxStore CreateStore() =>
		new(Options.Create(new InMemoryOutboxOptions { MaxMessages = Capacity }), NullLogger<InMemoryOutboxStore>.Instance);

	private static OutboundMessage NewMessage() =>
		new("TestMessageType", new byte[] { 1, 2, 3 }, "test-destination");

	private static async Task<OutboundMessage> StageAsync(InMemoryOutboxStore store)
	{
		var message = NewMessage();
		await store.StageMessageAsync(message, CancellationToken.None);
		return message;
	}

	[Fact]
	public async Task RefuseToStage_RatherThanDiscardAnUnsentLeasedMessage()
	{
		using var store = CreateStore();

		var first = await StageAsync(store);
		var second = await StageAsync(store);

		// Both are now leased to a claimer: a dispatcher holds them and is mid-delivery.
		var claimed = (await store.GetUnsentMessagesAsync(Capacity, CancellationToken.None)).ToList();
		claimed.ShouldContain(m => m.Id == first.Id);
		claimed.ShouldContain(m => m.Id == second.Id);

		// The store is full of messages that have never been delivered. Accepting a third would mean
		// discarding one of them, so the stage is REFUSED and the caller — whose own transaction has not
		// committed yet — learns the message was not accepted.
		var overflow = NewMessage();
		var refusal = await Should.ThrowAsync<InvalidOperationException>(
			async () => await store.StageMessageAsync(overflow, CancellationToken.None));

		refusal.Message.ShouldContain(
			"at capacity",
			customMessage: "the refusal must say why staging failed and what to change, or an operator sees only a bare throw");

		var statistics = await store.GetAllTenantsStatisticsAsync(CancellationToken.None);
		statistics.StagedMessageCount.ShouldBe(
			Capacity,
			"neither leased, undelivered message may be discarded to make room — an evicted staged message is "
			+ "delivered zero times, which breaks at-least-once at its floor rather than at its duplicate ceiling");
	}

	[Fact]
	public async Task EvictTheDeliveredMessage_WhenOneIsAvailable()
	{
		using var store = CreateStore();

		var delivered = await StageAsync(store);
		var stillOwed = await StageAsync(store);

		await store.MarkSentAsync(delivered.Id, CancellationToken.None);

		// Capacity is reclaimed from the message whose delivery is over, not from the one still owed.
		var overflow = NewMessage();
		await store.StageMessageAsync(overflow, CancellationToken.None);

		var statistics = await store.GetAllTenantsStatisticsAsync(CancellationToken.None);
		statistics.SentMessageCount.ShouldBe(0, "the Sent message is the one reclaimed");
		statistics.StagedMessageCount.ShouldBe(Capacity, "the undelivered message and the new one both remain");

		var claimable = await store.GetUnsentMessagesAsync(Capacity, CancellationToken.None);
		claimable.Select(m => m.Id).ShouldContain(
			stillOwed.Id,
			customMessage: "the message that was still owed delivery survived the eviction and is still claimable");
	}

	[Fact]
	public async Task EvictTheDeadLetteredMessage_RatherThanRefusing()
	{
		using var store = CreateStore();

		var abandoned = await StageAsync(store);
		var stillOwed = await StageAsync(store);

		await store.MarkDeadLetteredAsync(abandoned.Id, "retries exhausted", CancellationToken.None);

		// DeadLettered is terminal: the message will never be delivered from this store, so reclaiming it
		// costs nothing. Refusing here instead would fail closed on a store that did have room.
		var overflow = NewMessage();
		await store.StageMessageAsync(overflow, CancellationToken.None);

		var statistics = await store.GetAllTenantsStatisticsAsync(CancellationToken.None);
		statistics.StagedMessageCount.ShouldBe(Capacity, "the undelivered message and the new one both remain");

		var claimable = await store.GetUnsentMessagesAsync(Capacity, CancellationToken.None);
		claimable.Select(m => m.Id).ShouldContain(stillOwed.Id);
	}
}
