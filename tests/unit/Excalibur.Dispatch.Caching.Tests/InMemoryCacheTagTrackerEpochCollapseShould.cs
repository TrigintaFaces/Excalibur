// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.Metrics;

using Tests.Shared.Helpers;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// The property the capacity bound must not cost us: <b>an invalidation is never lost.</b>
/// </summary>
/// <remarks>
/// <para>
/// At capacity the tracker has nowhere to record a bump for a tag it is not tracking. Dropping that
/// bump would be a silent, unbounded correctness fault; instead the bump collapses to a global
/// invalidation, so every entry written before it is treated as stale. The cost is a miss-rate spike
/// -- observable, bounded and self-correcting -- rather than stale data, which nothing would reveal.
/// </para>
/// <para>
/// <b>Requirement</b>: after a bump that cannot be recorded, an entry written under a DIFFERENT,
/// still-tracked tag is invalid. <b>Predicate tested</b>: that tag's stamp resolves to a different
/// value than the one an entry would have recorded, which is exactly the comparison the caching
/// middleware makes when it decides freshness. Asserting the map merely stayed bounded is the proxy
/// and is not sufficient on its own -- a tracker that silently dropped the bump would pass that.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryCacheTagTrackerEpochCollapseShould
{
	private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

	private static InMemoryCacheTagTracker CreateTracker(int capacity, IMeterFactory meterFactory) =>
		new(meterFactory, MsOptions.Create(new CacheOptions { TagTrackerCapacity = capacity }), logger: null);

	private static async Task FillToCapacityAsync(InMemoryCacheTagTracker tracker, int capacity)
	{
		for (var i = 0; i < capacity; i++)
		{
			_ = await tracker.GetOrCreateStampAsync($"fill-{i}", Ct);
		}
	}

	[Fact]
	public async Task InvalidateAnEntryUnderADifferentTag_WhenABumpAtCapacityCannotBeRecorded()
	{
		// THE PROPERTY. Fill the bound, then bump a tag the tracker never admitted.
		using var meterFactory = new TestMeterFactory();
		var tracker = CreateTracker(capacity: 4, meterFactory);
		await FillToCapacityAsync(tracker, 4);

		// What a cached entry under an ADMITTED tag would have recorded.
		var recorded = await tracker.GetOrCreateStampAsync("fill-0", Ct);

		await tracker.BumpStampAsync("never-admitted", Ct);

		var current = await tracker.GetOrCreateStampAsync("fill-0", Ct);

		current.ShouldNotBe(
			recorded,
			"a bump that cannot be recorded per-tag must still invalidate -- an entry under a DIFFERENT "
			+ "tag has to compare unequal, or that invalidation was silently lost");
	}

	[Fact]
	public async Task LeaveOtherTagsAlone_WhenABumpIsRecordedNormally()
	{
		// LIVENESS the other way: below the bound nothing collapses, so a bump must NOT invalidate
		// unrelated tags. Without this, "invalidate everything on every bump" would pass the arm above.
		using var meterFactory = new TestMeterFactory();
		var tracker = CreateTracker(capacity: 8, meterFactory);

		var recorded = await tracker.GetOrCreateStampAsync("orders", Ct);
		await tracker.BumpStampAsync("users", Ct);
		var current = await tracker.GetOrCreateStampAsync("orders", Ct);

		current.ShouldBe(recorded, "a recordable bump invalidates only the tag it names");
	}

	[Fact]
	public async Task StayBounded_WhenBumpedRepeatedlyWithUntrackedTags()
	{
		// The memory bound the bead opened on. Secondary to the property above, not a substitute:
		// a tracker that dropped the bump entirely would also pass this.
		using var meterFactory = new TestMeterFactory();
		var tracker = CreateTracker(capacity: 4, meterFactory);
		await FillToCapacityAsync(tracker, 4);

		for (var i = 0; i < 500; i++)
		{
			await tracker.BumpStampAsync($"unbounded-{i}", Ct);
		}

		tracker.TrackedTagCount.ShouldBeLessThanOrEqualTo(
			4,
			"the invalidate path must not grow the map past the bound the resolve path enforces");
	}
}
