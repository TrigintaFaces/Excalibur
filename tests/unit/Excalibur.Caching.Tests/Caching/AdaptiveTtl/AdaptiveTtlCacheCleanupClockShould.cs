// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Caching.AdaptiveTtl;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Excalibur.Tests.Caching.AdaptiveTtl;

/// <summary>
/// Binds the metadata-cleanup sweep to the injected clock. The cache already measures entry age with the
/// injected <see cref="TimeProvider"/>; the periodic sweep that acts on that age must be scheduled from
/// the same clock, or the two halves of the cleanup disagree about what time it is.
/// </summary>
/// <remarks>
/// Both arms are needed. The pruning arm alone is satisfied by a sweep that discards everything; the
/// retention arm says a recently-touched entry survives the same sweep, so what is being measured is the
/// age cutoff and not a blanket clear.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class AdaptiveTtlCacheCleanupClockShould
{
	/// <summary>The cleanup sweep's period, as scheduled by the cache.</summary>
	private static readonly TimeSpan SweepPeriod = TimeSpan.FromMinutes(5);

	/// <summary>The age at which the sweep considers metadata stale.</summary>
	private static readonly TimeSpan StaleAfter = TimeSpan.FromHours(1);

	[Fact]
	public async Task PruneStaleMetadataWhenTheInjectedClockReachesTheSweep()
	{
		var clock = new FakeTimeProvider();
		await using var sut = Create(clock);

		sut.Set("stale-key", [1, 2, 3], new DistributedCacheEntryOptions());
		sut.GetMetrics().TotalCalculations.ShouldBe(
			1,
			"liveness: the entry has to be tracked before a sweep can be shown to remove it");

		// Past the staleness cutoff, and past a sweep boundary so the timer actually fires.
		clock.Advance(StaleAfter + SweepPeriod);

		sut.GetMetrics().TotalCalculations.ShouldBe(
			0,
			"advancing the injected clock past the staleness cutoff and a sweep boundary must run the "
			+ "cleanup -- if it does not, the sweep is scheduled off the ambient system clock and nothing "
			+ "a consumer or a test does to the injected clock can drive it");
	}

	[Fact]
	public async Task KeepMetadataThatIsStillFreshWhenTheSweepRuns()
	{
		var clock = new FakeTimeProvider();
		await using var sut = Create(clock);

		sut.Set("fresh-key", [1, 2, 3], new DistributedCacheEntryOptions());

		// Far enough to run a sweep, nowhere near the staleness cutoff.
		clock.Advance(SweepPeriod + TimeSpan.FromSeconds(1));

		sut.GetMetrics().TotalCalculations.ShouldBe(
			1,
			"the sweep ran but this entry is younger than the cutoff, so it must survive -- a sweep that "
			+ "cleared everything would satisfy the pruning arm while destroying the cache's own metadata");
	}

	private static AdaptiveTtlCache Create(TimeProvider clock)
	{
		var strategy = A.Fake<IAdaptiveTtlStrategy>();

		// A faked strategy returns TimeSpan.Zero, which the cache rejects as a relative expiry. Any
		// positive value will do -- the entry's TTL is not what this test measures.
		_ = A.CallTo(() => strategy.CalculateTtl(A<AdaptiveTtlContext>._)).Returns(TimeSpan.FromMinutes(10));

		return new AdaptiveTtlCache(
			A.Fake<IDistributedCache>(),
			strategy,
			NullLogger<AdaptiveTtlCache>.Instance,
			A.Fake<ISystemLoadMonitor>(),
			clock);
	}
}
