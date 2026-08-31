// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Inbox.InMemory;
using Excalibur.Dispatch;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.InMemory;

// 0yy2sp + 5uajzo (Dijkstra D5) — capacity eviction MUST respect the idempotency contract and FAIL CLOSED.
// A Processed entry is a deduplication marker and a Processing entry is an in-flight claim; evicting either
// while it is still live (within the retention window) would let a redelivery of that message be re-admitted
// and re-processed (a duplicate side-effect). Eviction drops the oldest NON-live entry (Received/Failed)
// first, then the oldest entry PAST the retention window; when EVERY entry is a live in-window dedup marker /
// in-flight claim it must THROW rather than evict one — dedup correctness outranks bounded memory (5uajzo
// reverses 0yy2sp's earlier "bounded memory wins" fallback).
//
// These are author!=impl regression locks. Each pairs a SAFETY arm (a live in-window marker is NOT evicted)
// with a LIVENESS arm (eviction/reclamation STILL happens for eligible victims — the store is not inert),
// per testing-patterns §3. The SAFETY arms go RED on the pre-fix blind-oldest / evict-a-live-marker eviction.
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryInboxStoreEvictionShould
{
	private const string Handler = "TestHandler";

	// A fixed, far-past base so ReceivedAt ordering is fully deterministic and independent of wall-clock.
	private static readonly DateTimeOffset Base = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

	private static InMemoryInboxStore NewStore(int maxEntries)
	{
		var options = Options.Create(new InMemoryInboxOptions
		{
			MaxEntries = maxEntries,
			EnableAutomaticCleanup = false
		});
		return new InMemoryInboxStore(options, NullLogger<InMemoryInboxStore>.Instance, UntenantedContext.Instance);
	}

	// CreateEntryAsync returns the live stored reference; mutate ReceivedAt on it to pin ordering.
	private static async Task<InboxEntry> AddWithReceivedAt(
		InMemoryInboxStore store, string messageId, DateTimeOffset receivedAt)
	{
		var entry = await store.CreateEntryAsync(
			messageId, Handler, "TestMessage", [1], new Dictionary<string, object>(), CancellationToken.None);
		entry.ReceivedAt = receivedAt;
		return entry;
	}

	// ARM 1 — SAFETY (headline): the OLDEST-by-ReceivedAt entry is a Processed dedup marker; the rest are
	// Received. Adding one more triggers eviction. The Processed marker MUST survive so a redelivery of that
	// message is still deduplicated. RED on the pre-fix blind-oldest eviction (drops the oldest = the marker).
	[Fact]
	public async Task NotEvictTheProcessedDedupMarkerWhenItIsTheOldestEntry()
	{
		await using var store = NewStore(maxEntries: 3);

		// Oldest-by-ReceivedAt is the Processed dedup marker.
		_ = await AddWithReceivedAt(store, "msg-processed", Base);
		await store.MarkProcessedAsync("msg-processed", Handler, CancellationToken.None);

		// Two newer Received entries fill the store to capacity (count == MaxEntries).
		_ = await AddWithReceivedAt(store, "msg-recv-1", Base.AddSeconds(1));
		_ = await AddWithReceivedAt(store, "msg-recv-2", Base.AddSeconds(2));

		// One more admission triggers EvictOldestEntry.
		_ = await store.CreateEntryAsync(
			"msg-new", Handler, "TestMessage", [1], new Dictionary<string, object>(), CancellationToken.None);

		// SAFETY: the Processed marker was NOT evicted — a redelivery is still deduplicated.
		(await store.IsProcessedAsync("msg-processed", Handler, CancellationToken.None))
			.ShouldBeTrue("the oldest entry is a Processed dedup marker and MUST NOT be evicted at capacity");
		(await store.GetEntryAsync("msg-processed", Handler, CancellationToken.None))
			.ShouldNotBeNull("the Processed dedup marker must remain present so redelivery re-processing is blocked");
	}

	// ARM 2 — LIVENESS pair for arm 1: eviction STILL happens. The store did not refuse to evict (is not
	// inert): a Received (non-live) victim — the oldest Received — WAS dropped, the count stays bounded at
	// MaxEntries, and the newly admitted entry is present.
	[Fact]
	public async Task StillEvictTheOldestReceivedEntryWhenProtectingTheProcessedMarker()
	{
		await using var store = NewStore(maxEntries: 3);

		_ = await AddWithReceivedAt(store, "msg-processed", Base);
		await store.MarkProcessedAsync("msg-processed", Handler, CancellationToken.None);

		_ = await AddWithReceivedAt(store, "msg-recv-1", Base.AddSeconds(1)); // oldest Received -> the victim
		_ = await AddWithReceivedAt(store, "msg-recv-2", Base.AddSeconds(2));

		_ = await store.CreateEntryAsync(
			"msg-new", Handler, "TestMessage", [1], new Dictionary<string, object>(), CancellationToken.None);

		// LIVENESS: bounded memory is still enforced.
		var stats = await store.GetAllTenantsStatisticsAsync(CancellationToken.None);
		stats.TotalEntries.ShouldBe(3, "capacity must remain bounded at MaxEntries — the store still evicts");

		// The evicted victim is the oldest NON-live entry, not the protected marker.
		(await store.GetEntryAsync("msg-recv-1", Handler, CancellationToken.None))
			.ShouldBeNull("the oldest Received (non-live) entry is the correct eviction victim");
		(await store.GetEntryAsync("msg-recv-2", Handler, CancellationToken.None))
			.ShouldNotBeNull("the newer Received entry must remain");
		(await store.GetEntryAsync("msg-new", Handler, CancellationToken.None))
			.ShouldNotBeNull("the newly admitted entry must be present");
	}

	// ARM 3a — SAFETY: an in-flight Processing claim is also protected. The oldest-by-ReceivedAt entry is a
	// Processing claim; a newer Received entry is the eligible victim. Adding one more must evict the Received
	// entry and preserve the Processing claim (so the in-flight message is not re-admitted). RED on blind-oldest.
	[Fact]
	public async Task NotEvictAnInFlightProcessingClaimWhenItIsTheOldestEntry()
	{
		await using var store = NewStore(maxEntries: 2);

		_ = await AddWithReceivedAt(store, "msg-processing", Base);
		await store.MarkProcessingAsync("msg-processing", Handler, CancellationToken.None);

		_ = await AddWithReceivedAt(store, "msg-recv", Base.AddSeconds(1)); // the eligible victim

		_ = await store.CreateEntryAsync(
			"msg-new", Handler, "TestMessage", [1], new Dictionary<string, object>(), CancellationToken.None);

		// SAFETY: the in-flight claim survives.
		var claim = await store.GetEntryAsync("msg-processing", Handler, CancellationToken.None);
		claim.ShouldNotBeNull("an in-flight Processing claim (oldest) MUST NOT be evicted at capacity");
		claim.Status.ShouldBe(InboxStatus.Processing);

		// LIVENESS: eviction still happened — the Received entry was the victim, count stayed bounded.
		(await store.GetEntryAsync("msg-recv", Handler, CancellationToken.None))
			.ShouldBeNull("the Received (non-live) entry is the correct victim while the claim is protected");
		var stats = await store.GetAllTenantsStatisticsAsync(CancellationToken.None);
		stats.TotalEntries.ShouldBe(2, "capacity must remain bounded at MaxEntries");
	}

	// ARM 3b — SAFETY (headline D5): when EVERY existing entry is a LIVE in-window dedup marker / in-flight
	// claim and the store is at capacity, eviction must FAIL CLOSED — throw rather than silently drop a live
	// marker (which would let a duplicate slip through). RED on the pre-fix fallback, which evicted the oldest
	// live marker to keep memory bounded.
	[Fact]
	public async Task ThrowAtCapacity_WhenEveryEntryIsALiveInWindowDedupMarker()
	{
		await using var store = NewStore(maxEntries: 2);

		_ = await AddWithReceivedAt(store, "msg-processed-old", Base);
		await store.MarkProcessedAsync("msg-processed-old", Handler, CancellationToken.None);

		_ = await AddWithReceivedAt(store, "msg-processed-new", Base.AddSeconds(1));
		await store.MarkProcessedAsync("msg-processed-new", Handler, CancellationToken.None);

		// Both markers are live and within the retention window, and the store is full: admission fails closed.
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			_ = await store.CreateEntryAsync(
				"msg-new", Handler, "TestMessage", [1], new Dictionary<string, object>(), CancellationToken.None));

		// SAFETY: NO live dedup marker was evicted — both survive, so redelivery of either is still blocked.
		(await store.GetEntryAsync("msg-processed-old", Handler, CancellationToken.None))
			.ShouldNotBeNull("a live in-window dedup marker must never be silently evicted — the admission fails closed instead");
		(await store.GetEntryAsync("msg-processed-new", Handler, CancellationToken.None))
			.ShouldNotBeNull("both live dedup markers survive; neither is dropped to admit the new entry");
	}

	// ARM 3c — LIVENESS pair for D5: fail-closed is NOT a blanket refusal. A Processed marker PAST the
	// retention window no longer protects against a duplicate, so it IS reclaimed to admit a new entry — the
	// store is not inert. Proves the throw fires only for the genuinely all-live-in-window case.
	[Fact]
	public async Task StillReclaimAProcessedMarker_PastTheRetentionWindow()
	{
		await using var store = NewStore(maxEntries: 2);

		// An old Processed marker whose ProcessedAt is far past the (default 7-day) retention window.
		_ = await AddWithReceivedAt(store, "msg-expired", Base);
		await store.MarkProcessedAsync("msg-expired", Handler, CancellationToken.None);
		var expired = await store.GetEntryAsync("msg-expired", Handler, CancellationToken.None);
		expired.ShouldNotBeNull();
		expired.ProcessedAt = Base; // far past the retention window relative to now

		// A newer, still-live in-window Processed marker.
		_ = await AddWithReceivedAt(store, "msg-live", Base.AddSeconds(1));
		await store.MarkProcessedAsync("msg-live", Handler, CancellationToken.None);

		// At capacity: the expired marker is reclaimable, so admission SUCCEEDS (no throw).
		_ = await store.CreateEntryAsync(
			"msg-new", Handler, "TestMessage", [1], new Dictionary<string, object>(), CancellationToken.None);

		var stats = await store.GetAllTenantsStatisticsAsync(CancellationToken.None);
		stats.TotalEntries.ShouldBe(2, "capacity stays bounded — the expired marker was reclaimed");

		// LIVENESS: the expired marker was the victim; the live marker and the admission survive.
		(await store.GetEntryAsync("msg-expired", Handler, CancellationToken.None))
			.ShouldBeNull("a Processed marker past the retention window is reclaimable and is the correct victim");
		(await store.GetEntryAsync("msg-live", Handler, CancellationToken.None))
			.ShouldNotBeNull("the still-live in-window marker must remain");
		(await store.GetEntryAsync("msg-new", Handler, CancellationToken.None))
			.ShouldNotBeNull("the newly admitted entry must be present");
	}
}
