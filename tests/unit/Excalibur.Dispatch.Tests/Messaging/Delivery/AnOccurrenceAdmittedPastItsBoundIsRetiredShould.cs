// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

/// <summary>
/// An occurrence whose settlement is never committed must stop being dispatched.
/// </summary>
/// <remarks>
/// <para>
/// A host that is paused, migrated or collected past its execution lease loses the settlement write. The
/// occurrence stays the current one and the next poll admits it again -- correctly, because the fence makes
/// a late write safe and a lapsed holder must be able to be succeeded. Nothing in that loop retired the
/// occurrence, so the handler ran once per poll interval, indefinitely, while the schedule never advanced.
/// </para>
/// <para>
/// Both arms are required and the liveness one is the one that would be forgotten. The refusal alone is
/// satisfied by a service that dispatches nothing at all, so the second arm requires that an occurrence AT
/// the bound still runs. Nothing else in this file distinguishes a working bound from a broken scheduler.
/// </para>
/// <para>
/// Neither arm fakes the count. Both drive the real store through the real admission path and let it count,
/// because the whole defect is that the count has to be durable: a test that handed the service a chosen
/// number would pass against a store that keeps none.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Pattern", "Regression")]
public sealed class AnOccurrenceAdmittedPastItsBoundIsRetiredShould
{
	/// <summary>
	/// SAFETY: past the bound, the handler is not invoked again and the schedule is retired.
	/// </summary>
	/// <returns>The arm.</returns>
	[Fact]
	public async Task RefuseToDispatchAnOccurrenceAlreadyAdmittedPastItsBound()
	{
		await using var host = new ScheduleWorkerTestHost(maxAdmissionAttemptsPerOccurrence: 2);
		var row = await host.StoreRowAsync("past-the-bound");

		// Two admissions whose settlement never commits -- exactly what a host interrupted between the
		// dispatch and the fenced write leaves behind. The release is what the service's own finally block
		// does, and the count has to survive it.
		await AdmitAndAbandonAsync(host, row, times: 2, maximum: 2);

		var dispatched = 0;
		host.Handler.MessageCallback = (_, _) =>
		{
			dispatched++;
			return Task.CompletedTask;
		};

		await host.Service.StartAsync(CancellationToken.None);
		try
		{
			var retirement = await WaitForRetirementAsync(host, row.Id);

			dispatched.ShouldBe(0,
				"the third admission exceeds the bound of 2, so the handler must not be invoked again. Without "
				+ "the bound this occurrence is dispatched once per poll interval for as long as the host keeps "
				+ "losing its settlement, and the schedule never advances past it");

			retirement.Values["Attempt"].ShouldBe(3);
			retirement.Values["Bound"].ShouldBe(2);

			var current = (await host.Store.Inner.GetAllAsync(CancellationToken.None)).Single(item => item.Id == row.Id);
			current.Enabled.ShouldBeFalse(
				"retiring the occurrence has to be a committed settlement, or the next poll admits it again and "
				+ "the bound has only moved the loop rather than stopped it");
		}
		finally
		{
			await StopAsync(host);
		}
	}

	/// <summary>
	/// LIVENESS: an occurrence AT the bound still runs. Without this arm a scheduler that dispatches nothing
	/// passes the arm above.
	/// </summary>
	/// <returns>The arm.</returns>
	[Fact]
	public async Task StillDispatchAnOccurrenceOnItsLastPermittedAdmission()
	{
		await using var host = new ScheduleWorkerTestHost(maxAdmissionAttemptsPerOccurrence: 3);
		var row = await host.StoreRowAsync("at-the-bound");

		// Two abandoned admissions, so the service's own admission is the third -- equal to the bound, which
		// is permitted. One more would exceed it.
		await AdmitAndAbandonAsync(host, row, times: 2, maximum: 3);

		var delivered = ScheduleWorkerTestHost.Signal();
		var dispatched = 0;
		host.Handler.MessageCallback = (_, _) =>
		{
			dispatched++;
			delivered.TrySetResult();
			return Task.CompletedTask;
		};

		await host.Service.StartAsync(CancellationToken.None);
		try
		{
			await delivered.Task.WaitAsync(TimeSpan.FromSeconds(10));
			dispatched.ShouldBeGreaterThanOrEqualTo(1,
				"an occurrence on its last permitted admission must still run. A bound that retires it one "
				+ "admission early is a scheduler that silently drops work");

			host.Logger.Entries.ShouldNotContain(
				entry => entry.EventId == DeliveryEventId.ScheduleRetiredAfterUnsettledAdmissions,
				"the admission equal to the bound is permitted, so nothing should have been retired");
		}
		finally
		{
			await StopAsync(host);
		}
	}

	/// <summary>
	/// Drives the real admission path and abandons each lease without settling, leaving the occurrence
	/// current and its durable count raised.
	/// </summary>
	private static async Task AdmitAndAbandonAsync(ScheduleWorkerTestHost host, IScheduledMessage row, int times, int maximum)
	{
		for (var i = 0; i < times; i++)
		{
			var snapshot = (await host.Store.Inner.GetExecutionSnapshotsAsync(CancellationToken.None))
				.Single(s => s.Message.Id == row.Id);
			var admission = await host.Store.Inner.TryAcquireExecutionAsync(row.Id, snapshot.Generation,
				snapshot.Message.NextExecutionUtc!.Value, maximum, CancellationToken.None);
			var lease = admission.ShouldNotBeNull().Lease;
			_ = lease.ShouldNotBeNull($"the occurrence must still be admissible on abandoned admission {i + 1}");
			lease.AdmissionAttempt.ShouldBe(i + 1,
				"the store must count these durably, or this fixture cannot reach the bound at all and both "
				+ "arms would pass against a service with no bound");
			(await host.Store.Inner.ReleaseExecutionAsync(lease, CancellationToken.None)).ShouldBeTrue();
		}
	}

	private static async Task<(int EventId, IReadOnlyDictionary<string, object?> Values)> WaitForRetirementAsync(
		ScheduleWorkerTestHost host, Guid scheduleId)
	{
		using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		while (!deadline.IsCancellationRequested)
		{
			// Entries is a queue of value tuples, so FirstOrDefault yields a default tuple rather than null.
			// Cast to the nullable form or an absent entry reads as a present one with a null dictionary.
			var entry = host.Logger.Entries
				.Where(e => e.EventId == DeliveryEventId.ScheduleRetiredAfterUnsettledAdmissions
					&& e.Values.TryGetValue("ScheduleId", out var id) && Equals(id, scheduleId))
				.Cast<(int EventId, IReadOnlyDictionary<string, object?> Values)?>()
				.FirstOrDefault();
			if (entry is { } found)
			{
				return found;
			}
			await Task.Delay(TimeSpan.FromMilliseconds(10), deadline.Token).ConfigureAwait(false); // delay-ok: Poll the retirement event for the exact schedule identity under the existing deadline.
		}
		throw new TimeoutException("The occurrence past its admission bound was never retired.");
	}

	private static async Task StopAsync(ScheduleWorkerTestHost host)
	{
		await host.Service.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
		var loop = host.Service.ExecuteTask;
		if (loop is null)
		{
			return;
		}
		try
		{
			await loop;
		}
		catch (OperationCanceledException) when (loop.IsCanceled)
		{
		}
	}
}
