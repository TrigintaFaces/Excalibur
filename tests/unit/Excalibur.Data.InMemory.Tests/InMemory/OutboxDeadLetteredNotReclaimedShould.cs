// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Outbox.InMemory;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.InMemory.Tests.InMemory;

// bd-stlcgg (S841, ADR-336): the outbox had no terminal status — a retry-exhausted/DLQ'd message stayed Failed
// and was re-claimed + re-dead-lettered forever (duplicate delivery + unbounded DLQ growth). The fix adds
// OutboxStatus.DeadLettered (terminal) + IDeadLetterableOutboxStore.MarkDeadLetteredAsync, and every store's
// claim predicate is an explicit allow-list that structurally excludes the terminal status. Independent engage-
// test (author≠impl): a DeadLettered message is NEVER returned by either claim path (GetUnsentMessagesAsync /
// GetFailedMessagesAsync) — no re-deliver, no re-dead-letter. RED if the terminal transition or the claim
// allow-list regressed (e.g. MarkDeadLettered left it claimable).
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class OutboxDeadLetteredNotReclaimedShould
{
	private static InMemoryOutboxStore CreateStore() =>
		new(Options.Create(new InMemoryOutboxOptions()), NullLogger<InMemoryOutboxStore>.Instance);

	private static OutboundMessage NewMessage() =>
		new("TestMessageType", new byte[] { 1, 2, 3 }, "test-destination");

	// AC-4 — the delivery poller must NOT re-claim a dead-lettered message.
	[Fact]
	public async Task NotReturnDeadLetteredMessageFromGetUnsentMessages()
	{
		using var store = CreateStore();
		var msg = NewMessage();
		await store.StageMessageAsync(msg, CancellationToken.None);

		// Sanity: a freshly-staged message IS claimable (otherwise the lock would be vacuous).
		(await store.GetUnsentMessagesAsync(10, CancellationToken.None))
			.ShouldContain(m => m.Id == msg.Id);

		// Retries exhausted → terminal DeadLettered.
		await store.MarkDeadLetteredAsync(msg.Id, "retries exhausted", CancellationToken.None);

		// AC-4: never re-claimed (no re-delivery, no re-dead-letter).
		(await store.GetUnsentMessagesAsync(10, CancellationToken.None))
			.ShouldNotContain(m => m.Id == msg.Id);
	}

	// AC-4 — the failed/DLQ-retrieval path must also exclude the terminal status (not surfaced as "failed").
	[Fact]
	public async Task NotReturnDeadLetteredMessageFromGetFailedMessages()
	{
		using var store = CreateStore();
		var msg = NewMessage();
		await store.StageMessageAsync(msg, CancellationToken.None);
		await store.MarkDeadLetteredAsync(msg.Id, "retries exhausted", CancellationToken.None);

		var failed = await store.GetFailedMessagesAsync(
			maxRetries: 100, olderThan: null, batchSize: 10, CancellationToken.None);

		failed.ShouldNotContain(m => m.Id == msg.Id);
	}

	// The transition actually reaches the terminal status (proves the setter, so the exclusion above is non-vacuous).
	[Fact]
	public async Task TransitionMessageToDeadLetteredStatus()
	{
		using var store = CreateStore();
		var msg = NewMessage();
		await store.StageMessageAsync(msg, CancellationToken.None);

		await store.MarkDeadLetteredAsync(msg.Id, "retries exhausted", CancellationToken.None);

		msg.Status.ShouldBe(OutboxStatus.DeadLettered);
	}
}
