// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Delivery;

namespace Excalibur.Testing.Conformance;

/// <summary>Conformance arms for renewable, expiring schedule execution ownership.</summary>
/// <remarks>
/// Wire all six renewal arms and all eighteen inherited arms. The factory and reset must address the
/// same authority. These arms certify expiry and fencing behavior, not process recovery, durable storage
/// or exactly-once handler effects. Each arm has a thirty-second operation deadline plus bounded cleanup;
/// each matching release has a separate five-second deadline. Fixtures must tear down any provider RPC
/// that ignores cancellation and remains outstanding after a deadline. Choose a short test grant
/// that permits renewal before expiry. A missed timing window fails rather than skipping an arm.
/// </remarks>
public abstract class ScheduleExecutionLeaseRenewalConformanceTestKit : ScheduleExecutionStoreConformanceTestKit
{
	/// <summary>Creates the renewable authority exercised by all inherited and renewal arms.</summary>
	/// <returns>The configured renewable execution store.</returns>
	protected abstract override IScheduleExecutionLeaseRenewal CreateStore();

	/// <summary>Waits for elapsed time on the provider's authoritative clock.</summary>
	/// <param name="duration">Elapsed duration required by the arm.</param>
	/// <param name="cancellationToken">The bounded arm deadline.</param>
	/// <returns>The elapsed-time operation.</returns>
	/// <remarks>
	/// The default uses real time. Override only to advance a clock owned by the tested provider.
	/// Advancing a scheduler's unrelated clock does not advance database ownership expiry.
	/// </remarks>
	protected virtual async Task WaitForLeaseTimeAsync(TimeSpan duration, CancellationToken cancellationToken)
	{
		while (duration > TimeSpan.Zero)
		{
			var slice = duration > TimeSpan.FromDays(1) ? TimeSpan.FromDays(1) : duration;
			await Task.Delay(slice, cancellationToken).ConfigureAwait(false);
			duration -= slice;
		}
	}

	/// <summary>Verifies renewal extends ownership beyond the original acquisition grant.</summary>
	public virtual Task RenewalExtendsCurrentOwnership() => RunArmAsync(async (store, message, lease, token) =>
	{
		var step = TimeSpan.FromTicks(Math.Max(1, store.LeaseDuration.Ticks / 3));
		for (var index = 0; index < 4; index++)
		{
			await WaitForLeaseTimeAsync(step, token).ConfigureAwait(false);
			Require(await store.TryRenewExecutionAsync(lease, token).ConfigureAwait(false), "Current ownership was not renewed.");
		}
		Require(await store.TryAcquireExecutionAsync(message.Id, lease.Generation, lease.OccurrenceUtc, token).ConfigureAwait(false) is null,
			"A competitor acquired ownership after successful renewal.");
		Require(await store.TryUpdateProgressAsync(lease, new(lease.OccurrenceUtc, null, true), token).ConfigureAwait(false),
			"Renewed ownership could not commit beyond its original expiry.");
	});

	/// <summary>Verifies expired authority cannot renew, commit progress or clear successor ownership.</summary>
	public virtual Task ExpiredAuthorityCannotRenewOrProgress() => RunArmAsync(async (store, message, lease, token) =>
	{
		await WaitPastExpiryAsync(store, token).ConfigureAwait(false);
		Require(!await store.TryRenewExecutionAsync(lease, token).ConfigureAwait(false), "Expired ownership was resurrected.");
		Require(!await store.TryUpdateProgressAsync(lease, new(null, null, true), token).ConfigureAwait(false), "Expired authority committed progress.");
		var successor = await AcquireAsync(store, message.Id, lease.Generation, lease.OccurrenceUtc, token).ConfigureAwait(false);
		try
		{
			Require(successor.OwnerToken != lease.OwnerToken, "Replacement reused expired bearer authority.");
			Require(!await store.ReleaseExecutionAsync(lease, token).ConfigureAwait(false), "Expired authority released its successor.");
			Require(await store.TryRenewExecutionAsync(successor, token).ConfigureAwait(false), "Stale release damaged successor ownership.");
		}
		finally
		{
			await ReleaseBoundedAsync(store, successor).ConfigureAwait(false);
		}
	});

