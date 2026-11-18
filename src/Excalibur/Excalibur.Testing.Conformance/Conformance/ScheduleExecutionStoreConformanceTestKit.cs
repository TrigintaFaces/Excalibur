// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Delivery;

namespace Excalibur.Testing.Conformance;

/// <summary>Additional conformance arms for authoritative schedule execution stores.</summary>
/// <remarks>
/// Derive from this kit and wire every inherited and new arm to the test runner. No capability arm skips.
/// The factory must return the SAME authority whose data ResetDataAsync clears; reset must not dispose its
/// connection. Consumers own teardown of clients, connections and any residual ownership after failed arms.
/// This kit tests bounded admission and fencing, not durability, distributed recovery or exactly-once effects.
/// </remarks>
public abstract class ScheduleExecutionStoreConformanceTestKit : ScheduleStoreConformanceTestKit
{
	/// <summary>Creates the authoritative execution store tested by all inherited and new arms.</summary>
	/// <returns>The configured authority.</returns>
	protected abstract override IScheduleExecutionStore CreateStore();

	private async Task<IScheduleExecutionStore> StoreForArmAsync() =>
		(IScheduleExecutionStore)await CreateStoreForArmAsync().ConfigureAwait(false);

	private IScheduledMessage DueMessage()
	{
		var message = CreateScheduledMessage();
		message.Enabled = true;

		// Comfortably in the past, not "now". Admission refuses an occurrence that is not yet due by the
		// store's own clock, and several arms advance the occurrence forward and acquire it again -- from
		// an occurrence of exactly now, the advanced one is in the future and is correctly refused. The
		// margin keeps every arm's occurrence due without any arm having to reason about the clock.
		message.NextExecutionUtc = DateTimeOffset.UtcNow.AddMinutes(-5);
		return message;
	}

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new TestFixtureAssertionException(message);
		}
	}

	private static async Task<ScheduleExecutionSnapshot> SnapshotAsync(IScheduleExecutionStore store, Guid id) =>
		(await store.GetExecutionSnapshotsAsync(CancellationToken.None).ConfigureAwait(false)).Single(s => s.Message.Id == id);

	private static async Task<ScheduleExecutionLease> AcquireAsync(IScheduleExecutionStore store, ScheduleExecutionSnapshot snapshot) =>
		await store.TryAcquireExecutionAsync(snapshot.Message.Id, snapshot.Generation, snapshot.Message.NextExecutionUtc!.Value,
			CancellationToken.None).ConfigureAwait(false)
		?? throw new TestFixtureAssertionException("Current enabled unowned occurrence was not admitted.");

	/// <summary>Verifies caller input mutation cannot change stored data or authority.</summary>
	public virtual async Task StoreInputRemainsDetached()
	{
		var store = await StoreForArmAsync().ConfigureAwait(false);
		var message = DueMessage();
		var id = message.Id;
		var body = message.MessageBody;
		var due = message.NextExecutionUtc;
		await store.StoreAsync(message, CancellationToken.None).ConfigureAwait(false);
		message.MessageBody = "mutated";
		message.Enabled = false;
		message.NextExecutionUtc = due!.Value.AddDays(1);
		var actual = await SnapshotAsync(store, id).ConfigureAwait(false);
		Require(actual.Message.MessageBody == body && actual.Message.Enabled && actual.Message.NextExecutionUtc == due,
			"Stored input remains caller-owned and mutable.");
	}

	/// <summary>Verifies both inventory and execution snapshots are detached from authoritative state.</summary>
	public virtual async Task ReturnedSnapshotsRemainDetached()
	{
		var store = await StoreForArmAsync().ConfigureAwait(false);
		var message = DueMessage();
		await store.StoreAsync(message, CancellationToken.None).ConfigureAwait(false);
		var snapshot = await SnapshotAsync(store, message.Id).ConfigureAwait(false);
		var inventory = (await store.GetAllAsync(CancellationToken.None).ConfigureAwait(false)).Single(m => m.Id == message.Id);
		inventory.Enabled = false;
		inventory.MessageBody = "inventory-mutation";
		snapshot.Message.Enabled = false;
		snapshot.Message.MessageBody = "snapshot-mutation";
		snapshot.Message.NextExecutionUtc = null;
		var actual = await SnapshotAsync(store, message.Id).ConfigureAwait(false);
		Require(actual.Generation == snapshot.Generation && actual.Message.Enabled
			&& actual.Message.MessageBody == message.MessageBody && actual.Message.NextExecutionUtc == message.NextExecutionUtc,
			"Returned snapshots changed authoritative state.");
	}

	/// <summary>Verifies one winner while all contenders target the same still-owned occurrence.</summary>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("ApiDesign", "RS0030",
		Justification = "This conformance arm deliberately schedules ready-gated contenders on the thread pool to exercise synchronous providers concurrently; it does not wrap a library operation in an asynchronous facade.")]
	public virtual async Task SimultaneousAdmissionHasOneWinner()
	{
		var store = await StoreForArmAsync().ConfigureAwait(false);
		var message = DueMessage();
		await store.StoreAsync(message, CancellationToken.None).ConfigureAwait(false);
		var snapshot = await SnapshotAsync(store, message.Id).ConfigureAwait(false);
		var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var readyCount = 0;
		const int contenderCount = 8;
		var contenders = Enumerable.Range(0, contenderCount).Select(_ => Task.Run(async () =>
		{
			if (Interlocked.Increment(ref readyCount) == contenderCount)
			{
				ready.SetResult();
			}
			await start.Task.ConfigureAwait(false);
			return await store.TryAcquireExecutionAsync(message.Id, snapshot.Generation, message.NextExecutionUtc!.Value,
				CancellationToken.None).ConfigureAwait(false);
		})).ToArray();
		await ready.Task.ConfigureAwait(false);
		start.SetResult();
		var outcomes = await Task.WhenAll(contenders).ConfigureAwait(false);
		try
		{
			Require(outcomes.Count(lease => lease is not null) == 1, "Exactly one simultaneous admission must win.");
		}
		finally
		{
			foreach (var lease in outcomes.Where(lease => lease is not null))
			{
				await store.ReleaseExecutionAsync(lease!, CancellationToken.None).ConfigureAwait(false);
			}
		}
	}

	/// <summary>Verifies completion and same-due reactivation invalidate stale admission and progress.</summary>
	public virtual async Task AdministrativeChangesInvalidateOldAuthority()
	{
		var store = await StoreForArmAsync().ConfigureAwait(false);
		var message = DueMessage();
		await store.StoreAsync(message, CancellationToken.None).ConfigureAwait(false);
		var first = await SnapshotAsync(store, message.Id).ConfigureAwait(false);
		var lease = await AcquireAsync(store, first).ConfigureAwait(false);
		await store.CompleteAsync(message.Id, CancellationToken.None).ConfigureAwait(false);
		Require(await store.TryAcquireExecutionAsync(message.Id, first.Generation, message.NextExecutionUtc!.Value,
			CancellationToken.None).ConfigureAwait(false) is null, "Completion did not fence old admission.");
		await store.StoreAsync(message, CancellationToken.None).ConfigureAwait(false);
		var next = await SnapshotAsync(store, message.Id).ConfigureAwait(false);
		Require(next.Generation != first.Generation && next.Message.Enabled, "Reactivation must use a fresh enabled generation.");
		Require(!await store.TryUpdateProgressAsync(lease, new(null, null, true), CancellationToken.None).ConfigureAwait(false),
			"Old progress changed a reactivated schedule.");
		var current = await AcquireAsync(store, next).ConfigureAwait(false);
		try
		{
			Require(!await store.ReleaseExecutionAsync(lease, CancellationToken.None).ConfigureAwait(false),
				"Old ownership released a reactivated schedule.");
			Require(await store.TryAcquireExecutionAsync(message.Id, next.Generation, message.NextExecutionUtc!.Value,
				CancellationToken.None).ConfigureAwait(false) is null, "Stale release cleared the current owner's admission.");
		}
		finally
		{
			Require(await store.ReleaseExecutionAsync(current, CancellationToken.None).ConfigureAwait(false), "Fresh generation was not live.");
		}
	}

	/// <summary>Verifies one progress commit, original-occurrence release and fencing against newer or foreign ownership.</summary>
	public virtual async Task ProgressAndReleaseRespectCurrentAuthority()
	{
		var store = await StoreForArmAsync().ConfigureAwait(false);
		var message = DueMessage();
		await store.StoreAsync(message, CancellationToken.None).ConfigureAwait(false);
		var first = await SnapshotAsync(store, message.Id).ConfigureAwait(false);
		var lease = await AcquireAsync(store, first).ConfigureAwait(false);
		var nextDue = message.NextExecutionUtc!.Value.AddMinutes(1);
		Require(await store.TryUpdateProgressAsync(lease, new(message.NextExecutionUtc, nextDue), CancellationToken.None).ConfigureAwait(false),
			"First progress was refused.");
		Require(!await store.TryUpdateProgressAsync(lease, new(null, message.NextExecutionUtc), CancellationToken.None).ConfigureAwait(false),
			"Duplicate progress was accepted.");
		Require(await store.ReleaseExecutionAsync(lease, CancellationToken.None).ConfigureAwait(false), "Original-occurrence release failed after progress.");
		var actual = await SnapshotAsync(store, message.Id).ConfigureAwait(false);
		Require(actual.Message.NextExecutionUtc == nextDue && actual.Message.MessageBody == message.MessageBody && actual.Message.Enabled,
			"Progress changed payload, enabled state or regressed the occurrence.");
		var newer = await AcquireAsync(store, actual).ConfigureAwait(false);
		try
		{
			Require(!await store.ReleaseExecutionAsync(lease, CancellationToken.None).ConfigureAwait(false), "Stale token released newer ownership.");
			var foreign = new ScheduleExecutionLease(Guid.NewGuid(), newer.ScheduleId, newer.Generation, newer.OccurrenceUtc, newer.OwnerToken) { AdmissionAttempt = newer.AdmissionAttempt };
			Require(!await store.TryUpdateProgressAsync(foreign, new(null, null, true), CancellationToken.None).ConfigureAwait(false),
				"Foreign logical-store authority committed progress.");
			Require(!await store.ReleaseExecutionAsync(foreign, CancellationToken.None).ConfigureAwait(false), "Foreign authority released ownership.");
		}
		finally
		{
			await store.ReleaseExecutionAsync(newer, CancellationToken.None).ConfigureAwait(false);
		}
	}

	/// <summary>Verifies already-canceled admission, progress and release cannot mutate authority or progress.</summary>
	public virtual async Task CanceledOperationsLeaveAuthorityUnchanged()
	{
		var store = await StoreForArmAsync().ConfigureAwait(false);
		var message = DueMessage();
		await store.StoreAsync(message, CancellationToken.None).ConfigureAwait(false);
		var snapshot = await SnapshotAsync(store, message.Id).ConfigureAwait(false);
		using var source = new CancellationTokenSource();
		await source.CancelAsync().ConfigureAwait(false);
		await ExpectCancellationAsync(async () =>
			await store.TryAcquireExecutionAsync(message.Id, snapshot.Generation, message.NextExecutionUtc!.Value, source.Token).ConfigureAwait(false)).ConfigureAwait(false);
		var lease = await AcquireAsync(store, snapshot).ConfigureAwait(false);
		await ExpectCancellationAsync(async () =>
			await store.TryUpdateProgressAsync(lease, new(null, null, true), source.Token).ConfigureAwait(false)).ConfigureAwait(false);
		await ExpectCancellationAsync(async () => await store.ReleaseExecutionAsync(lease, source.Token).ConfigureAwait(false)).ConfigureAwait(false);
		var actual = await SnapshotAsync(store, message.Id).ConfigureAwait(false);
		Require(actual.Generation == snapshot.Generation && actual.Message.Enabled && actual.Message.NextExecutionUtc == message.NextExecutionUtc,
			"Cancellation changed schedule progress.");
		Require(await store.TryUpdateProgressAsync(lease, new(message.NextExecutionUtc, null, true), CancellationToken.None).ConfigureAwait(false),
			"Canceled progress consumed the valid ownership.");
		Require(await store.ReleaseExecutionAsync(lease, CancellationToken.None).ConfigureAwait(false), "Canceled release consumed ownership.");
	}

	/// <summary>Verifies the admission count rises per unsettled admission and resets only on a committed settlement.</summary>
	/// <remarks>
	/// <para>
	/// An occurrence whose settlement is never committed is admitted again by the next poll, legitimately:
	/// the fence makes a late write safe, so a lapsed holder must be able to be succeeded. Nothing in that
	/// loop retires the occurrence, so the count reported by the admission is the ONLY thing a caller can
	/// bound it with, and a store that does not keep it durably leaves the repeat unbounded.
	/// </para>
	/// <para>
	/// <b>The reset point is the load-bearing half, and it is the one a provider gets wrong.</b> Keying the
	/// count to the admitted-occurrence column looks equivalent and is not: releasing a lease clears that
	/// column, and releasing is exactly what happens on the path being counted — so such a store reports a
	/// first attempt forever while passing every arm about a healthy schedule. This arm therefore releases
	/// between admissions, which is what a real host does, and requires the count to survive it.
	/// </para>
	/// <para>
	/// Both halves are required. Without the rising half a store that always reports 1 passes; without the
	/// reset half a store that only ever increments passes, and it would retire every healthy schedule once
	/// enough occurrences had run.
	/// </para>
	/// </remarks>
	/// <returns>The arm.</returns>
	public virtual async Task AdmissionCountRisesUntilSettlementCommits()
	{
		var store = await StoreForArmAsync().ConfigureAwait(false);
		var message = DueMessage();
		await store.StoreAsync(message, CancellationToken.None).ConfigureAwait(false);

		var snapshot = await SnapshotAsync(store, message.Id).ConfigureAwait(false);
		var first = await AcquireAsync(store, snapshot).ConfigureAwait(false);
		Require(first.AdmissionAttempt == 1,
			$"First admission of an occurrence reported attempt {first.AdmissionAttempt}, not 1.");

		// What a host does when its settlement is refused: it gives the authority back without advancing
		// the occurrence. The count must not be reset by this.
		Require(await store.ReleaseExecutionAsync(first, CancellationToken.None).ConfigureAwait(false),
			"The admitted occurrence could not be released.");

		var second = await AcquireAsync(store, snapshot).ConfigureAwait(false);
		Require(second.AdmissionAttempt == 2,
			$"Re-admission after an uncommitted settlement reported attempt {second.AdmissionAttempt}, not 2. "
			+ "A count that a release resets can never reach a bound on the path that needs one.");

		Require(await store.ReleaseExecutionAsync(second, CancellationToken.None).ConfigureAwait(false),
			"The re-admitted occurrence could not be released.");

		var third = await AcquireAsync(store, snapshot).ConfigureAwait(false);
		Require(third.AdmissionAttempt == 3,
			$"A third unsettled admission reported attempt {third.AdmissionAttempt}, not 3. The count must "
			+ "keep rising for as long as no settlement commits, which is what lets a caller bound it.");

		// A COMMITTED settlement retires the occurrence, so the next one starts over.
		var advanced = snapshot.Message.NextExecutionUtc!.Value.AddMinutes(1);
		Require(await store.TryUpdateProgressAsync(third, new(snapshot.Message.NextExecutionUtc, advanced, false), CancellationToken.None).ConfigureAwait(false),
			"The current authority could not commit progress.");
		Require(await store.ReleaseExecutionAsync(third, CancellationToken.None).ConfigureAwait(false),
			"The settled authority could not be released.");

		var settled = await SnapshotAsync(store, message.Id).ConfigureAwait(false);
		var next = await AcquireAsync(store, settled).ConfigureAwait(false);
		Require(next.AdmissionAttempt == 1,
			$"The first admission after a committed settlement reported attempt {next.AdmissionAttempt}, not 1. "
			+ "A count that only ever rises would retire a healthy schedule.");
		Require(await store.ReleaseExecutionAsync(next, CancellationToken.None).ConfigureAwait(false),
			"The final authority could not be released.");
	}

	private static async Task ExpectCancellationAsync(Func<Task> operation)
	{
		try
		{
			await operation().ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			return;
		}
		throw new TestFixtureAssertionException("Already-canceled operation was not canceled.");
	}
}
