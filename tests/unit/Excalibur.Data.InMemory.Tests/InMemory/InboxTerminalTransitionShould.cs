// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Inbox.InMemory;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.InMemory;

// Processed is ABSORBING: no operation returns a key from Processed before its retention deadline.
//
// The sequence that breaks it needs no concurrency, only a handler that outruns its own lease -- which the
// middleware permits, because it passes ProcessingTimeout as the lease and never bounds the handler by it:
//
//   A claims under a lease T; A's handler runs longer than T
//   B reclaims the expired lease and runs the handler
//   A finishes  -> MarkProcessed marks B's entry Processed
//   B finishes  -> MarkProcessed throws "already processed"
//   B's catch   -> MarkFailed ... and a terminal entry becomes Failed again
//
// Failed is re-admittable, so the next redelivery runs the handler a THIRD time, and IsProcessedAsync then
// answers false about a message that was processed twice. The guard refuses the transition instead.
//
// Every arm is sequential and driven by a controlled clock: the lapse is a clock move, not a sleep, so
// nothing depends on scheduling. SAFETY arms go RED on the pre-guard implementation. LIVENESS arms fail a
// store that refuses everything -- which would otherwise satisfy every safety arm by doing nothing.
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InboxTerminalTransitionShould
{
	private const string Handler = "TestHandler";
	private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
	private static readonly TimeSpan Lease = TimeSpan.FromMinutes(1);

	private sealed class TestClock(DateTimeOffset now) : TimeProvider
	{
		public DateTimeOffset Now { get; set; } = now;

		public override DateTimeOffset GetUtcNow() => Now;
	}

	private static InMemoryInboxStore NewStore(TimeProvider clock) =>
		new(
			Options.Create(new InMemoryInboxOptions { EnableAutomaticCleanup = false }),
			NullLogger<InMemoryInboxStore>.Instance,
			UntenantedContext.Instance,
			clock);

	// Drives the two-caller lapse to the point where the late caller's finalize has already thrown, leaving
	// the entry terminal and the late caller about to run its own error handling.
	private static async Task<(InMemoryInboxStore Store, TestClock Clock, string MessageId)> LapsedReclaimAsync()
	{
		var clock = new TestClock(T0);
		var store = NewStore(clock);
		var messageId = $"msg-{Guid.NewGuid():N}";
		var ct = CancellationToken.None;

		// A claims under a lease.
		(await store.TryAcquireLeaseAsync(messageId, Handler, Lease, ct)).ShouldNotBeNull();

		// A's handler outruns it.
		clock.Now = T0.Add(Lease).AddSeconds(1);

		// B reclaims the expired lease and becomes the sole processor.
		(await store.TryAcquireLeaseAsync(messageId, Handler, Lease, ct)).ShouldNotBeNull(
			"an expired lease must be reclaimable, or the message would be stuck forever");

		// A finishes first and finalizes what is now B's entry.
		await store.MarkProcessedAsync(messageId, Handler, ct);
		(await store.IsProcessedAsync(messageId, Handler, ct)).ShouldBeTrue();

		// B finishes and is told it lost.
		_ = await Should.ThrowAsync<InvalidOperationException>(
			async () => await store.MarkProcessedAsync(messageId, Handler, ct));

		return (store, clock, messageId);
	}

	// SAFETY (headline) -- the exact sequence above: B's error handling must not resurrect the entry.
	[Fact]
	public async Task KeepAnEntryProcessedWhenTheLateCallerReportsItsOwnFailure()
	{
		var (store, _, messageId) = await LapsedReclaimAsync();
		await using (store)
		{
			var ct = CancellationToken.None;

			await store.MarkFailedAsync(messageId, Handler, "handler threw after losing its lease", ct);

			(await store.IsProcessedAsync(messageId, Handler, ct)).ShouldBeTrue(
				"the message really was processed; demoting the entry would re-admit it on the next delivery");
			var entry = await store.GetEntryAsync(messageId, Handler, ct);
			_ = entry.ShouldNotBeNull();
			entry.Status.ShouldBe(InboxStatus.Processed);
		}
	}

	// SAFETY -- the retry-count overload takes a separate path and must refuse identically.
	[Fact]
	public async Task KeepAnEntryProcessedWhenTheLateCallerReportsAFailureWithAnExplicitRetryCount()
	{
		var (store, _, messageId) = await LapsedReclaimAsync();
		await using (store)
		{
			var ct = CancellationToken.None;

			await store.MarkFailedAsync(messageId, Handler, "transient", retryCount: 7, ct);

			(await store.IsProcessedAsync(messageId, Handler, ct)).ShouldBeTrue();
			var entry = await store.GetEntryAsync(messageId, Handler, ct);
			_ = entry.ShouldNotBeNull();
			entry.Status.ShouldBe(InboxStatus.Processed);
			entry.RetryCount.ShouldNotBe(7, "a refused transition must not write its retry count either");
		}
	}

	// SAFETY -- releasing a FINALIZED entry must not delete the dedup marker.
	//
	// Scope, precisely: this covers a release arriving after the entry became Processed. It does NOT cover a
	// lapsed holder releasing its successor's still-live Processing claim -- at that instant the entry is
	// legitimately Processing and no status predicate can tell the two callers apart. That needs the claim
	// term and is tracked separately. Do not read this arm as covering it.
	[Fact]
	public async Task KeepAProcessedEntryWhenALateCallerReleasesIt()
	{
		var (store, _, messageId) = await LapsedReclaimAsync();
		await using (store)
		{
			var ct = CancellationToken.None;

			await store.ReleaseAsync(messageId, Handler, ct);

			(await store.IsProcessedAsync(messageId, Handler, ct)).ShouldBeTrue(
				"deleting the marker would erase the record of a message that really was processed");
		}
	}

	// SAFETY -- MarkProcessing is a write too, and it un-terminalises just as effectively.
	[Fact]
	public async Task KeepAnEntryProcessedWhenALateCallerMarksItProcessingAgain()
	{
		var (store, _, messageId) = await LapsedReclaimAsync();
		await using (store)
		{
			var ct = CancellationToken.None;

			await store.MarkProcessingAsync(messageId, Handler, ct);

			var entry = await store.GetEntryAsync(messageId, Handler, ct);
			_ = entry.ShouldNotBeNull();
			entry.Status.ShouldBe(InboxStatus.Processed);
		}
	}

	// SAFETY -- a Processed entry must not be re-claimable, which is what makes the demotion damaging.
	[Fact]
	public async Task RefuseToReclaimAProcessedEntry()
	{
		var (store, clock, messageId) = await LapsedReclaimAsync();
		await using (store)
		{
			clock.Now = clock.Now.AddHours(1);

			(await store.TryAcquireLeaseAsync(messageId, Handler, Lease, CancellationToken.None)).ShouldBeNull(
				"Processed is terminal for claiming; a redelivery must not run the handler again");
		}
	}

	// LIVENESS -- a store that refuses every write passes every arm above. These fail it.
	[Fact]
	public async Task StillRecordFailureOnAnEntryThatIsNotTerminal()
	{
		var clock = new TestClock(T0);
		await using var store = NewStore(clock);
		var messageId = $"msg-{Guid.NewGuid():N}";
		var ct = CancellationToken.None;

		(await store.TryAcquireLeaseAsync(messageId, Handler, Lease, ct)).ShouldNotBeNull();
		await store.MarkFailedAsync(messageId, Handler, "boom", ct);

		var entry = await store.GetEntryAsync(messageId, Handler, ct);
		_ = entry.ShouldNotBeNull();
		entry.Status.ShouldBe(InboxStatus.Failed);
		entry.LastError.ShouldBe("boom");
		(await store.IsProcessedAsync(messageId, Handler, ct)).ShouldBeFalse();
	}

	// LIVENESS -- release still removes a live, non-terminal claim, so a failed message is re-admittable.
	[Fact]
	public async Task StillReleaseAClaimThatIsNotTerminal()
	{
		var clock = new TestClock(T0);
		await using var store = NewStore(clock);
		var messageId = $"msg-{Guid.NewGuid():N}";
		var ct = CancellationToken.None;

		(await store.TryAcquireLeaseAsync(messageId, Handler, Lease, ct)).ShouldNotBeNull();
		await store.ReleaseAsync(messageId, Handler, ct);

		(await store.GetEntryAsync(messageId, Handler, ct)).ShouldBeNull();
		(await store.TryAcquireLeaseAsync(messageId, Handler, Lease, ct)).ShouldNotBeNull(
			"a released claim must be re-admittable, or a failed message would be stuck");
	}

	// The entry type carries the same rule, so every in-memory caller inherits it from one place.
	[Fact]
	public void RefuseEveryTransitionOutOfProcessedOnTheEntryItself()
	{
		var entry = new InboxEntry("m", Handler, "T", [1]);
		entry.MarkProcessed();
		var processedAt = entry.ProcessedAt;

		entry.MarkFailed("boom");
		entry.Status.ShouldBe(InboxStatus.Processed);
		entry.LastError.ShouldBeNull();
		entry.RetryCount.ShouldBe(0);

		entry.MarkProcessing();
		entry.Status.ShouldBe(InboxStatus.Processed);

		entry.MarkProcessed();
		entry.ProcessedAt.ShouldBe(processedAt, "re-finalizing must not restamp when the message was handled");
	}

	// LIVENESS for the entry type -- the transitions still work before it is terminal.
	[Fact]
	public void StillApplyTransitionsBeforeTheEntryIsTerminal()
	{
		var entry = new InboxEntry("m", Handler, "T", [1]);

		entry.MarkProcessing();
		entry.Status.ShouldBe(InboxStatus.Processing);

		entry.MarkFailed("boom");
		entry.Status.ShouldBe(InboxStatus.Failed);
		entry.LastError.ShouldBe("boom");
		entry.RetryCount.ShouldBe(1);

		entry.MarkProcessed();
		entry.Status.ShouldBe(InboxStatus.Processed);
		entry.LastError.ShouldBeNull();
	}
}
