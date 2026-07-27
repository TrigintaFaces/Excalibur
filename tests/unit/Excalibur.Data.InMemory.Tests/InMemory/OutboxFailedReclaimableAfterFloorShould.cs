// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Outbox.InMemory;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.InMemory.Tests.InMemory;

/// <summary>
/// Canonical <c>MarkFailedAsync</c> re-claimability contract for the outbox family, verified on the
/// InMemory store. A failed (sub-retry-ceiling) message is:
/// <list type="bullet">
/// <item><description><b>Safety (R1)</b> — NOT re-claimable within the failure-anchored visibility floor
/// (no zero-backoff hot-loop), on BOTH the reserved and the unreserved-input path.</description></item>
/// <item><description><b>Liveness (R1 / at-least-once)</b> — eventually re-claimable once the floor
/// elapses; never terminally dropped (the direct contrast with <c>OutboxDeadLetteredNotReclaimedShould</c>,
/// where a terminal status is never re-claimed).</description></item>
/// <item><description><b>R3</b> — attempts are non-decreasing across re-claims (a stale late writer cannot
/// move the count down and weaken the DLQ-ceiling termination guarantee).</description></item>
/// </list>
/// Each safety arm is non-vacuous against an immediate-reclaim (no-floor) store; the liveness arm is
/// non-vacuous against a terminal (silent-drop) store — the two broken behaviors this contract forbids.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class OutboxFailedReclaimableAfterFloorShould
{
	private static InMemoryOutboxStore CreateStore(int floorSeconds) =>
		new(
			Options.Create(new InMemoryOutboxOptions { FailureBackoffFloorSeconds = floorSeconds }),
			NullLogger<InMemoryOutboxStore>.Instance);

	private static OutboundMessage NewMessage() =>
		new("TestMessageType", new byte[] { 1, 2, 3 }, "test-destination");

	// SAFETY (R1), reserved path: claim then fail — the message is NOT re-claimable in the same cycle.
	[Fact]
	public async Task NotReclaimFailedMessageWithinTheFloor_ReservedPath()
	{
		// Large floor so it is robustly un-elapsed at the immediate re-claim check below.
		using var store = CreateStore(floorSeconds: 60);
		var msg = NewMessage();
		await store.StageMessageAsync(msg, CancellationToken.None);

		// Non-vacuity: a freshly-staged message IS claimable.
		(await store.GetUnsentMessagesAsync(10, CancellationToken.None))
			.ShouldContain(m => m.Id == msg.Id);

		await store.MarkFailedAsync(msg.Id, "boom", 1, CancellationToken.None);

		// Floor not elapsed → not re-claimed (no zero-backoff hot-loop).
		(await store.GetUnsentMessagesAsync(10, CancellationToken.None))
			.ShouldNotContain(m => m.Id == msg.Id);
	}

	// SAFETY (R1), unreserved-input path (Lamport's hole): stage then fail WITHOUT ever claiming — still floored.
	[Fact]
	public async Task NotReclaimFailedMessageWithinTheFloor_UnreservedInputPath()
	{
		using var store = CreateStore(floorSeconds: 60);
		var msg = NewMessage();
		await store.StageMessageAsync(msg, CancellationToken.None);

		// No claim — fail an unreserved message directly.
		await store.MarkFailedAsync(msg.Id, "boom", 1, CancellationToken.None);

		(await store.GetUnsentMessagesAsync(10, CancellationToken.None))
			.ShouldNotContain(m => m.Id == msg.Id);
	}

	// LIVENESS (R1 / at-least-once): after the floor elapses, the failed message IS re-claimed (never dropped).
	[Fact]
	public async Task EventuallyReclaimFailedMessageAfterTheFloorElapses()
	{
		// Short floor so the bounded poll below observes re-claim quickly.
		using var store = CreateStore(floorSeconds: 1);
		var msg = NewMessage();
		await store.StageMessageAsync(msg, CancellationToken.None);
		await store.MarkFailedAsync(msg.Id, "boom", 1, CancellationToken.None);

		// Poll for the condition with a bounded timeout (no fixed sleep-then-assert): the floor elapses
		// via real time and the message must re-enter the claimable set.
		var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
		var reclaimed = false;
		while (DateTimeOffset.UtcNow < deadline)
		{
			var batch = await store.GetUnsentMessagesAsync(10, CancellationToken.None);
			if (batch.Any(m => m.Id == msg.Id))
			{
				reclaimed = true;
				break;
			}

			await Task.Delay(50, CancellationToken.None);
		}

		reclaimed.ShouldBeTrue(
			"a failed message must remain eventually re-claimable (at-least-once) — never silently dropped");
	}

	// LIVENESS (R1), authored independently of the implementer.
	//
	// The existing liveness arm asserts the message eventually re-enters the claimable set. That is true of
	// the correct store AND of a store with no floor at all, which returns it immediately — so on its own it
	// does not bind the floor. It is the safety arms that forbid immediate re-claim, in separate tests.
	//
	// This arm binds the conjunction in ONE observation: the SAME message is refused while the floor is
	// un-elapsed and served after it elapses. It fails against both behaviours the contract forbids —
	// a silent-drop store never satisfies the second half, an immediate-reclaim store never satisfies the
	// first — which neither existing arm does alone.
	[Fact]
	public async Task ServeAFailedMessageOnlyAfterTheFloorElapses_NotBefore()
	{
		const int FloorSeconds = 2;
		using var store = CreateStore(floorSeconds: FloorSeconds);
		var msg = NewMessage();
		await store.StageMessageAsync(msg, CancellationToken.None);

		// Non-vacuity: the message is claimable BEFORE it is failed, so a later absence means the floor
		// acted rather than the message never having been servable.
		(await store.GetUnsentMessagesAsync(10, CancellationToken.None))
			.ShouldContain(m => m.Id == msg.Id);

		await store.MarkFailedAsync(msg.Id, "boom", 1, CancellationToken.None);

		// FIRST HALF — refused while the floor is un-elapsed. An immediate-reclaim store fails here.
		(await store.GetUnsentMessagesAsync(10, CancellationToken.None))
			.ShouldNotContain(
				m => m.Id == msg.Id,
				"a failed message must not be served again while its backoff floor is un-elapsed");

		// SECOND HALF — served once it elapses. A silent-drop store fails here. Polled against a bounded
		// deadline rather than slept, so the arm does not depend on scheduler timing.
		var deadline = DateTimeOffset.UtcNow.AddSeconds(FloorSeconds + 15);
		var served = false;
		while (DateTimeOffset.UtcNow < deadline)
		{
			var batch = await store.GetUnsentMessagesAsync(10, CancellationToken.None);
			if (batch.Any(m => m.Id == msg.Id))
			{
				served = true;
				break;
			}

			await Task.Delay(50, CancellationToken.None);
		}

		served.ShouldBeTrue(
			"a failed message must become claimable again once its floor elapses — the floor is a delay, "
			+ "never a terminal drop");
	}

	// R3: attempts are non-decreasing — a stale late failure report with a lower count must not lower it.
	[Fact]
	public async Task NotDecreaseRetryCountOnAStaleLateFailureReport()
	{
		using var store = CreateStore(floorSeconds: 60);
		var msg = NewMessage();
		await store.StageMessageAsync(msg, CancellationToken.None);

		await store.MarkFailedAsync(msg.Id, "attempt-3", 3, CancellationToken.None);
		await store.MarkFailedAsync(msg.Id, "stale-1", 1, CancellationToken.None);

		msg.RetryCount.ShouldBe(3);
	}

	// Observability: a failed (retryable) message is surfaced via GetFailedMessages (Status == Failed).
	[Fact]
	public async Task SurfaceFailedMessageViaGetFailedMessages()
	{
		using var store = CreateStore(floorSeconds: 60);
		var msg = NewMessage();
		await store.StageMessageAsync(msg, CancellationToken.None);
		await store.MarkFailedAsync(msg.Id, "boom", 2, CancellationToken.None);

		(await store.GetFailedMessagesAsync(maxRetries: 100, olderThan: null, batchSize: 10, CancellationToken.None))
			.ShouldContain(m => m.Id == msg.Id);
	}
}
