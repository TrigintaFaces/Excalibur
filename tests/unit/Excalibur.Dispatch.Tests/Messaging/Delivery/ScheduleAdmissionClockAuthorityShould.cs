// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Delivery;

using Microsoft.Extensions.Time.Testing;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

/// <summary>
/// Occurrence admission judges due-ness against the STORE's clock, never the caller's.
/// </summary>
/// <remarks>
/// <para>
/// <b>The failure this prevents is not "a host is occasionally early".</b> Admission is first-come, so
/// when due-ness rests on each caller's own clock the host running FASTEST wins the race for every
/// occurrence, deterministically. One misconfigured host captures the entire schedule inventory and runs
/// all of it ahead of time, and because it also stamps the execution instant from that same clock, the
/// stored record agrees with it. Nothing downstream can tell.
/// </para>
/// <para>
/// <b>Why the in-memory store is tested for this at all</b>, given a single process cannot disagree with
/// itself about the time: the obligation is on the contract, not on the providers that happen to be
/// exposed to skew. A rule honoured only where it cannot be violated is not a rule the next provider
/// author will find.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Pattern", "Concurrency")]
public sealed class ScheduleAdmissionClockAuthorityShould
{
	/// <summary>
	/// SAFETY: a caller running ahead of the store cannot admit an occurrence that is not yet due.
	/// </summary>
	[Fact]
	public async Task Refuse_an_occurrence_that_is_not_yet_due_by_the_stores_clock()
	{
		var storeNow = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
		var clock = new FakeTimeProvider(storeNow);
		var store = new InMemoryScheduleStore(clock);

		// Five minutes in the store's future -- the window a host with a fast clock would have claimed.
		var due = storeNow.AddMinutes(5);
		var schedule = new ScheduledMessage { MessageName = "probe", MessageBody = "{}", Enabled = true, NextExecutionUtc = due };
		await store.StoreAsync(schedule, CancellationToken.None);

		var snapshot = (await store.GetExecutionSnapshotsAsync(CancellationToken.None)).Single();

		var lease = await store.TryAcquireExecutionAsync(
			snapshot.Message.Id, snapshot.Generation, due, CancellationToken.None);

		lease.ShouldBeNull("the occurrence is still in the store's future, whatever the caller's clock says");
	}

	/// <summary>
	/// LIVENESS: once the store's own clock reaches the instant, the same occurrence is admitted.
	/// </summary>
	/// <remarks>
	/// Paired with the arm above and not optional. A guard that refuses everything satisfies the safety
	/// arm perfectly and stops every scheduled message in the product from ever running — a far worse
	/// defect than the one being fixed, and invisible to a refusal-only suite.
	/// </remarks>
	[Fact]
	public async Task Admit_the_same_occurrence_once_the_stores_clock_reaches_it()
	{
		var storeNow = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
		var clock = new FakeTimeProvider(storeNow);
		var store = new InMemoryScheduleStore(clock);

		var due = storeNow.AddMinutes(5);
		var schedule = new ScheduledMessage { MessageName = "probe", MessageBody = "{}", Enabled = true, NextExecutionUtc = due };
		await store.StoreAsync(schedule, CancellationToken.None);

		var snapshot = (await store.GetExecutionSnapshotsAsync(CancellationToken.None)).Single();

		(await store.TryAcquireExecutionAsync(snapshot.Message.Id, snapshot.Generation, due, CancellationToken.None))
			.ShouldBeNull("precondition: not due yet");

		clock.Advance(TimeSpan.FromMinutes(5));

		var lease = await store.TryAcquireExecutionAsync(
			snapshot.Message.Id, snapshot.Generation, due, CancellationToken.None);

		lease.ShouldNotBeNull("the occurrence is now due by the store's own clock");
	}
}
