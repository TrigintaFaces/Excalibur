// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Storage;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Patterns.Tests.Sagas.Timeouts;

/// <summary>
/// Author≠impl concurrent-claimer regression lock for 7cgbx6 — <see cref="InMemorySagaTimeoutStore"/>'s
/// <c>ClaimDueTimeoutsAsync</c> is an <b>atomic disjoint claim</b>: two concurrent delivery workers draining
/// the same due set can never claim the same timeout (no double-fire), and a still-leased timeout is not
/// re-handed-out. Distinct from the pure-read <c>GetDueTimeoutsAsync</c> snapshot (which records no lease).
/// Same atomic select-and-claim contract as the provider saga stores, in-process backend.
/// </summary>
/// <remarks>
/// Deterministic (no wall-clock dependence, no Docker): timeouts are due in the past relative to a fixed
/// <c>asOf</c>; two claimers race via <see cref="Task.WhenAll(System.Threading.Tasks.Task[])"/>.
/// <para>
/// <b>RED-on-mutant:</b> remove the <c>lock (_dueLock)</c> around select→claim (select the due batch, then
/// record claims in a second step) ⇒ both workers select the same due timeouts before either records its
/// claims ⇒ <see cref="TwoWorkers_PartitionTheDueSet_WithNoDoubleClaim"/> observes overlapping ids → RED.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Saga)]
public sealed class InMemorySagaTimeoutClaimDisjointShould
{
	private static SagaTimeout DueTimeout(string id, DateTimeOffset dueAt) =>
		new(
			TimeoutId: id,
			SagaId: "saga-" + id,
			SagaType: "TestSaga",
			TimeoutType: "TestTimeout",
			TimeoutData: null,
			DueAt: dueAt,
			ScheduledAt: dueAt);

	[Fact]
	public async Task TwoWorkers_PartitionTheDueSet_WithNoDoubleClaim()
	{
		var store = new InMemorySagaTimeoutStore();
		var asOf = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
		var dueAt = asOf.AddMinutes(-1); // all due before asOf

		const int total = 40;
		const int batchSize = 20;
		for (var i = 0; i < total; i++)
		{
			await store.ScheduleTimeoutAsync(DueTimeout($"t{i:D3}", dueAt), CancellationToken.None);
		}

		// Two delivery workers claim concurrently from the SAME due set.
		var claims = await Task.WhenAll(
			Task.Run(async () =>
				(await store.ClaimDueTimeoutsAsync(asOf, batchSize, CancellationToken.None)).Select(t => t.TimeoutId).ToList()),
			Task.Run(async () =>
				(await store.ClaimDueTimeoutsAsync(asOf, batchSize, CancellationToken.None)).Select(t => t.TimeoutId).ToList()));

		var a = claims[0];
		var b = claims[1];

		var overlap = a.Intersect(b, StringComparer.Ordinal).ToList();
		overlap.ShouldBeEmpty(
			$"concurrent timeout claims must be disjoint — {overlap.Count} id(s) were claimed by both workers (double-fire)");

		var union = a.Concat(b).ToList();
		union.Count.ShouldBe(
			union.Distinct(StringComparer.Ordinal).Count(),
			"no timeout id may appear twice across the two concurrent claims");
	}

	[Fact]
	public async Task AClaimedTimeout_IsNotReClaimed_WhileTheLeaseIsValid()
	{
		var store = new InMemorySagaTimeoutStore();
		var asOf = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
		await store.ScheduleTimeoutAsync(DueTimeout("only", asOf.AddMinutes(-1)), CancellationToken.None);

		var first = (await store.ClaimDueTimeoutsAsync(asOf, 10, CancellationToken.None)).Select(t => t.TimeoutId).ToList();
		first.ShouldBe(["only"], "the single due timeout is claimed by the first worker");

		// Second claim at the same asOf (well within the 120s lease) must NOT re-hand-out the leased timeout.
		var second = (await store.ClaimDueTimeoutsAsync(asOf, 10, CancellationToken.None)).Select(t => t.TimeoutId).ToList();
		second.ShouldBeEmpty(
			"a timeout under a still-valid claim lease must not be re-claimed (no double delivery)");
	}
}
