// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience.Polly;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

using MsOptions = Microsoft.Extensions.Options.Options;

using CircuitState = Excalibur.Dispatch.Resilience.CircuitState;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Locks the two properties a <see cref="DistributedCircuitBreaker"/> named <i>Distributed</i> must hold.
/// Both are driven by TWO breaker instances sharing ONE store object — the only shape in which the
/// cross-instance property is observable at all. A single instance exercised twice cannot exhibit
/// either failure, because each breaker's own serialisation gate is a per-instance field.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Resilience)]
public sealed class DistributedCircuitBreakerCrossInstanceCountingShould
{
	/// <summary>
	/// One instance reaching its failure threshold must open the circuit for every instance sharing the
	/// store. This is the guarantee the type's name sells.
	/// </summary>
	[Fact]
	public async Task Open_on_every_instance_sharing_the_store_when_one_instance_trips()
	{
		var cache = NewSharedStore();
		var options = new DistributedCircuitBreakerOptions
		{
			ConsecutiveFailureThreshold = 2,
			MinimumThroughput = int.MaxValue,          // isolate the consecutive-failure arm
			SyncInterval = System.Threading.Timeout.InfiniteTimeSpan,   // no background sync; every read is on-demand
			BreakDuration = TimeSpan.FromMinutes(5),
		};

		await using var instanceA = NewBreaker("cross-instance-trip", cache, options);
		await using var instanceB = NewBreaker("cross-instance-trip", cache, options);

		await instanceA.RecordFailureAsync(CancellationToken.None, new InvalidOperationException("a1"));
		await instanceA.RecordFailureAsync(CancellationToken.None, new InvalidOperationException("a2"));

		(await instanceA.GetStateAsync(CancellationToken.None)).ShouldBe(CircuitState.Open);
		(await instanceB.GetStateAsync(CancellationToken.None)).ShouldBe(
			CircuitState.Open,
			"a trip on one instance must be visible to every instance sharing the store");
	}

	/// <summary>
	/// A concurrent instance must not be able to erase another instance's accumulated failure run.
	/// </summary>
	/// <remarks>
	/// RED before the fix. The failure counters lived in ONE shared cache entry mutated by an
	/// unsynchronised read-modify-write: instance B's success wrote <c>ConsecutiveFailures = 0</c> over the
	/// run instance A was accumulating, so A's next failure resumed from 1 and the threshold was never
	/// reached. The breaker undercounted and never opened — the exact failure the type exists to prevent.
	/// No forced interleaving is needed to show it: the writes are lost even when strictly ordered,
	/// because each instance's serialisation gate is a per-instance field that the other cannot observe.
	/// </remarks>
	[Fact]
	public async Task Trip_on_one_instance_even_when_another_instance_records_a_success_mid_run()
	{
		var cache = NewSharedStore();
		var options = new DistributedCircuitBreakerOptions
		{
			ConsecutiveFailureThreshold = 3,
			MinimumThroughput = int.MaxValue,          // isolate the consecutive-failure arm
			SyncInterval = System.Threading.Timeout.InfiniteTimeSpan,
			BreakDuration = TimeSpan.FromMinutes(5),
		};

		await using var instanceA = NewBreaker("cross-instance-crosstalk", cache, options);
		await using var instanceB = NewBreaker("cross-instance-crosstalk", cache, options);

		await instanceA.RecordFailureAsync(CancellationToken.None, new InvalidOperationException("a1"));
		await instanceA.RecordFailureAsync(CancellationToken.None, new InvalidOperationException("a2"));

		// A different replica succeeds against a different downstream shard. It must not reset the run
		// that THIS replica is accumulating.
		await instanceB.RecordSuccessAsync(CancellationToken.None);

		await instanceA.RecordFailureAsync(CancellationToken.None, new InvalidOperationException("a3"));

		(await instanceA.GetStateAsync(CancellationToken.None)).ShouldBe(
			CircuitState.Open,
			"three consecutive failures on one instance must trip it; a concurrent instance's success " +
			"must not erase the failure run it was accumulating");
	}

	private static MemoryDistributedCache NewSharedStore() =>
		new(MsOptions.Create(new MemoryDistributedCacheOptions()));

	private static DistributedCircuitBreaker NewBreaker(
		string name,
		IDistributedCache cache,
		DistributedCircuitBreakerOptions options) =>
		new(name, cache, MsOptions.Create(options), NullLogger<DistributedCircuitBreaker>.Instance);
}