	/// <summary>Verifies released and individually altered bearer fields cannot renew or release a successor.</summary>
	public virtual Task ReleasedAndForeignAuthorityCannotRenewSuccessor() => RunArmAsync(async (store, message, lease, token) =>
	{
		Require(await store.ReleaseExecutionAsync(lease, token).ConfigureAwait(false), "Current ownership could not be released.");
		var successor = await AcquireAsync(store, message.Id, lease.Generation, lease.OccurrenceUtc, token).ConfigureAwait(false);
		try
		{
			var foreign = new[]
			{
				lease,
				new ScheduleExecutionLease(Guid.NewGuid(), successor.ScheduleId, successor.Generation, successor.OccurrenceUtc, successor.OwnerToken),
				new ScheduleExecutionLease(successor.StoreId, Guid.NewGuid(), successor.Generation, successor.OccurrenceUtc, successor.OwnerToken),
				new ScheduleExecutionLease(successor.StoreId, successor.ScheduleId, Guid.NewGuid(), successor.OccurrenceUtc, successor.OwnerToken),
				new ScheduleExecutionLease(successor.StoreId, successor.ScheduleId, successor.Generation, successor.OccurrenceUtc.AddTicks(1), successor.OwnerToken),
				new ScheduleExecutionLease(successor.StoreId, successor.ScheduleId, successor.Generation, successor.OccurrenceUtc, Guid.NewGuid()),
			};
			foreach (var altered in foreign)
			{
				Require(!await store.TryRenewExecutionAsync(altered, token).ConfigureAwait(false), "Foreign or released authority renewed a successor.");
				Require(!await store.TryUpdateProgressAsync(altered, new(null, null, true), token).ConfigureAwait(false), "Foreign authority committed progress.");
				Require(!await store.ReleaseExecutionAsync(altered, token).ConfigureAwait(false), "Foreign authority released a successor.");
			}
			Require(await store.TryRenewExecutionAsync(successor, token).ConfigureAwait(false), "Rejected foreign authority damaged the valid owner.");
		}
		finally
		{
			await ReleaseBoundedAsync(store, successor).ConfigureAwait(false);
		}
	});

	/// <summary>Verifies administrative replacement and completion invalidate old renewal authority.</summary>
	public virtual Task AdministrativeMutationInvalidatesRenewal() => RunArmAsync(async (store, message, lease, token) =>
	{
		await store.StoreAsync(message, token).ConfigureAwait(false);
		Require(!await store.TryRenewExecutionAsync(lease, token).ConfigureAwait(false), "Replacement retained old renewal authority.");
		var snapshot = (await store.GetExecutionSnapshotsAsync(token).ConfigureAwait(false)).Single(item => item.Message.Id == message.Id);
		Require(snapshot.Generation != lease.Generation, "Administrative replacement retained its generation.");
		var successor = await AcquireAsync(store, message.Id, snapshot.Generation, snapshot.Message.NextExecutionUtc!.Value, token).ConfigureAwait(false);
		try
		{
			await store.CompleteAsync(message.Id, token).ConfigureAwait(false);
			Require(!await store.TryRenewExecutionAsync(successor, token).ConfigureAwait(false), "Completion retained renewal authority.");
			Require(!await store.TryUpdateProgressAsync(successor, new(null, null), token).ConfigureAwait(false), "Completed ownership changed progress.");
			var actual = (await store.GetExecutionSnapshotsAsync(token).ConfigureAwait(false)).SingleOrDefault(item => item.Message.Id == message.Id);
			Require(actual is null || !actual.Message.Enabled, "Rejected renewal re-enabled a completed schedule.");
			Require(await store.TryAcquireExecutionAsync(message.Id, successor.Generation, successor.OccurrenceUtc, token).ConfigureAwait(false) is null,
				"Completed authority admitted a stale occurrence.");
		}
		finally
		{
			await ReleaseBoundedAsync(store, successor).ConfigureAwait(false);
		}
	});

	/// <summary>Verifies renewal preserves the one-progress limit and remains available after commit until release.</summary>
	public virtual async Task RenewalPreservesProgressOnceIncludingAfterCommit()
	{
		using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		await VerifyCommittedRenewalAsync(disable: false, deadline.Token).ConfigureAwait(false);
		await VerifyCommittedRenewalAsync(disable: true, deadline.Token).ConfigureAwait(false);
	}

	private Task VerifyCommittedRenewalAsync(bool disable, CancellationToken cancellationToken) => RunArmAsync(async (store, message, lease, token) =>
	{
		Require(await store.TryRenewExecutionAsync(lease, token).ConfigureAwait(false), "Current owner could not renew before progress.");
		var next = lease.OccurrenceUtc.AddMinutes(1);
		Require(await store.TryUpdateProgressAsync(lease, new(lease.OccurrenceUtc, next, disable), token).ConfigureAwait(false), "First progress was refused.");
		Require(await store.TryRenewExecutionAsync(lease, token).ConfigureAwait(false), "Progress commit disabled settlement renewal.");
		Require(!await store.TryUpdateProgressAsync(lease, new(null, null, true), token).ConfigureAwait(false), "Renewal reset the one-progress limit.");
		var actual = (await store.GetExecutionSnapshotsAsync(token).ConfigureAwait(false)).Single(item => item.Message.Id == message.Id);
		Require(actual.Message.LastExecutionUtc == lease.OccurrenceUtc && actual.Message.NextExecutionUtc == next && actual.Message.Enabled == !disable,
			"Renewal or duplicate progress mutated committed schedule progress.");
		Require(await store.ReleaseExecutionAsync(lease, token).ConfigureAwait(false), "Original occurrence could not release after renewed progress.");
		Require(!await store.TryRenewExecutionAsync(lease, token).ConfigureAwait(false), "Released committed ownership was renewed.");
	}, cancellationToken);

