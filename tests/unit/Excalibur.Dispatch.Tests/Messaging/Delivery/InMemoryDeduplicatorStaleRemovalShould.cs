// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Options.Delivery;

using Microsoft.Extensions.Logging.Abstractions;

using Tests.Shared.Infrastructure;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

/// <summary>
/// Binds the deduplicator's defining property against a <i>stale remover</i>: a caller that reads an
/// entry, judges it expired, and then removes it must remove <b>that</b> entry, never whatever occupies
/// the key at the moment of removal.
/// </summary>
/// <remarks>
/// <para>
/// The invariant: <b>at most one caller ever holds a claim on a given message id at a time.</b>
/// </para>
/// <para>
/// The interleaving that breaks it, with a removal that does not compare the value:
/// two callers A and B both read the same expired entry E; B removes E and installs its own live claim
/// L; A then removes <i>L</i> — the live claim it never read — and installs its own. A and B now both
/// believe they hold the id, which is the one outcome a deduplicator exists to prevent.
/// </para>
/// <para>
/// The window between the read and the removal is a couple of instructions and there is no interposition
/// point in the implementation, so these arms drive it by contention rather than by ordering: every
/// racer is steered onto the expired-entry branch by seeding an already-expired entry first, and two
/// threads are released onto the same id together by a barrier. See the file's liveness arms — a removal
/// predicate can be tightened until it never removes anything, and every safety arm here would still
/// pass.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class InMemoryDeduplicatorStaleRemovalShould : IDisposable
{
	private const int RaceIterations = 20_000;

	private static readonly TimeSpan AlreadyExpiring = TimeSpan.FromTicks(1);
	private static readonly TimeSpan LongEnoughToStayClaimed = TimeSpan.FromMinutes(5);

	private readonly InMemoryDeduplicator _deduplicator = new(
		Microsoft.Extensions.Options.Options.Create(new InMemoryDeduplicatorOptions
		{
			EnableAutomaticCleanup = false,
			MaxEntries = 0,
		}),
		NullLogger<InMemoryDeduplicator>.Instance);

	public void Dispose() => _deduplicator.Dispose();

	// ---------------------------------------------------------------------------------------------
	// Safety: a stale remover must not delete a live claim.
	// ---------------------------------------------------------------------------------------------

	[Fact]
	public async Task TryClaimAsync_GrantsAtMostOneClaimPerMessageId_WhenTwoCallersRaceOverAnExpiredEntry()
	{
		var ids = Enumerable.Range(0, RaceIterations).Select(i => $"race-{i}").ToArray();

		// Steer every racer onto the expired-entry branch: seed each id, then let the seeds lapse.
		// Without this, a racer that finds no entry never reaches the read-then-remove path at all.
		foreach (var id in ids)
		{
			_ = await _deduplicator.TryClaimAsync(id, AlreadyExpiring, CancellationToken.None);
		}

		await Task.Delay(50);

		var doubleGrants = new ConcurrentBag<string>();
		using var barrier = new Barrier(2);
		var winners = new int[2];

		async Task RaceAsync(int slot)
		{
			for (var i = 0; i < ids.Length; i++)
			{
				barrier.SignalAndWait();
				winners[slot] = await _deduplicator.TryClaimAsync(ids[i], LongEnoughToStayClaimed, CancellationToken.None)
					is not null
					? 1
					: 0;

				// Only one racer tallies, and only once both have written their result.
				barrier.SignalAndWait();
				if (slot == 0 && winners[0] + winners[1] > 1)
				{
					doubleGrants.Add(ids[i]);
				}
			}
		}

		await Task.WhenAll(
			Task.Run(() => RaceAsync(0)),
			Task.Run(() => RaceAsync(1)));

		doubleGrants.ShouldBeEmpty(
			$"a claim is exclusive: {doubleGrants.Count} message id(s) were granted to both racers, "
			+ "which means one racer's removal deleted the other's live claim");
	}

	[Fact]
	public async Task CleanupExpiredEntriesAsync_LeavesLiveClaimsIntact_WhenClaimsLandDuringTheSweep()
	{
		var ids = Enumerable.Range(0, RaceIterations).Select(i => $"sweep-{i}").ToArray();

		foreach (var id in ids)
		{
			_ = await _deduplicator.TryClaimAsync(id, AlreadyExpiring, CancellationToken.None);
		}

		await Task.Delay(50);

		using var sweeping = new CancellationTokenSource();
		var sweeper = Task.Run(async () =>
		{
			while (!sweeping.IsCancellationRequested)
			{
				_ = await _deduplicator.CleanupExpiredEntriesAsync(CancellationToken.None);
			}
		});

		// The sweep reads an expired entry in its scan pass and removes it in a later pass. A claim that
		// lands between those two passes is live by the time the removal runs, and must survive it.
		var held = new List<string>(ids.Length);
		foreach (var id in ids)
		{
			if (await _deduplicator.TryClaimAsync(id, LongEnoughToStayClaimed, CancellationToken.None) is not null)
			{
				held.Add(id);
			}
		}

		await sweeping.CancelAsync();
		await sweeper;

		// Everything in `held` is a live claim with minutes left to run. Nothing may re-grant any of them.
		var lostClaims = new List<string>();
		foreach (var id in held)
		{
			if (await _deduplicator.TryClaimAsync(id, LongEnoughToStayClaimed, CancellationToken.None) is not null)
			{
				lostClaims.Add(id);
			}
		}

		held.ShouldNotBeEmpty("every seeded id had lapsed, so the claims must have been granted");

		lostClaims.ShouldBeEmpty(
			$"expiry cleanup deleted {lostClaims.Count} claim(s) that were live by the time it removed them, "
			+ "re-admitting a message that was already claimed");
	}

	[Fact]
	public async Task IsDuplicateAsync_KeepsReportingADuplicate_WhenAnExpiredEntryIsRefreshedDuringTheCheck()
	{
		var ids = Enumerable.Range(0, RaceIterations).Select(i => $"check-{i}").ToArray();

		foreach (var id in ids)
		{
			await _deduplicator.MarkProcessedAsync(id, AlreadyExpiring, CancellationToken.None);
		}

		await Task.Delay(50);

		var forgotten = new ConcurrentBag<string>();
		using var barrier = new Barrier(2);

		async Task CheckerAsync()
		{
			foreach (var id in ids)
			{
				barrier.SignalAndWait();
				_ = await _deduplicator.IsDuplicateAsync(id, LongEnoughToStayClaimed, CancellationToken.None);
			}
		}

		async Task RecorderAsync()
		{
			foreach (var id in ids)
			{
				barrier.SignalAndWait();
				await _deduplicator.MarkProcessedAsync(id, LongEnoughToStayClaimed, CancellationToken.None);
			}
		}

		await Task.WhenAll(Task.Run(CheckerAsync), Task.Run(RecorderAsync));

		// Every id was freshly recorded with a long expiry. A checker that removed the *new* record
		// instead of the expired one it read has silently forgotten a processed message.
		foreach (var id in ids)
		{
			if (!await _deduplicator.IsDuplicateAsync(id, LongEnoughToStayClaimed, CancellationToken.None))
			{
				forgotten.Add(id);
			}
		}

		forgotten.ShouldBeEmpty(
			$"{forgotten.Count} freshly-recorded message(s) are no longer known: the expiry check removed a "
			+ "record it had not read, so a redelivery would be processed a second time");
	}

	// ---------------------------------------------------------------------------------------------
	// Liveness: the removal predicate must still remove what genuinely should be removed.
	// A predicate tightened until it never removes anything passes every arm above.
	// ---------------------------------------------------------------------------------------------

	[Fact]
	public async Task TryClaimAsync_ReclaimsTheMessageId_OnceTheExistingClaimHasExpired()
	{
		(await _deduplicator.TryClaimAsync("live-reclaim", AlreadyExpiring, CancellationToken.None)).ShouldNotBeNull();
		await Task.Delay(50);

		(await _deduplicator.TryClaimAsync("live-reclaim", LongEnoughToStayClaimed, CancellationToken.None))
			.ShouldNotBeNull("an expired claim must be removed and the id re-admitted, or expiry is wedged shut");

		(await _deduplicator.TryClaimAsync("live-reclaim", LongEnoughToStayClaimed, CancellationToken.None))
			.ShouldBeNull("the fresh claim is live and exclusive");
	}

	[Fact]
	public async Task CleanupExpiredEntriesAsync_StillRemovesExpiredEntries()
	{
		const int Expired = 500;
		for (var i = 0; i < Expired; i++)
		{
			_ = await _deduplicator.TryClaimAsync($"live-sweep-{i}", AlreadyExpiring, CancellationToken.None);
		}

		_ = await _deduplicator.TryClaimAsync("live-sweep-keep", LongEnoughToStayClaimed, CancellationToken.None);
		await Task.Delay(50);

		var removed = await _deduplicator.CleanupExpiredEntriesAsync(CancellationToken.None);

		removed.ShouldBe(Expired, "every expired entry must still be swept, or the sweep is wedged shut");
		_deduplicator.GetStatistics().TrackedMessageCount.ShouldBe(1, "the unexpired claim must survive the sweep");
	}

	[Fact]
	public async Task IsDuplicateAsync_StillEvictsAnExpiredRecord()
	{
		await _deduplicator.MarkProcessedAsync("live-evict", AlreadyExpiring, CancellationToken.None);
		await Task.Delay(50);

		(await _deduplicator.IsDuplicateAsync("live-evict", LongEnoughToStayClaimed, CancellationToken.None))
			.ShouldBeFalse("an expired record must not report a duplicate");

		_deduplicator.GetStatistics().TrackedMessageCount.ShouldBe(0, "the expired record must actually be evicted");
	}
}