	/// <summary>Verifies already-canceled renewal neither releases nor extends ownership.</summary>
	public virtual Task CanceledRenewalLeavesOwnershipUnchanged() => RunArmAsync(async (store, message, lease, token) =>
	{
		await WaitForLeaseTimeAsync(TimeSpan.FromTicks(Math.Max(1, store.LeaseDuration.Ticks / 2)), token).ConfigureAwait(false);
		using var canceled = new CancellationTokenSource();
		await canceled.CancelAsync().ConfigureAwait(false);
		try
		{
			await store.TryRenewExecutionAsync(lease, canceled.Token).ConfigureAwait(false);
			throw new TestFixtureAssertionException("Already-canceled renewal did not throw cancellation.");
		}
		catch (OperationCanceledException)
		{
			// The original owner must still exclude competitors without gaining a new grant.
		}
		Require(await store.TryAcquireExecutionAsync(message.Id, lease.Generation, lease.OccurrenceUtc, token).ConfigureAwait(false) is null,
			"Canceled renewal released ownership.");
		await WaitForLeaseTimeAsync(TimeSpan.FromTicks(Math.Max(1, store.LeaseDuration.Ticks / 4 * 3)), token).ConfigureAwait(false);
		Require(!await store.TryRenewExecutionAsync(lease, token).ConfigureAwait(false), "Canceled renewal extended the original grant.");
		var successor = await AcquireAsync(store, message.Id, lease.Generation, lease.OccurrenceUtc, token).ConfigureAwait(false);
		await ReleaseBoundedAsync(store, successor).ConfigureAwait(false);
	});

	private async Task RunArmAsync(Func<IScheduleExecutionLeaseRenewal, IScheduledMessage, ScheduleExecutionLease, CancellationToken, Task> arm,
		CancellationToken cancellationToken = default)
	{
		using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		deadline.CancelAfter(TimeSpan.FromSeconds(30));
		var store = (IScheduleExecutionLeaseRenewal)await CreateStoreForArmAsync().WaitAsync(deadline.Token).ConfigureAwait(false);
		Require(store.LeaseDuration > TimeSpan.Zero, "LeaseDuration must be positive.");
		var message = CreateScheduledMessage();
		message.Enabled = true;
		message.NextExecutionUtc = DateTimeOffset.UtcNow;
		await store.StoreAsync(message, deadline.Token).WaitAsync(deadline.Token).ConfigureAwait(false);
		var snapshot = (await store.GetExecutionSnapshotsAsync(deadline.Token).WaitAsync(deadline.Token).ConfigureAwait(false))
			.Single(item => item.Message.Id == message.Id);
		var lease = await AcquireAsync(store, message.Id, snapshot.Generation, message.NextExecutionUtc.Value, deadline.Token).ConfigureAwait(false);
		try
		{
			await arm(store, message, lease, deadline.Token).WaitAsync(deadline.Token).ConfigureAwait(false);
		}
		finally
		{
			await ReleaseBoundedAsync(store, lease).ConfigureAwait(false);
		}
	}

	private async Task WaitPastExpiryAsync(IScheduleExecutionLeaseRenewal store, CancellationToken token)
	{
		await WaitForLeaseTimeAsync(store.LeaseDuration, token).ConfigureAwait(false);
		await WaitForLeaseTimeAsync(TimeSpan.FromTicks(Math.Max(1, store.LeaseDuration.Ticks / 4)), token).ConfigureAwait(false);
	}

	private static async Task<ScheduleExecutionLease> AcquireAsync(IScheduleExecutionLeaseRenewal store, Guid id, Guid generation,
		DateTimeOffset occurrence, CancellationToken token) =>
		await store.TryAcquireExecutionAsync(id, generation, occurrence, token).WaitAsync(token).ConfigureAwait(false)
		?? throw new TestFixtureAssertionException("Current unowned occurrence was not admitted.");

	private static async Task ReleaseBoundedAsync(IScheduleExecutionLeaseRenewal store, ScheduleExecutionLease lease)
	{
		using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		await store.ReleaseExecutionAsync(lease, deadline.Token).WaitAsync(deadline.Token).ConfigureAwait(false);
	}

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new TestFixtureAssertionException(message);
		}
	}
}
